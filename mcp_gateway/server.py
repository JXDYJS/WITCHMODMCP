#!/usr/bin/env python3
"""
WitchModMCP Gateway Server — MCP stdio server that proxies to the game mod.

AI tools (opencode / Claude / Codex) connect to THIS server via stdio.
This server authenticates with the game mod's HTTP server, so AI tools
never talk to the mod directly.

On first heartbeat from the mod:
  1. Skill docs are synced to workspace .agents/skills/ and global opencode skills dir
  2. decompile_source is triggered to cache game source code

Environment variables:
    MCP_MOD_PORT    — game mod HTTP port (default: 3100)
    MCP_MOD_TOKEN   — auth token (default: reads from game ModConfig.json if found)
    MCP_HEARTBEAT_INTERVAL — heartbeat interval seconds (default: 5)
    MCP_HEARTBEAT_MAX_FAIL — consecutive failures before marking disconnected (default: 3)
    MCP_DECOMPILE_DIR      — decompile cache directory (default: workspace/.cache/game_src)
    MCP_GLOBAL_SKILLS      — set to "0" to disable global skills sync
"""

import json
import os
import sys
import http.client
import urllib.request
import urllib.error
from pathlib import Path

from mcp_gateway.heartbeat import HeartbeatManager
from mcp_gateway.skill_sync import sync_skill_docs

DEFAULT_MOD_PORT = 3100
DEFAULT_TOKEN = "witch-mod-mcp-dev-2026"


def find_mod_config() -> str | None:
    """Scan common game install paths for ModConfig.json."""
    candidates = [
        os.environ.get("MCP_MOD_CONFIG", ""),
        str(Path.home() / ".config" / "witch-mod-mcp" / "ModConfig.json"),
        # Steam default install path
        r"F:\steam\steamapps\common\Witch's Apocalyptic Journey\Witch's Apocalyptic Journey_Data\Mods\WitchModMCP\ModConfig.json",
        r"F:\steam\steamapps\common\Witch's Apocalyptic Journey\Witch's Apocalyptic Journey_Data\Mods\WitchModMCP.DeveloperTools\ModConfig.json",
    ]
    # Also scan Mods/WitchModMCP relative to this script
    script_dir = Path(__file__).resolve().parent
    for p in [script_dir / ".." / "【MOD文件夹】", script_dir.parent / "【MOD文件夹】"]:
        candidates.append(str(p / "ModConfig.json"))

    for c in candidates:
        if c and Path(c).exists():
            return c
    return None


def read_mod_config() -> dict:
    """Read game mod config to get port and token."""
    path = find_mod_config()
    if path:
        try:
            with open(path, "r", encoding="utf-8") as f:
                cfg = json.load(f)
                port = cfg.get("MCPPort", DEFAULT_MOD_PORT)
                token = cfg.get("MCPAuthToken", "")
                return {"port": port, "token": token, "config_path": path}
        except (json.JSONDecodeError, OSError):
            pass
    return {"port": DEFAULT_MOD_PORT, "token": DEFAULT_TOKEN, "config_path": None}


class ModConnection:
    """Connection to the game mod's HTTP server."""

    def __init__(self, port: int, token: str):
        self.port = port
        self.token = token
        self._conn: http.client.HTTPConnection | None = None

    def _get_conn(self) -> http.client.HTTPConnection:
        if self._conn is None:
            self._conn = http.client.HTTPConnection("localhost", self.port, timeout=5)
        return self._conn

    def ping(self) -> dict:
        """GET /ping — alive check (no auth required)."""
        try:
            conn = self._get_conn()
            conn.request("GET", "/ping")
            resp = conn.getresponse()
            body = resp.read().decode("utf-8")
            return json.loads(body) if resp.status == 200 else {"status": "error", "http_status": resp.status}
        except Exception as e:
            return {"status": "error", "message": str(e)}

    def call_tool(self, method: str, params: dict | None = None) -> dict:
        """POST JSON-RPC to the mod. Normalises PascalCase keys to lowercase."""
        body = json.dumps({
            "jsonrpc": "2.0",
            "id": 1,
            "method": method,
            "params": params or {}
        })
        try:
            conn = self._get_conn()
            conn.request(
                "POST", "/", body,
                {
                    "Content-Type": "application/json",
                    "Authorization": f"Bearer {self.token}",
                }
            )
            resp = conn.getresponse()
            data = json.loads(resp.read().decode("utf-8"))
            return self._lower_keys(data)
        except Exception as e:
            return {"jsonrpc": "2.0", "error": {"code": -32000, "message": f"Mod connection failed: {e}"}}

    @staticmethod
    def _lower_keys(d):
        """Recursively lowercase all dict keys (handles PascalCase from C# Newtonsoft)."""
        if isinstance(d, dict):
            return {k[0].lower() + k[1:]: ModConnection._lower_keys(v) for k, v in d.items()}
        elif isinstance(d, list):
            return [ModConnection._lower_keys(v) for v in d]
        return d


class McpGateway:
    """MCP stdio server that proxies tool calls to the game mod."""

    def __init__(self, mod: ModConnection, mod_config: dict, workspace_dir: str):
        self.mod = mod
        self.mod_config = mod_config
        self.workspace_dir = workspace_dir
        self._tool_cache: list[dict] | None = None
        self._session_id: str | None = None
        self._heartbeat: HeartbeatManager | None = None

    def _log(self, msg: str):
        print(f"[gateway] {msg}", file=sys.stderr, flush=True)

    # ── Skill sync + decompile on first heartbeat ──

    def _on_first_heartbeat(self, resp: dict):
        self._log(f"First heartbeat received — sessionId={resp.get('sessionId', '?')}")
        self._log(f"  toolCount={resp.get('toolCount', '?')}, activeModules={len(resp.get('activeModules', []))}")

        # 1. Sync skill docs
        local_skills = os.path.join(self.workspace_dir, ".agents", "skills")
        global_skills = None
        if os.environ.get("MCP_GLOBAL_SKILLS", "1") != "0":
            global_skills = str(Path.home() / ".config" / "opencode" / "skills")

        try:
            sync_result = sync_skill_docs(resp, local_skills, global_skills)
            for asm, count in sync_result.get("synced", {}).items():
                self._log(f"  synced skills: {asm} ({count} .md files)")
            for err in sync_result.get("errors", []):
                self._log(f"  sync error: {err}")
            if global_skills:
                self._log(f"  global skills dir: {global_skills}")
        except Exception as e:
            self._log(f"  skill sync failed: {e}")

        # 2. Trigger decompile_source
        decompile_dir = os.environ.get(
            "MCP_DECOMPILE_DIR",
            os.path.join(self.workspace_dir, ".cache", "game_src"),
        )
        os.makedirs(decompile_dir, exist_ok=True)

        try:
            decomp_resp = self.mod.call_tool("decompile_source", {"outputDir": decompile_dir})
            decomp_result = decomp_resp.get("result", {})
            status = decomp_result.get("status", "unknown")
            self._log(f"  decompile_source: {status}")
            if decomp_result.get("error"):
                self._log(f"  decompile error: {decomp_result['error']}")
        except Exception as e:
            self._log(f"  decompile_source call failed: {e}")

    # ── MCP stdio transport ──────────────────────────────────────

    def run(self):
        """Read JSON-RPC requests from stdin, write responses to stdout."""
        self._log(f"Mod port: {self.mod.port}, auth: {'enabled' if self.mod.token else 'disabled'}")
        self._log(f"Config source: {self.mod_config.get('config_path', 'defaults')}")
        self._log(f"Workspace: {self.workspace_dir}")

        # Start heartbeat manager
        interval = float(os.environ.get("MCP_HEARTBEAT_INTERVAL", "5"))
        max_fail = int(os.environ.get("MCP_HEARTBEAT_MAX_FAIL", "3"))

        self._heartbeat = HeartbeatManager(
            mod_conn=self.mod,
            workspace_dir=self.workspace_dir,
            on_first_heartbeat=self._on_first_heartbeat,
            interval=interval,
            max_failures=max_fail,
        )
        self._heartbeat.start()
        self._log("Heartbeat manager started — waiting for mod...")

        # MCP protocol: read lines from stdin
        for line in sys.stdin:
            line = line.strip()
            if not line:
                continue
            try:
                req = json.loads(line)
            except json.JSONDecodeError:
                self._send_error(None, -32700, "Parse error")
                continue

            result = self._handle_request(req)
            if result is not None:
                self._send_json(result)

        self._heartbeat.stop()

    def _handle_request(self, req: dict) -> dict | None:
        method = req.get("method", "")
        req_id = req.get("id")
        params = req.get("params", {})

        # ── MCP lifecycle ──
        if method == "initialize":
            return self._handle_initialize(req_id, params)

        if method == "notifications/initialized":
            return None  # no response expected

        if method == "ping":
            return self._mcp_response(req_id, {"status": "ok"})

        # ── Tool methods ──
        if method == "tools/list":
            return self._handle_tools_list(req_id)

        if method == "tools/call":
            return self._handle_tools_call(req_id, params)

        # ── Resources (for SKILL doc injection) ──
        if method == "resources/list":
            return self._handle_resources_list(req_id)

        if method == "resources/read":
            return self._handle_resources_read(req_id, params)

        return self._mcp_error(req_id, -32601, f"Method not found: {method}")

    # ── MCP message builders ──

    def _mcp_response(self, req_id, result: dict) -> dict:
        return {"jsonrpc": "2.0", "id": req_id, "result": result}

    def _mcp_error(self, req_id, code: int, message: str):
        return {"jsonrpc": "2.0", "id": req_id, "error": {"code": code, "message": message}}

    def _send_json(self, obj: dict):
        sys.stdout.write(json.dumps(obj, ensure_ascii=False) + "\n")
        sys.stdout.flush()

    def _send_error(self, req_id, code: int, message: str):
        self._send_json(self._mcp_error(req_id, code, message))

    # ── Handlers ──

    def _handle_initialize(self, req_id, params: dict) -> dict:
        self._session_id = str(id(self))
        return self._mcp_response(req_id, {
            "protocolVersion": params.get("protocolVersion", "0.1.0"),
            "capabilities": {
                "tools": {},
                "resources": {},
            },
            "serverInfo": {
                "name": "witch-mod-mcp-gateway",
                "version": "2.0.0",
            },
        })

    def _handle_tools_list(self, req_id) -> dict:
        if self._tool_cache is None:
            resp = self.mod.call_tool("list_tools")
            if "result" in resp:
                tools_raw = resp["result"].get("tools", [])
                self._tool_cache = [
                    {
                        "name": t["name"],
                        "description": t.get("description", ""),
                        "inputSchema": t.get("inputSchema", {"type": "object"}),
                    }
                    for t in tools_raw
                ]
            else:
                return self._mcp_error(req_id, -32000, "Failed to list tools from mod")

        return self._mcp_response(req_id, {"tools": self._tool_cache})

    def _handle_tools_call(self, req_id, params: dict) -> dict:
        tool_name = params.get("name", "")
        arguments = params.get("arguments", {})

        if not tool_name:
            return self._mcp_error(req_id, -32602, "tool name is required")

        # Check heartbeat manager's connection status
        if self._heartbeat and not self._heartbeat.connected:
            return self._mcp_error(req_id, -32000, "Mod is not reachable. Make sure the game is running with WitchModMCP loaded.")

        # Forward to mod
        resp = self.mod.call_tool(tool_name, arguments)
        err = resp.get("error")
        if err:
            return self._mcp_error(req_id, err.get("code", -32603), err.get("message", "Unknown mod error"))

        result = resp.get("result", {})
        return self._mcp_response(req_id, {"content": [{"type": "text", "text": json.dumps(result, ensure_ascii=False)}]})

    def _handle_resources_list(self, req_id) -> dict:
        resources = [
            {
                "uri": "witch-mod-mcp://tools/list",
                "name": "Available Tools (from mod)",
                "description": "Current tool list from the game mod",
                "mimeType": "application/json",
            },
            {
                "uri": "witch-mod-mcp://mod/status",
                "name": "Mod Connection Status",
                "description": "Whether the game mod is currently reachable",
                "mimeType": "application/json",
            },
        ]
        return self._mcp_response(req_id, {"resources": resources})

    def _handle_resources_read(self, req_id, params: dict) -> dict:
        uri = params.get("uri", "")
        if uri == "witch-mod-mcp://tools/list":
            resp = self.mod.call_tool("list_tools")
            text = json.dumps(resp.get("result", {}), indent=2, ensure_ascii=False)
            return self._mcp_response(req_id, {"contents": [{"uri": uri, "mimeType": "application/json", "text": text}]})
        elif uri == "witch-mod-mcp://mod/status":
            status = {
                "connected": self._heartbeat.connected if self._heartbeat else False,
                "first_heartbeat_done": self._heartbeat.first_heartbeat_done if self._heartbeat else False,
                "session_id": self._heartbeat.session_id if self._heartbeat else None,
            }
            text = json.dumps(status, indent=2, ensure_ascii=False)
            return self._mcp_response(req_id, {"contents": [{"uri": uri, "mimeType": "application/json", "text": text}]})
        else:
            return self._mcp_error(req_id, -32602, f"Resource not found: {uri}")


def main():
    # Read config
    mod_config = read_mod_config()
    port = int(os.environ.get("MCP_MOD_PORT", mod_config["port"]))
    token = os.environ.get("MCP_MOD_TOKEN", mod_config["token"])

    # Determine workspace directory (= parent of mcp_gateway/)
    workspace_dir = str(Path(__file__).resolve().parent.parent)

    mod = ModConnection(port, token)
    gateway = McpGateway(mod, mod_config, workspace_dir)
    gateway.run()


if __name__ == "__main__":
    main()