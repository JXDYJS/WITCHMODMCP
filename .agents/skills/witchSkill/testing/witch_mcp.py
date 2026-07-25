#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
WitchModMCP Python Helper
=========================

Thin client for the WitchModMCP in-game HTTP server (JSON-RPC 2.0 style).

- No third-party deps (stdlib http.client only).
- Server listens on http://localhost:<port>/ (default 3100).
- Every request is a POST of {"jsonrpc":"2.0","id":N,"method":<tool>,"params":{...}}.
- `method` IS the tool name (e.g. "get_game_data"). The special method
  "list_tools" returns the tool registry.
- Responses are serialized by Newtonsoft with PascalCase keys
  (Result / Error / Id), so both cases are handled transparently.

CLI usage:
    python witch_mcp.py ping
    python witch_mcp.py list_tools
    python witch_mcp.py get_game_data
    python witch_mcp.py eval_command '{"command": "help give"}'
    python witch_mcp.py inspect '{"typeName": "RoleTable", "memberPath": "Instance.San"}'
    python witch_mcp.py --port 3100 query_config '{"tableName": "CardConfig", "limit": 3}'
    # PowerShell double-quote escaping:
    python witch_mcp.py search_config '{\"pattern\": \"buff\", \"limit\": 5}'
"""

import sys
if sys.platform == "win32":
    import codecs
    if hasattr(sys.stdout, "buffer"):
        sys.stdout = codecs.getwriter("utf-8")(sys.stdout.buffer, "replace")
    if hasattr(sys.stderr, "buffer"):
        sys.stderr = codecs.getwriter("utf-8")(sys.stderr.buffer, "replace")

import json
import http.client
from typing import Any, Dict, Optional

DEFAULT_PORT = 3100
DEFAULT_TIMEOUT = 15


class WitchMcpError(Exception):
    """Raised when the server returns a JSON-RPC error object."""

    def __init__(self, code: int, message: str):
        super().__init__(f"[{code}] {message}")
        self.code = code
        self.message = message


class WitchMcp:
    """Client for one WitchModMCP server instance."""

    def __init__(self, port: int = DEFAULT_PORT, host: str = "localhost",
                 timeout: int = DEFAULT_TIMEOUT):
        self.host = host
        self.port = port
        self.timeout = timeout
        self._id = 0

    def call(self, method: str, params: Optional[Dict[str, Any]] = None) -> Any:
        """Send one JSON-RPC request and return the unwrapped result.

        Raises WitchMcpError on a JSON-RPC error, ConnectionError on transport
        failure (game not running / wrong port / mod not loaded).
        """
        self._id += 1
        body: Dict[str, Any] = {"jsonrpc": "2.0", "id": self._id, "method": method}
        if params is not None:
            body["params"] = params

        try:
            conn = http.client.HTTPConnection(self.host, self.port, timeout=self.timeout)
            conn.request("POST", "/", json.dumps(body), {"Content-Type": "application/json"})
            resp = conn.getresponse()
            raw = resp.read().decode("utf-8")
            conn.close()
        except (ConnectionError, OSError) as exc:
            raise ConnectionError(
                f"Cannot reach WitchModMCP at {self.host}:{self.port} "
                f"({exc}). Is the game running with the mod loaded? "
                f"Check ModConfig.json MCPPort."
            ) from exc

        data = json.loads(raw)
        err = data.get("Error") or data.get("error")
        if err:
            raise WitchMcpError(err.get("code") or err.get("Code"),
                                err.get("message") or err.get("Message"))
        result = data.get("Result")
        if result is None:
            result = data.get("result")
        return result

    # --- connectivity -----------------------------------------------------
    def ping(self) -> bool:
        """Return True if the server responds to list_tools."""
        try:
            self.call("list_tools")
            return True
        except Exception:
            return False

    # --- discovery --------------------------------------------------------
    def list_tools(self) -> Any:
        return self.call("list_tools")

    def list_commands(self) -> Any:
        return self.call("list_commands")

    def reload_tools(self) -> Any:
        return self.call("reload_tools")

    # --- read-only state --------------------------------------------------
    def get_game_data(self) -> Any:
        return self.call("get_game_data")

    def get_recent_logs(self, count: int = 50) -> Any:
        return self.call("get_recent_logs", {"count": count})

    def dump_mod_state(self) -> Any:
        return self.call("dump_mod_state")

    def get_scene_tree(self, root_name: Optional[str] = None, max_depth: int = 10,
                       max_children: int = 50, include_components: bool = True,
                       include_inactive: bool = False) -> Any:
        params: Dict[str, Any] = {
            "maxDepth": max_depth,
            "maxChildren": max_children,
            "includeComponents": include_components,
            "includeInactive": include_inactive,
        }
        if root_name:
            params["rootName"] = root_name
        return self.call("get_scene_tree", params)

    def inspect(self, type_name: str, member_path: Optional[str] = None,
                max_depth: int = 3, max_items: int = 20) -> Any:
        params: Dict[str, Any] = {"typeName": type_name, "maxDepth": max_depth,
                                  "maxItems": max_items}
        if member_path:
            params["memberPath"] = member_path
        return self.call("inspect", params)

    def query_config(self, table_name: Optional[str] = None,
                     item_id: Optional[int] = None, limit: int = 5) -> Any:
        params: Dict[str, Any] = {"limit": limit}
        if table_name:
            params["tableName"] = table_name
        if item_id is not None:
            params["id"] = item_id
        return self.call("query_config", params)

    def search_config(self, pattern: str, limit: int = 20,
                      include_fields: bool = False) -> Any:
        return self.call("search_config", {
            "pattern": pattern,
            "limit": limit,
            "includeFields": include_fields,
        })

    # --- mutations (change game state) ------------------------------------
    def eval_command(self, command: str) -> Any:
        return self.call("eval_command", {"command": command})

    def give_item(self, item_type: str, value: str) -> Any:
        return self.call("give_item", {"type": item_type, "value": str(value)})

    def load_scene(self, scene_type: str, scene_id: Optional[str] = None) -> Any:
        params: Dict[str, Any] = {"type": scene_type}
        if scene_id:
            params["id"] = scene_id
        return self.call("load_scene", params)


def _main(argv) -> int:
    port = DEFAULT_PORT
    args = list(argv)
    if len(args) >= 2 and args[0] == "--port":
        port = int(args[1])
        args = args[2:]

    if not args:
        print(__doc__)
        return 1

    method = args[0]
    params = None
    if len(args) >= 2:
        params = json.loads(args[1])

    client = WitchMcp(port=port)

    if method == "ping":
        alive = client.ping()
        print("alive" if alive else "unreachable")
        return 0 if alive else 1

    try:
        result = client.call(method, params)
    except (WitchMcpError, ConnectionError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1

    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(_main(sys.argv[1:]))
