"""
SimpleMCP — minimal MCP stdio server, zero external dependencies.

Replaces FastMCP with a self-contained stdio JSON-RPC 2.0 transport.
Public API mirrors the subset of FastMCP that tools.py and resources.py use.
"""

import asyncio
import json
import sys
import threading
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

_client_uses_content_length: bool | None = None  # detected from first incoming frame

def _set_output_format(content_length: bool):
    global _client_uses_content_length
    if _client_uses_content_length is None:
        _client_uses_content_length = content_length

def _write_stdout(payload: str):
    """Write a message to stdout. Adapts output format to match what the
    client sent (Content-Length framed or newline-delimited JSON)."""
    body = payload.encode("utf-8")
    if _client_uses_content_length:
        header = f"Content-Length: {len(body)}\r\n\r\n".encode("utf-8")
        sys.stdout.buffer.write(header + body)
    else:
        sys.stdout.buffer.write(body + b"\n")
    sys.stdout.buffer.flush()


class WriteStream:
    async def send(self, data: Any):
        payload = json.dumps(data, ensure_ascii=False)
        _write_stdout(payload)


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

def _read_frame() -> str | None:
    """Read one MCP frame from stdin (blocking, thread-safe).

    Returns the JSON body string, or None on EOF.

    Supports both Content-Length framed and newline-delimited JSON.
    Some MCP clients (e.g. OpenCode) send raw JSON terminated by \\n
    instead of the Content-Length prefix. We detect the format by
    inspecting the first byte.
    """
    buf = sys.stdin.buffer
    while True:
        line = buf.readline()
        if not line:
            return None

        stripped = line.strip()
        if not stripped:
            continue

        # ── Content-Length framed ──
        if stripped.startswith(b"Content-Length:"):
            _set_output_format(True)
            try:
                length = int(stripped.split(b":", 1)[1].strip())
            except ValueError:
                continue
            buf.readline()          # consume blank separator line
            body = buf.read(length)
            return body.decode("utf-8", errors="replace")

        # ── Newline-delimited JSON (OpenCode, etc.) ──
        if stripped.startswith(b"{"):
            _set_output_format(False)
            return stripped.decode("utf-8", errors="replace")


async def run_stdio_async(mcp: SimpleMCP):
    """Run MCP server over stdin/stdout with auto-detected framing.

    Uses a background thread to read stdin (works cross-platform).
    Sets module-level globals so the heartbeat thread can schedule tools
    registration and send notifications.  Event loop stays free for
    heartbeat callbacks and tool handler dispatch.
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

    _running = [True]
    _stopped = threading.Event()

    async def handle_frame(body: str):
        try:
            msg = json.loads(body)
        except json.JSONDecodeError:
            return

        msg_id = msg.get("id")
        method = msg.get("method")
        params = msg.get("params", {})

        if msg_id is None:
            return

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

        _write_stdout(json.dumps(response, ensure_ascii=False))

    def _stdin_loop():
        while _running[0]:
            body = _read_frame()
            if body is None:
                _running[0] = False
                break
            asyncio.run_coroutine_threadsafe(handle_frame(body), loop)
        _stopped.set()

    reader_thread = threading.Thread(target=_stdin_loop, daemon=True, name="mcp-stdin")
    reader_thread.start()

    # Yield to event loop until stdin reader thread finishes
    await asyncio.get_event_loop().run_in_executor(None, _stopped.wait)
