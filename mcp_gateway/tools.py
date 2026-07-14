#!/usr/bin/env python3
"""
tools — MCP Tool definitions for WitchModMCP.

Dynamically discovers tools from the C# game mod via list_tools.
No hardcoded @mcp.tool() per C# tool — C# adds/removes tools autonomously.

During startup (before heartbeat connects), only a minimal set of
Python-native tools is available. After first heartbeat succeeds,
all C# tools are automatically registered.
"""

import json
import logging
from mcp.server.fastmcp import FastMCP
from mcp_gateway.mod_client import ModConnection
from mcp_gateway.heartbeat import HeartbeatManager

log = logging.getLogger(__name__)

# ── Shared state set by init() ─────────────────────────────────────────

_mod: ModConnection | None = None
_heartbeat: HeartbeatManager | None = None
_mcp: FastMCP | None = None


def init(mcp_instance: FastMCP, mod: ModConnection, heartbeat: HeartbeatManager):
    global _mod, _heartbeat, _mcp
    _mod = mod
    _heartbeat = heartbeat
    _mcp = mcp_instance


# ── Forwarding helpers ─────────────────────────────────────────────────

def _forward(tool_name: str, arguments: dict | None = None) -> str:
    """Forward a tool call to the game mod, with connection check.

    Returns a JSON string suitable for MCP text content.
    """
    if _heartbeat is None or not _heartbeat.connected:
        return json.dumps({
            "error": "Game mod is not reachable. Start the game with WitchModMCP loaded.",
            "hint": "Heartbeat has not yet connected. Wait for the mod to finish loading.",
        }, ensure_ascii=False)

    camel_args = _to_camel(arguments) if arguments else None
    resp = _mod.call_tool(tool_name, camel_args)

    err = resp.get("error")
    if err:
        return json.dumps(err, ensure_ascii=False)

    return json.dumps(resp.get("result", resp), ensure_ascii=False, indent=2)


def _to_camel(d: dict) -> dict:
    """Convert snake_case dict keys to camelCase for the C# mod.

    Examples:
        root_name -> rootName
        max_depth -> maxDepth
    """
    result = {}
    for k, v in d.items():
        parts = k.split("_")
        camel_key = parts[0].lower() + "".join(p[0].upper() + p[1:] for p in parts[1:] if p)
        result[camel_key] = _to_camel(v) if isinstance(v, dict) else v
    return result


# ── Core (always-available) tools ──────────────────────────────────────

def register_core_tools(mcp: FastMCP) -> int:
    """Register Python-native tools that don't depend on C# mod.

    These are always available even before heartbeat connects.
    Returns the number of tools registered.
    """

    @mcp.tool()
    def list_tools() -> str:
        """Return the full MCP tool registry from the game mod.

        Lists every tool with name, description, and inputSchema.
        Read resource://witchmod/tools/core for module overview.
        """
        return _forward("list_tools")

    @mcp.tool()
    def list_commands() -> str:
        """Return all available game console commands with parameter signatures.

        Read resource://witchmod/tools/core for eval_command usage patterns.
        """
        return _forward("list_commands")

    return 2


# ── Dynamic C# tool discovery ─────────────────────────────────────────

def register_dynamic_tools() -> int:
    """Fetch tool list from C# mod and register each as an MCP tool.

    Must be called AFTER first heartbeat succeeds (mod reachable).
    Returns the number of tools registered.
    """
    if _mod is None or _mcp is None:
        return 0

    try:
        resp = _mod.call_tool("list_tools", {})
    except Exception as e:
        log.warning(f"register_dynamic_tools: list_tools call failed: {e}")
        return 0

    result = resp.get("result")
    if not result:
        return 0

    csharp_tools = result.get("tools", [])
    if not csharp_tools:
        return 0

    count = 0
    for t in csharp_tools:
        name = t.get("name")
        if not name:
            continue

        # Skip tools already registered (e.g. list_tools on Python side)
        try:
            _mcp.remove_tool(name)
        except Exception:
            pass

        desc = t.get("description", "")
        schema = t.get("inputSchema", {"type": "object"})

        def handler(**kwargs):
            return _forward(name, kwargs)
        handler.__name__ = name
        if desc:
            handler.__doc__ = desc

        _mcp.add_tool(handler, name=name, description=desc)

        # Patch parameters schema to match C# definition
        _patch_tool_schema(name, schema)

        count += 1

    log.info(f"Registered {count} dynamic tools from C# mod")
    return count


def _patch_tool_schema(name: str, schema: dict):
    """Replace the auto-generated parameters schema on a Tool object
    with the one provided by C#, so AI sees correct parameter names.
    """
    try:
        tm = getattr(_mcp, "_tool_manager", None)
        if tm is None:
            return
        tool_obj = tm._tools.get(name)
        if tool_obj is None:
            return
        if schema:
            tool_obj.parameters = schema
    except Exception:
        pass
