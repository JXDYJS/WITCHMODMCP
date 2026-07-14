#!/usr/bin/env python3
"""
WitchModMCP Gateway — MCP stdio server (FastMCP-based).

This is the entry point AI tools connect to via stdio.
It proxies tool calls to the game mod's HTTP server and exposes
skill documentation as MCP Resources.

Stages:
  [x] Stage 1 — FastMCP skeleton + mod_client + heartbeat
  [ ] Stage 2 — Resources (skill docs mapped to resource:// URIs)
  [ ] Stage 3 — Low-risk read-only tools
  [ ] Stage 4 — High-risk mutation tools with guardrails

Environment variables:
    MCP_MOD_PORT       — game mod HTTP port (default: from ModConfig or 3100)
    MCP_MOD_TOKEN      — auth token (default: from ModConfig or built-in)
    MCP_HEARTBEAT_INTERVAL — heartbeat interval seconds (default: 5)
    MCP_HEARTBEAT_MAX_FAIL — consecutive failures before disconnected (default: 3)
    MCP_DECOMPILE_DIR  — decompile cache directory (default: workspace/.cache/game_src)
"""

import os
import sys
from pathlib import Path

from mcp.server.fastmcp import FastMCP

from mcp_gateway.heartbeat import HeartbeatManager
from mcp_gateway.mod_client import ModConnection, read_mod_config
from mcp_gateway.resources import register_resources
from mcp_gateway.tools import init as tools_init, register_core_tools, register_dynamic_tools

# ── Workspace path (resolved once at import time) ────────────────────

_workspace_dir = str(Path(__file__).resolve().parent.parent)

# ── Global state ────────────────────────────────────────────────────
_heartbeat: HeartbeatManager | None = None
_mod: ModConnection | None = None

# ── FastMCP app ─────────────────────────────────────────────────────
mcp = FastMCP(
    name="witch-mod-mcp-gateway",
    instructions="WitchModMCP gateway server v3.0.0 — proxies MCP tools to the game mod and exposes skill documentation as Resources.",
)


# ── Stderr logging ──────────────────────────────────────────────────

def log(msg: str):
    """Log to stderr. stdout is reserved for MCP JSON-RPC protocol traffic."""
    print(f"[gateway] {msg}", file=sys.stderr, flush=True)


# ── Connection check helper (used by tools in later stages) ─────────

def check_mod_connected() -> bool:
    """Return True if the game mod is reachable."""
    return _heartbeat is not None and _heartbeat.connected


# ── First-heartbeat callback ────────────────────────────────────────

def _on_first_heartbeat(resp: dict):
    """Triggered on first successful heartbeat from the game mod.

    Dynamically registers all C# tools and triggers decompile_source.
    """
    sid = resp.get("sessionId", "?")
    tool_count = resp.get("toolCount", "?")
    modules = resp.get("activeModules", [])
    log(f"First heartbeat — sessionId={sid}, toolCount={tool_count}, "
        f"activeModules={len(modules)}")

    if _mod is None:
        log("  first-heartbeat: no mod connection, skipping")
        return

    # 1. Dynamically register all C# tools
    dyn_count = register_dynamic_tools()
    log(f"  registered {dyn_count} dynamic tools from C# mod")

    # 2. Trigger decompile_source
    decompile_dir = os.environ.get(
        "MCP_DECOMPILE_DIR",
        os.path.join(_workspace_dir, ".cache", "game_src"),
    )
    os.makedirs(decompile_dir, exist_ok=True)

    try:
        decomp_resp = _mod.call_tool("decompile_source", {"outputDir": decompile_dir})
        result = decomp_resp.get("result", {})
        status = result.get("status", "unknown")
        log(f"  decompile_source: {status}")
        if result.get("error"):
            log(f"  decompile error: {result['error']}")
    except Exception as e:
        log(f"  decompile_source failed: {e}")


# ── Entry point ─────────────────────────────────────────────────────

def main():
    global _mod, _heartbeat

    # 1. Read configuration
    mod_config = read_mod_config()
    port = int(os.environ.get("MCP_MOD_PORT") or mod_config["port"])
    token = os.environ.get("MCP_MOD_TOKEN") or mod_config["token"]

    log(f"Mod port: {port}, auth: {'enabled' if token else 'disabled'}")
    log(f"Config source: {mod_config.get('config_path', 'defaults')}")
    log(f"Workspace: {_workspace_dir}")

    # 2. Create mod connection
    _mod = ModConnection(port, token)

    # 3. Start heartbeat (background daemon thread)
    interval = float(os.environ.get("MCP_HEARTBEAT_INTERVAL") or "5")
    max_fail = int(os.environ.get("MCP_HEARTBEAT_MAX_FAIL") or "3")

    _heartbeat = HeartbeatManager(
        mod_conn=_mod,
        workspace_dir=_workspace_dir,
        on_first_heartbeat=_on_first_heartbeat,
        interval=interval,
        max_failures=max_fail,
    )
    _heartbeat.start()
    log("Heartbeat manager started — waiting for game mod...")

    # 3.5. Initialize tools module with shared state
    tools_init(mcp, _mod, _heartbeat)

    # 3.6. Register skill documentation as MCP Resources
    resource_count = register_resources(mcp, _workspace_dir)
    log(f"Registered {resource_count} skill doc resources")

    # 3.7. Register core tools (always available, before heartbeat)
    core_count = register_core_tools(mcp)
    log(f"Registered {core_count} core tools")

    # (dynamic C# tools register on first heartbeat via _on_first_heartbeat)

    # 4. Run MCP stdio server (blocks until stdin closes)
    try:
        mcp.run(transport="stdio")
    finally:
        log("Shutting down...")
        _heartbeat.stop()
        log("Gateway stopped.")


if __name__ == "__main__":
    main()
