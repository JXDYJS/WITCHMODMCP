#!/usr/bin/env python3
"""
WitchModMCP Gateway — MCP stdio server (zero external deps).

This is the entry point AI tools connect to via stdio.
It proxies tool calls to the game mod's HTTP server and exposes
skill documentation as MCP Resources.

Design (dynamic discovery):
  * On startup, only `ping` is registered (plus skill-doc Resources).
  * The MCP handshake completes immediately — the client sees an empty
    tools list except for `ping`.
  * When the first heartbeat to the game mod succeeds (background thread),
    we fetch the C# tool registry and dynamically register every mod tool
    with its native inputSchema, then send `notifications/tools/list_changed`
    so the client re-fetches tools/list and sees all 76+ tools.
  * All registration + notification happens on the asyncio event loop
    (scheduled from the heartbeat thread via run_coroutine_threadsafe)
    — no race with concurrent tools/list messages.

Environment variables:
    MCP_MOD_PORT             — game mod HTTP port (default: from ModConfig or 3100)
    MCP_HEARTBEAT_INTERVAL   — heartbeat interval seconds (default: 5)
    MCP_HEARTBEAT_MAX_FAIL   — consecutive failures before disconnected (default: 3)
    MCP_DECOMPILE_DIR        — decompile cache directory
    MCP_DISABLE_DECOMPILE    — set to "1" to skip auto-decompile on first heartbeat
"""

import asyncio
import json
import os
import sys
from pathlib import Path

from mcp_gateway.mcp_transport import SimpleMCP, run_stdio_async

from mcp_gateway.heartbeat import HeartbeatManager
from mcp_gateway.mod_client import ModConnection, read_mod_config
from mcp_gateway.resources import register_resources
from mcp_gateway.tools import (
    init as tools_init,
    register_core_tools,
    register_dynamic_tools,
    register_dynamic_sync,
    unregister_dynamic_tools,
)

# ── Workspace path (resolved once at import time) ────────────────────

_workspace_dir = str(Path(__file__).resolve().parent.parent)

# ── Global state ────────────────────────────────────────────────────
_heartbeat: HeartbeatManager | None = None
_mod: ModConnection | None = None

# Captured from inside the patched run_stdio_async (NOT before anyio.run()).
# These are used by the heartbeat background thread to schedule async work
# (tool registration + list_changed notification) on the event loop.
_active_loop: asyncio.AbstractEventLoop | None = None
_active_write_stream = None  # type: ignore[var-annotated]

# Track last-known tool count / reload version so we can re-register when
# the C# side rebuilds or reloads its tool assembly.
_last_tool_count: int = 0
_last_reload_count: int = 0

# ── MCP app (SimpleMCP — no external deps) ──────────────────────────
mcp = SimpleMCP(
    name="witch-mod-mcp-gateway",
    instructions=(
        "WitchModMCP gateway server v3.0.0 — proxies MCP tools to the game mod. "
        "Tools are discovered dynamically after the game mod heartbeat connects; "
        "wait for notifications/tools/list_changed before calling game-mod tools. "
        "If a tool returns 'Game mod is not reachable', start the game with the "
        "WitchModMCP mod loaded."
    ),
)


# ── Stderr logging ──────────────────────────────────────────────────

def log(msg: str):
    """Log to stderr. stdout is reserved for MCP JSON-RPC protocol traffic."""
    print(f"[gateway] {msg}", file=sys.stderr, flush=True)


# ── Connection check helper ──────────────────────────────────────────

def check_mod_connected() -> bool:
    """Return True if the game mod is reachable right now."""
    return _heartbeat is not None and _heartbeat.connected


# ── Decompile helper ─────────────────────────────────────────────────

def _trigger_decompile():
    if _mod is None:
        return
    if os.environ.get("MCP_DISABLE_DECOMPILE") == "1":
        log("  decompile skipped (MCP_DISABLE_DECOMPILE=1)")
        return
    decompile_dir = os.environ.get(
        "MCP_DECOMPILE_DIR",
        os.path.join(_workspace_dir, ".cache", "game_src"),
    )
    os.makedirs(decompile_dir, exist_ok=True)
    try:
        resp = _mod.call_tool("decompile_source", {"outputDir": decompile_dir})
        result = resp.get("result", {})
        status = result.get("status", "unknown")
        log(f"  decompile_source: {status}")
        if result.get("error"):
            log(f"  decompile error: {result['error']}")
    except Exception as e:
        log(f"  decompile_source failed: {e}")


# ── list_changed notification ───────────────────────────────────────

async def _send_tool_list_changed():
    if _active_write_stream is None:
        log("  cannot send tools/list_changed — write_stream not captured")
        return

    await _active_write_stream.send({
        "jsonrpc": "2.0",
        "method": "notifications/tools/list_changed",
    })


async def _after_first_heartbeat_async():
    """Runs on the asyncio event loop when the game mod first connects.

    1. Remove any previously-registered dynamic tools (clean slate, so a
       re-register after a transient disconnect gives a fresh registry).
    2. Register all C# mod tools, each with its native inputSchema.
    3. Send notifications/tools/list_changed so the client re-fetches.
    """
    try:
        unregister_dynamic_tools()
    except Exception as e:
        log(f"  unregister_dynamic_tools failed: {e}")

    try:
        count = register_dynamic_tools()
        log(f"  register_dynamic_tools: {count} tools registered")
    except Exception as e:
        log(f"  register_dynamic_tools failed: {e}")
        return

    # Cache game path for deploy_mod
    try:
        if _mod is not None:
            resp = _mod.call_tool("get_game_info", {})
            if not resp.get("error"):
                gi = resp.get("result", {})
                gr = gi.get("gameRoot") or ""
                if gr:
                    from mcp_gateway.tools import cache_game_path
                    cache_game_path(gr)
                    log(f"  cached game path: {gr}")
    except Exception as e:
        log(f"  cache_game_path failed: {e}")

    try:
        await _send_tool_list_changed()
        log("  sent notifications/tools/list_changed")
    except Exception as e:
        log(f"  send_tool_list_changed failed: {e}")


# ── First-heartbeat callback (runs in heartbeat thread) ─────────────

def _on_first_heartbeat(resp: dict):
    """Triggered by the heartbeat daemon thread on first successful contact
    with the game mod. Schedules an async re-registration + notification
    on the main event loop so freshly compiled C# tools get picked up
    without restarting the gateway.
    """
    global _last_tool_count, _last_reload_count
    sid = resp.get("sessionId", "?")
    _last_tool_count = resp.get("toolCount", 0)
    _last_reload_count = resp.get("reloadCount", 0)
    log(f"First heartbeat — sessionId={sid}, toolCount={_last_tool_count}")

    if _mod is None:
        log("  first-heartbeat: no mod connection, skipping")
        return

    _trigger_decompile()

    if _active_loop is None or _active_loop.is_closed():
        log("  cannot re-register tools — event loop not captured yet")
        return

    fut = asyncio.run_coroutine_threadsafe(
        _after_first_heartbeat_async(), _active_loop
    )
    def _log_failure(f: asyncio.Future):
        if f.cancelled(): return
        exc = f.exception()
        if exc is not None:
            log(f"  _after_first_heartbeat_async raised: {exc!r}")
    fut.add_done_callback(_log_failure)


def _on_heartbeat(resp: dict):
    """Triggered on every heartbeat after the first. Re-registers tools when
    tool count changes (e.g. after reload_tools added new tools) or when
    the C# reload count bumps (schema changes).
    """
    global _last_tool_count, _last_reload_count
    tool_count = resp.get("toolCount", 0)
    reload_count = resp.get("reloadCount", 0)

    changed = False
    if tool_count != _last_tool_count and tool_count > 0:
        prev = _last_tool_count
        _last_tool_count = tool_count
        log(f"Heartbeat: toolCount changed from {prev} to {tool_count}")
        changed = True

    if reload_count != _last_reload_count:
        prev = _last_reload_count
        _last_reload_count = reload_count
        log(f"Heartbeat: reloadCount changed from {prev} to {reload_count}")
        changed = True

    if not changed:
        return

    if _active_loop is None or _active_loop.is_closed():
        log("  cannot re-register — event loop not available")
        return

    fut = asyncio.run_coroutine_threadsafe(
        _after_first_heartbeat_async(), _active_loop
    )
    def _log_failure(f: asyncio.Future):
        if f.cancelled(): return
        exc = f.exception()
        if exc is not None:
            log(f"  _on_heartbeat async raised: {exc!r}")
    fut.add_done_callback(_log_failure)


# ── Entry point ─────────────────────────────────────────────────────

def main():
    global _mod, _heartbeat

    # 1. Read configuration
    mod_config = read_mod_config()
    port = int(os.environ.get("MCP_MOD_PORT") or mod_config["port"])

    log(f"Mod port: {port}")
    log(f"Config source: {mod_config.get('config_path', 'defaults')}")
    log(f"Workspace: {_workspace_dir}")

    # 2. Create mod connection
    _mod = ModConnection(port)

    # 3. Start heartbeat (background daemon thread, infinite retry).
    #    The MCP server starts immediately; the heartbeat retries in
    #    the background until the game mod shows up. The first time the
    #    mod responds, _on_first_heartbeat fires (on the heartbeat thread)
    #    and schedules dynamic tool registration on our event loop.
    interval = float(os.environ.get("MCP_HEARTBEAT_INTERVAL") or "5")
    max_fail = int(os.environ.get("MCP_HEARTBEAT_MAX_FAIL") or "3")

    _heartbeat = HeartbeatManager(
        mod_conn=_mod,
        workspace_dir=_workspace_dir,
        on_first_heartbeat=_on_first_heartbeat,
        on_heartbeat=_on_heartbeat,
        interval=interval,
        max_failures=max_fail,
    )
    _heartbeat.start()
    log("Heartbeat manager started — background retries until game mod responds")

    # 4. Initialize tools module with shared state (_mod, _heartbeat, _mcp)
    tools_init(mcp, _mod, _heartbeat)

    # 5. Register CORE tool (ping) — always available.
    core_count = register_core_tools(mcp)
    log(f"Registered {core_count} core tool (ping)")

    # 5b. Try to synchronously discover and register C# tools from the mod.
    #     This works if the game mod is already running when the gateway
    #     starts. If not, only ping is registered until the heartbeat
    #     connects (which schedules a re-register + list_changed for
    #     future opencode versions that support dynamic discovery).
    try:
        disc_resp = _mod.call_tool("list_tools", {})
        disc_err = disc_resp.get("error")
        if not disc_err:
            tools_info = disc_resp.get("result", {}).get("tools", [])
            if tools_info:
                dyn_count = register_dynamic_sync(mcp, tools_info)
                log(f"Registered {dyn_count} C# tools (sync startup discovery)")
            else:
                log("C# mod returned empty tool list — only ping available")
        else:
            log(f"C# mod responded with error: {disc_err}")
    except (ConnectionError, OSError, Exception) as e:
        log(f"C# mod not reachable at startup ({e}) — only ping until heartbeat")

    # 6. Register skill documentation as MCP Resources (always available)
    resource_count = register_resources(mcp)
    log(f"Registered {resource_count} skill doc resources")

    # 7. Run MCP stdio server (blocks until stdin closes).
    #    run_stdio_async handles Content-Length framing and JSON-RPC dispatch.
    #    It also captures the asyncio event loop and write_stream so the
    #    heartbeat thread can schedule async work (tool registration + notifications).
    try:
        asyncio.run(run_stdio_async(mcp))
    finally:
        log("Shutting down...")
        _heartbeat.stop()
        log("Gateway stopped.")


if __name__ == "__main__":
    try:
        main()
    except Exception:
        import traceback
        traceback.print_exc()
        sys.exit(1)