#!/usr/bin/env python3
"""
mod_client — HTTP client for the WitchModMCP in-game JSON-RPC server.

All HTTP communication with the game mod flows through this module.
Every request creates a fresh connection (thread-safe).
"""

import json
import os
import sys
import threading
import http.client

DEFAULT_MOD_PORT = 3100


def log(msg: str):
    """Log diagnostic messages to stderr (never stdout)."""
    print(f"[mod_client] {msg}", file=sys.stderr, flush=True)


class ModConnection:
    """Manages HTTP communication with the game mod's built-in JSON-RPC server.

    Connection-per-request design ensures thread safety between the
    heartbeat thread and MCP handler thread.
    """

    def __init__(self, port: int):
        self.port = port
        self._id_counter = 0
        self._id_lock = threading.Lock()

    # ── low-level HTTP helpers ───────────────────────────────────────

    def _request(self, method: str, path: str,
                 body: str | None = None,
                 timeout: int = 5) -> tuple[int, str]:
        """Send an HTTP request and return (status_code, response_body).

        Creates a fresh connection per call for thread safety.
        """
        conn = http.client.HTTPConnection("localhost", self.port, timeout=timeout)
        try:
            headers = {"Content-Type": "application/json"}
            conn.request(method, path, body, headers)
            resp = conn.getresponse()
            data = resp.read().decode("utf-8", errors="replace")
            return resp.status, data
        finally:
            conn.close()

    # ── public API ───────────────────────────────────────────────────

    def call_tool(self, method: str, params: dict | None = None) -> dict:
        """POST JSON-RPC to the mod. Normalises PascalCase keys to camelCase.

        Args:
            method: Tool name (e.g. "get_game_data", "eval_command").
            params: Tool arguments dict.

        Returns:
            Normalised JSON-RPC response dict (result / error / jsonrpc / id).

        Note:
            Tool calls use a 120s timeout because some C# tools (decompile_source,
            get_fight_state during complex fights, etc.) may take many seconds.
        """
        with self._id_lock:
            self._id_counter += 1
            req_id = self._id_counter
        req_body = json.dumps({
            "jsonrpc": "2.0",
            "id": req_id,
            "method": method,
            "params": params or {},
        })
        try:
            status, body = self._request("POST", "/", req_body, timeout=120)
            if status != 200:
                return {
                    "jsonrpc": "2.0",
                    "error": {
                        "code": -32000,
                        "message": f"Mod returned HTTP {status}: {body[:200]}",
                    },
                }
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
        """Recursively convert dict keys from PascalCase to camelCase
        (handles C# Newtonsoft serialization: JsonRpc -> jsonRpc)."""
        if isinstance(d, dict):
            result = {}
            for k, v in d.items():
                key = k[0].lower() + k[1:] if k else k
                result[key] = ModConnection._lower_keys(v)
            return result
        elif isinstance(d, list):
            return [ModConnection._lower_keys(v) for v in d]
        return d
