#!/usr/bin/env python3
"""
mod_client — HTTP client for the WitchModMCP in-game JSON-RPC server.

All HTTP communication with the game mod flows through this module.
Every request creates a fresh connection (thread-safe).
"""

import json
import os
import sys
import http.client
from pathlib import Path

DEFAULT_MOD_PORT = 3100
DEFAULT_TOKEN = "witch-mod-mcp-dev-2026"

# Mod 源码目录：工作区下存放待部署 Mod 文件的文件夹名。
# 可通过环境变量 MCP_MOD_SOURCE_DIR 覆盖（不需要重命名实际目录）。
# csproj 中同名字段需同步修改（搜索 `【MOD文件夹】`）。
MOD_SOURCE_DIR = os.environ.get("MCP_MOD_SOURCE_DIR", "【MOD文件夹】")


def log(msg: str):
    """Log diagnostic messages to stderr (never stdout)."""
    print(f"[mod_client] {msg}", file=sys.stderr, flush=True)


def find_mod_config() -> str | None:
    """Scan possible paths for ModConfig.json and return the first match."""
    candidates = [
        os.environ.get("MCP_MOD_CONFIG", ""),
        str(Path.home() / ".config" / "witch-mod-mcp" / "ModConfig.json"),
    ]
    script_dir = Path(__file__).resolve().parent
    for p in [script_dir / ".." / MOD_SOURCE_DIR, script_dir.parent / MOD_SOURCE_DIR]:
        candidates.append(str(p / "ModConfig.json"))

    for c in candidates:
        if c and Path(c).exists():
            return c
    return None


def read_mod_config() -> dict:
    """Read game mod config to get port and token.

    Returns:
        {"port": int, "token": str, "config_path": str|None}
    """
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
    """Manages HTTP communication with the game mod's built-in JSON-RPC server.

    Connection-per-request design ensures thread safety between the
    heartbeat thread and MCP handler thread.
    """

    def __init__(self, port: int, token: str):
        self.port = port
        self.token = token
        self._id_counter = 0

    # ── low-level HTTP helpers ───────────────────────────────────────

    def _request(self, method: str, path: str,
                 body: str | None = None,
                 auth: bool = False,
                 timeout: int = 5) -> tuple[int, str]:
        """Send an HTTP request and return (status_code, response_body).

        Creates a fresh connection per call for thread safety.
        """
        conn = http.client.HTTPConnection("localhost", self.port, timeout=timeout)
        try:
            headers = {"Content-Type": "application/json"}
            if auth and self.token:
                headers["Authorization"] = f"Bearer {self.token}"

            conn.request(method, path, body, headers)
            resp = conn.getresponse()
            data = resp.read().decode("utf-8", errors="replace")
            return resp.status, data
        finally:
            conn.close()

    # ── public API ───────────────────────────────────────────────────

    def ping(self) -> dict:
        """GET /ping — alive check (no auth required)."""
        try:
            status, body = self._request("GET", "/ping")
            if status == 200:
                return json.loads(body)
            return {"status": "error", "http_status": status}
        except json.JSONDecodeError:
            return {"status": "error", "message": "Invalid JSON response"}
        except Exception as e:
            return {"status": "error", "message": str(e)}

    def call_tool(self, method: str, params: dict | None = None) -> dict:
        """POST JSON-RPC to the mod. Normalises PascalCase keys to lowercase.

        Args:
            method: Tool name (e.g. "get_game_data", "eval_command").
            params: Tool arguments dict.

        Returns:
            Normalised JSON-RPC response dict with lowercase keys
            (result / error / jsonrpc / id).
        """
        self._id_counter += 1
        req_body = json.dumps({
            "jsonrpc": "2.0",
            "id": self._id_counter,
            "method": method,
            "params": params or {},
        })
        try:
            status, body = self._request("POST", "/", req_body, auth=True)
            data = json.loads(body)
            return self._lower_keys(data)
        except json.JSONDecodeError:
            return {
                "jsonrpc": "2.0",
                "error": {"code": -32700, "message": "Invalid JSON response from mod"},
            }
        except Exception as e:
            return {
                "jsonrpc": "2.0",
                "error": {"code": -32000, "message": f"Mod connection failed: {e}"},
            }

    def send_heartbeat(self, workspace_dir: str) -> tuple[bool, dict | None]:
        """POST /heartbeat — send a heartbeat to the mod (no auth).

        Returns:
            (ok, response_dict) where ok is True on status==200 and
            the response dict contains the parsed JSON body.
        """
        body = json.dumps({
            "workspacePath": workspace_dir,
            "pid": os.getpid(),
            "keepalive": True,
        })
        try:
            status, raw = self._request("POST", "/heartbeat", body)
            data = json.loads(raw)
            if status == 200 and data.get("status") == "ok":
                return True, data
            return False, data
        except json.JSONDecodeError:
            return False, {"error": "Invalid JSON response"}
        except Exception as e:
            return False, {"error": str(e)}

    # ── helpers ──────────────────────────────────────────────────────

    @staticmethod
    def _lower_keys(d):
        """Recursively lowercase all dict keys (handles PascalCase from C# Newtonsoft)."""
        if isinstance(d, dict):
            result = {}
            for k, v in d.items():
                key = k[0].lower() + k[1:] if k else k
                result[key] = ModConnection._lower_keys(v)
            return result
        elif isinstance(d, list):
            return [ModConnection._lower_keys(v) for v in d]
        return d
