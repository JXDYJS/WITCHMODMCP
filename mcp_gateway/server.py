#!/usr/bin/env python3
"""
WitchModMCP Gateway Server — MCP stdio server that proxies to the game mod.

AI tools (opencode / Claude / Codex) connect to THIS server via stdio.
This server authenticates with the game mod's HTTP server, so AI tools
never talk to the mod directly.

Usage in opencode / Claude Desktop config:
    "witch-mod-mcp": {
        "command": "python",
        "args": ["path/to/mcp_gateway/server.py"]
    }

Environment variables:
    MCP_MOD_PORT    — game mod HTTP port (default: 3100)
    MCP_MOD_TOKEN   — auth token (default: reads from game ModConfig.json if found)
    MCP_GATEWAY_PORT — not used (stdio-only)
"""

import json
import os
import sys
import http.client
import urllib.request
import urllib.error
from pathlib import Path

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
            # Normalise PascalCase → lowercase for predictable access
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

    def __init__(self, mod: ModConnection, mod_config: dict):
        self.mod = mod
        self.mod_config = mod_config
        self._tool_cache: list[dict] | None = None
        self._session_id: str | None = None

    # ── MCP stdio transport ──────────────────────────────────────

    def run(self):
        """Read JSON-RPC requests from stdin, write responses to stdout."""
        # Log to stderr so it doesn't interfere with MCP stdio protocol
        log = lambda msg: print(f"[gateway] {msg}", file=sys.stderr, flush=True)

        log(f"Mod port: {self.mod.port}, auth: {'enabled' if self.mod.token else 'disabled'}")
        log(f"Config source: {self.mod_config.get('config_path', 'defaults')}")

        # Verify mod is alive before accepting any requests
        alive = self.mod.ping()
        if alive.get("status") != "ok":
            log(f"MOD NOT REACHABLE at localhost:{self.mod.port}")
            log(f"ping response: {alive}")
            log("Make sure the game is running with WitchModMCP loaded.")
            log("Gateway will start but tools will return errors until mod is reachable.")
        else:
            log(f"Mod alive — auth={'yes' if alive.get('auth') else 'no'}, tools will proxy through")

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
                "version": "1.0.0",
            },
        })

    def _handle_tools_list(self, req_id) -> dict:
        # Call mod's list_tools and cache
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

        # Check mod is still alive before forwarding
        alive = self.mod.ping()
        if alive.get("status") != "ok":
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
            alive = self.mod.ping()
            text = json.dumps(alive, indent=2, ensure_ascii=False)
            return self._mcp_response(req_id, {"contents": [{"uri": uri, "mimeType": "application/json", "text": text}]})
        else:
            return self._mcp_error(req_id, -32602, f"Resource not found: {uri}")


def main():
    # Read config
    mod_config = read_mod_config()
    port = int(os.environ.get("MCP_MOD_PORT", mod_config["port"]))
    token = os.environ.get("MCP_MOD_TOKEN", mod_config["token"])

    mod = ModConnection(port, token)
    gateway = McpGateway(mod, mod_config)
    gateway.run()


if __name__ == "__main__":
    main()
