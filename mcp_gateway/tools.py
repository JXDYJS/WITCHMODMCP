#!/usr/bin/env python3
"""
tools — MCP Tool definitions for WitchModMCP.

Design:
  Before first heartbeat:  MCP exposes ONLY `ping` (zero game-mod tools).
  After first heartbeat:    Dynamically registers ALL C# mod tools, each with
                            its native inputSchema, then sends
                            `notifications/tools/list_changed` so the client
                            re-fetches tools/list and discovers them.

Thread-safety:
  Tool registration mutations run on the asyncio event loop (scheduled from
  the heartbeat daemon thread via asyncio.run_coroutine_threadsafe), so there
  is no race with tools/list being served concurrently.
"""

import inspect
import json
import logging
from typing import Any

from mcp.server.fastmcp import FastMCP
from mcp_gateway.mod_client import ModConnection
from mcp_gateway.heartbeat import HeartbeatManager

log = logging.getLogger(__name__)

# JSON Schema type → Python annotation (for building handler signatures)
_SCHEMA_TYPE_MAP: dict[str, type] = {
    "string": str,
    "integer": int,
    "number": float,
    "boolean": bool,
    "array": list,
    "object": dict,
}

# ── Shared state set by init() ─────────────────────────────────────────
_mod: ModConnection | None = None
_heartbeat: HeartbeatManager | None = None
_mcp: FastMCP | None = None

# Names of tools that survive unregister_dynamic_tools (always-available core)
_CORE_TOOL_NAMES: set[str] = {"ping"}


def init(mcp_instance: FastMCP, mod: ModConnection,
         heartbeat: HeartbeatManager) -> None:
    global _mod, _heartbeat, _mcp
    _mod = mod
    _heartbeat = heartbeat
    _mcp = mcp_instance


# ── Forwarding helpers ─────────────────────────────────────────────────

def _to_camel(d: dict) -> dict:
    """Convert snake_case dict keys to camelCase for the C# Newtonsoft-backed mod.

    The C# mod expects camelCase parameter names (e.g. "targetIndex", "maxDepth").
    Keys that are already camelCase are passed through unchanged.
    Examples:
        root_name -> rootName
        max_depth -> maxDepth
        targetIndex -> targetIndex  (unchanged)
        cardId -> cardId            (unchanged)
    """
    result: dict[str, Any] = {}
    for k, v in d.items():
        if "_" not in k:
            result[k] = _to_camel(v) if isinstance(v, dict) else v
            continue
        parts = k.split("_")
        camel_key = parts[0].lower() + "".join(
            p[0].upper() + p[1:] for p in parts[1:] if p
        )
        result[camel_key] = _to_camel(v) if isinstance(v, dict) else v
    return result


def _forward(tool_name: str, arguments: dict | None = None) -> str:
    """Forward a tool call to the game mod, with connection check.

    Returns a JSON string suitable for MCP text content.
    """
    if _heartbeat is None or not _heartbeat.connected:
        return json.dumps({
            "error": "Game mod is not reachable.",
            "hint": "Start the game with WitchModMCP loaded, then wait for heartbeat.",
        }, ensure_ascii=False)

    camel_args = _to_camel(arguments) if arguments else None
    if camel_args is not None:
        camel_args = {k: v for k, v in camel_args.items() if v is not None}
    resp = _mod.call_tool(tool_name, camel_args)

    err = resp.get("error")
    if err:
        return json.dumps(err, ensure_ascii=False)

    return json.dumps(resp.get("result", resp), ensure_ascii=False, indent=2)


# ── Core (always-available) tools ──────────────────────────────────────

def register_core_tools(mcp: FastMCP) -> int:
    """Register Python-native tools that don't depend on C# mod.

    These are available even before heartbeat connects.
    Returns the number of tools registered.
    """
    @mcp.tool()
    async def ping() -> str:
        """Simple ping-pong test. Returns {"ok": true} — verifies the gateway
        process is alive. Does NOT verify the game mod is reachable; use
        list_tools (after heartbeat) for that.
        """
        return json.dumps({"ok": True})

    return 1


# ── Dynamic C# tool discovery ─────────────────────────────────────────

def register_dynamic_tools() -> int:
    """Fetch the tool list from the C# mod and register each as an MCP tool.

    MUST be called from the asyncio event loop thread (after `_mod.call_tool`
    succeeds, i.e. after the first heartbeat). Idempotent: tools already
    registered are skipped.

    Returns the number of NEW tools registered (does not count skipped ones).
    """
    if _mod is None or _mcp is None:
        log.warning("register_dynamic_tools: mod/mcp not initialised")
        return 0

    # Ask the C# mod for its full tool registry
    try:
        resp = _mod.call_tool("list_tools", {})
    except Exception as e:
        log.warning(f"register_dynamic_tools: list_tools call failed: {e}")
        return 0

    err = resp.get("error")
    if err:
        log.warning(f"register_dynamic_tools: list_tools returned error: {err}")
        return 0

    result = resp.get("result") or resp
    if not isinstance(result, dict):
        log.warning(f"register_dynamic_tools: unexpected result type: {type(result)}")
        return 0

    csharp_tools = result.get("tools", [])
    if not csharp_tools:
        log.warning("register_dynamic_tools: C# mod returned empty tool list")
        return 0

    return _register_tool_list(csharp_tools)


def register_dynamic_sync(mcp: FastMCP, tools_list: list) -> int:
    """Register C# tools from a pre-fetched tool list (e.g. fetched at startup).

    Unlike register_dynamic_tools(), this doesn't call the mod — it uses the
    list provided. Called from main() BEFORE mcp.run() so tools appear in
    the initial tools/list response.
    """
    return _register_tool_list(tools_list)


def _register_tool_list(csharp_tools: list) -> int:
    """Internal helper: register a list of C# tool definitions on _mcp.

    csharp_tools: list of dicts with keys: name, description, inputSchema.

    Must be called from the asyncio event loop thread (or synchronously
    before mcp.run()).
    """
    global _mcp
    if _mcp is None:
        return 0

    tm = getattr(_mcp, "_tool_manager", None)
    if tm is None:
        return 0

    already_registered = set(tm._tools.keys())
    count = 0

    for t in csharp_tools:
        name = t.get("name")
        if not name:
            continue

        # Skip already-registered tools (idempotency)
        if name in already_registered:
            continue

        desc = t.get("description") or ""
        schema = t.get("inputSchema") or {"type": "object"}

        # Build a handler with a real signature derived from the C# inputSchema.
        sig = _build_signature_from_schema(schema)
        handler = _make_handler(name)
        handler.__name__ = name
        handler.__signature__ = sig
        handler.__doc__ = desc or f"C# mod tool: {name}"

        _mcp.add_tool(handler, name=name, description=desc)
        count += 1

    if count:
        log.info(f"Registered {count} C# tools (total now: {len(tm._tools)})")
    return count


# ── Dynamic handler construction ─────────────────────────────────────

def _make_handler(tool_name: str):
    """Build a closure-bound async handler for one C# mod tool.

    The function body uses **kwargs so it can receive whatever FastMCP's
    pydantic validation passes through; the visible signature (used by
    FastMCP for schema generation + call-time validation) is supplied
    separately via __signature__ override.
    """
    async def _handler(**kwargs):
        return _forward(tool_name, kwargs)
    return _handler


def _build_signature_from_schema(schema: dict) -> inspect.Signature:
    """Convert a JSON-Schema-style inputSchema (properties/required) into a
    Python inspect.Signature that FastMCP's func_metadata will consume.

    - Required params → POSITIONAL_OR_KEYWORD with no default
    - Optional params → POSITIONAL_OR_KEYWORD with default=None
    - Type annotations are mapped from JSON Schema "type" when possible,
      otherwise Any is used (Pydantic accepts anything).
    """
    properties = schema.get("properties", {}) or {}
    required = set(schema.get("required", []) or [])

    params: list[inspect.Parameter] = []
    for prop_name, prop_schema in properties.items():
        json_type = (prop_schema or {}).get("type")
        annotation = _SCHEMA_TYPE_MAP.get(json_type, Any) if json_type else Any

        if prop_name in required:
            default = inspect.Parameter.empty
        else:
            # JSON-Schema "default" wins if present; otherwise None.
            default = prop_schema.get("default", None) if prop_schema else None

        params.append(inspect.Parameter(
            name=prop_name,
            kind=inspect.Parameter.POSITIONAL_OR_KEYWORD,
            annotation=annotation,
            default=default,
        ))

    return inspect.Signature(params)


def unregister_dynamic_tools() -> int:
    """Remove all dynamically-registered tools, keeping only the core ones
    (e.g. `ping`).

    Useful for re-registration flows (e.g. reload_mod_tools).
    MUST be called from the asyncio event loop thread.

    Returns the number of tools removed.
    """
    if _mcp is None:
        return 0
    tm = getattr(_mcp, "_tool_manager", None)
    if tm is None:
        return 0

    removed = 0
    for name in list(tm._tools.keys()):
        if name in _CORE_TOOL_NAMES:
            continue
        try:
            tm.remove_tool(name)
            removed += 1
        except Exception as e:
            log.debug(f"Failed to remove tool {name}: {e}")

    if removed:
        log.info(f"Unregistered {removed} dynamic tools "
                 f"(kept {len(tm._tools)} core)")
    return removed