"""
SimpleMCP — minimal MCP stdio server, zero external dependencies.

Replaces FastMCP with a self-contained stdio JSON-RPC 2.0 transport.
Public API mirrors the subset of FastMCP that tools.py and resources.py use.
"""

import asyncio
import json
import sys
from typing import Any


class _ToolManager:
    """Internal helper so tools.py's `_mcp._tool_manager` access still works."""
    def __init__(self, owner: "SimpleMCP"):
        self._owner = owner

    @property
    def _tools(self):
        return self._owner._tools

    def remove_tool(self, name: str):
        self._owner._tools.pop(name, None)


class SimpleMCP:
    def __init__(self, name: str = "mcp", instructions: str = ""):
        self.name = name
        self.instructions = instructions
        self._tools: dict[str, dict] = {}
        self._resources: dict[str, dict] = {}
        self._tool_manager = _ToolManager(self)

    # ── Tool API (matches FastMCP subset) ──

    def tool(self, name: str | None = None, description: str = ""):
        def decorator(func):
            n = name or func.__name__
            self._tools[n] = {
                "handler": func,
                "description": description or func.__doc__ or "",
                "input_schema": {"type": "object"},
            }
            return func
        return decorator

    def add_tool(self, handler, *, name: str, description: str = ""):
        self._tools[name] = {
            "handler": handler,
            "description": description,
            "input_schema": {"type": "object"},
        }

    def remove_tool(self, name: str):
        self._tools.pop(name, None)

    # ── Resource API (matches FastMCP subset) ──

    def resource(self, uri: str, name: str = "", description: str = ""):
        def decorator(func):
            self._resources[uri] = {
                "handler": func,
                "name": name,
                "description": description,
            }
            return func
        return decorator


# ── Write stream ──

class WriteStream:
    """Minimal write stream: accepts dict or raw-JSON string, frames and
    writes to stdout via Content-Length framing.

    Handles both the `SessionMessage`-style dict (from tools.py's
    send_tool_list_changed) and plain JSON payloads.
    """

    async def send(self, data: Any):
        payload = json.dumps(data, ensure_ascii=False)
        sys.stdout.write(f"Content-Length: {len(payload)}\r\n\r\n{payload}")
        sys.stdout.flush()


# ── JSON-RPC dispatch ──

async def _dispatch(mcp: SimpleMCP, method: str, params: dict) -> dict:
    if method == "initialize":
        return {
            "protocolVersion": "2024-11-05",
            "capabilities": {"tools": {}, "resources": {}},
            "serverInfo": {"name": mcp.name, "version": "1.0.0"},
            "instructions": mcp.instructions,
        }

    if method == "ping":
        return {}

    if method == "tools/list":
        return {
            "tools": [
                {
                    "name": n,
                    "description": v["description"],
                    "inputSchema": v.get("input_schema", {"type": "object"}),
                }
                for n, v in mcp._tools.items()
            ]
        }

    if method == "tools/call":
        name = params.get("name", "")
        arguments = params.get("arguments", {})
        tool = mcp._tools.get(name)
        if tool is None:
            return {"error": {"code": -32601, "message": f"Tool not found: {name}"}}
        try:
            result = await tool["handler"](**arguments)
            return {"content": [{"type": "text", "text": result}]}
        except Exception as e:
            return {"error": {"code": -32603, "message": f"{e}"}}

    if method == "resources/list":
        return {
            "resources": [
                {
                    "uri": u,
                    "name": v["name"],
                    "description": v["description"],
                    "mimeType": "text/markdown",
                }
                for u, v in mcp._resources.items()
            ]
        }

    if method == "resources/read":
        uri = params.get("uri", "")
        resource = mcp._resources.get(uri)
        if resource is None:
            return {"error": {"code": -32602, "message": f"Resource not found: {uri}"}}
        text = await resource["handler"]()
        return {
            "contents": [{"uri": uri, "mimeType": "text/markdown", "text": text}]
        }

    return {"error": {"code": -32601, "message": f"Method not found: {method}"}}


# ── Stdio transport ──

async def run_stdio_async(mcp: SimpleMCP):
    """Run MCP server over stdin/stdout with Content-Length framing.

    Yields control to the asyncio event loop. Sets module-level globals
    (`_active_loop`, tools._write_stream) so the heartbeat thread can
    schedule async work and send notifications.
    """
    # Capture event loop for heartbeat thread
    import mcp_gateway.server as _server_mod
    import mcp_gateway.tools as _tools_mod

    loop = asyncio.get_running_loop()
    _server_mod._active_loop = loop

    # Wire up a write stream on the tools module (for notifications)
    write_stream = WriteStream()
    _tools_mod._write_stream = write_stream
    _server_mod._active_write_stream = write_stream

    # Pipe stdin into an asyncio StreamReader
    reader = asyncio.StreamReader()
    protocol = asyncio.StreamReaderProtocol(reader)
    await loop.connect_read_pipe(lambda: protocol, sys.stdin)

    async def _read_line() -> str | None:
        raw = await reader.readline()
        if not raw:
            return None
        return raw.decode("utf-8", errors="replace").rstrip("\r\n")

    async def _read_body(n: int) -> str:
        raw = await reader.readexactly(n)
        return raw.decode("utf-8", errors="replace")

    while True:
        line = await _read_line()
        if line is None:
            break
        if not line.startswith("Content-Length:"):
            continue

        try:
            length = int(line.split(":", 1)[1].strip())
        except ValueError:
            continue

        blank = await _read_line()
        if blank is None or blank != "":
            continue

        body = await _read_body(length)

        try:
            msg = json.loads(body)
        except json.JSONDecodeError:
            continue

        msg_id = msg.get("id")
        method = msg.get("method")
        params = msg.get("params", {})

        if msg_id is None:
            continue

        try:
            result = await _dispatch(mcp, method, params)
            error = result.pop("error", None) if isinstance(result, dict) else None

            response = {"jsonrpc": "2.0", "id": msg_id}
            if error:
                response["error"] = error
            else:
                response["result"] = result
        except Exception as e:
            response = {
                "jsonrpc": "2.0",
                "id": msg_id,
                "error": {"code": -32603, "message": str(e)},
            }

        payload = json.dumps(response, ensure_ascii=False)
        sys.stdout.write(f"Content-Length: {len(payload)}\r\n\r\n{payload}")
        sys.stdout.flush()
