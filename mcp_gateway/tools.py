#!/usr/bin/env python3
"""
tools — MCP Tool definitions for WitchModMCP.

Design:
  Before first heartbeat:  MCP exposes ONLY `ping` (zero game-mod tools).
  After first heartbeat:    Dynamically registers ALL C# mod tools, each with
                            its native inputSchema, then sends
                            `notifications/tools/list_changed` so the client
                            re-fetches tools/list and discovers them.

Thread-safety:
  Tool registration mutations run on the asyncio event loop (scheduled from
  the heartbeat daemon thread via asyncio.run_coroutine_threadsafe), so there
  is no race with tools/list being served concurrently.
"""

import asyncio
import inspect
import json
import logging
from typing import Any

from mcp_gateway.mcp_transport import SimpleMCP
from mcp_gateway.mod_client import ModConnection
from mcp_gateway.heartbeat import HeartbeatManager

log = logging.getLogger(__name__)

# JSON Schema type → Python annotation (for building handler signatures)
_SCHEMA_TYPE_MAP: dict[str, type] = {
    "string": str,
    "integer": int,
    "number": float,
    "boolean": bool,
    "array": list,
    "object": dict,
}

# ── Shared state set by init() ─────────────────────────────────────────
_mod: ModConnection | None = None
_heartbeat: HeartbeatManager | None = None
_mcp: SimpleMCP | None = None
_write_stream = None  # Captured write stream for list_changed notification

# Names of tools that survive unregister_dynamic_tools (always-available core)
_CORE_TOOL_NAMES: set[str] = {"ping", "reload_tools", "deploy_mod"}


def init(mcp_instance: SimpleMCP, mod: ModConnection,
         heartbeat: HeartbeatManager,
         write_stream=None) -> None:
    global _mod, _heartbeat, _mcp, _write_stream
    _mod = mod
    _heartbeat = heartbeat
    _mcp = mcp_instance
    _write_stream = write_stream


# ── Forwarding helpers ─────────────────────────────────────────────────

def _to_camel(d: dict) -> dict:
    """Convert snake_case dict keys to camelCase for the C# Newtonsoft-backed mod.

    The C# mod expects camelCase parameter names (e.g. "targetIndex", "maxDepth").
    Keys that are already camelCase are passed through unchanged.
    Examples:
        root_name -> rootName
        max_depth -> maxDepth
        targetIndex -> targetIndex  (unchanged)
        cardId -> cardId            (unchanged)
    """
    result: dict[str, Any] = {}
    for k, v in d.items():
        if "_" not in k:
            result[k] = _to_camel(v) if isinstance(v, dict) else v
            continue
        parts = k.split("_")
        camel_key = parts[0].lower() + "".join(
            p[0].upper() + p[1:] for p in parts[1:] if p
        )
        result[camel_key] = _to_camel(v) if isinstance(v, dict) else v
    return result


def _forward(tool_name: str, arguments: dict | None = None) -> str:
    """Forward a tool call to the game mod, with connection check.

    Returns a JSON string suitable for MCP text content.
    """
    if _heartbeat is None or not _heartbeat.connected:
        return json.dumps({
            "error": "Game mod is not reachable.",
            "hint": "Start the game with WitchModMCP loaded, then wait for heartbeat.",
        }, ensure_ascii=False)

    camel_args = _to_camel(arguments) if arguments else None
    if camel_args is not None:
        camel_args = {k: v for k, v in camel_args.items() if v is not None}
    resp = _mod.call_tool(tool_name, camel_args)

    err = resp.get("error")
    if err:
        return json.dumps(err, ensure_ascii=False)

    return json.dumps(resp.get("result", resp), ensure_ascii=False, indent=2)


# ── Cached game path ──────────────────────────────────────────────

_LAST_GAME_PATH: str | None = None

def cache_game_path(path: str):
    global _LAST_GAME_PATH
    _LAST_GAME_PATH = path

# ── Send list_changed notification ───────────────────────────────────

async def send_tool_list_changed():
    global _write_stream
    if _write_stream is None:
        return

    await _write_stream.send({
        "jsonrpc": "2.0",
        "method": "notifications/tools/list_changed",
    })


# ── Core (always-available) tools ──────────────────────────────────────

def register_core_tools(mcp: SimpleMCP) -> int:
    """Register Python-native tools that don't depend on C# mod.

    These are available even before heartbeat connects.
    Returns the number of tools registered.
    """
    @mcp.tool()
    async def ping() -> str:
        """Simple ping-pong test. Returns {"ok": true} — verifies the gateway
        process is alive. Does NOT verify the game mod is reachable; use
        list_tools (after heartbeat) for that.
        """
        return json.dumps({"ok": True})

    @mcp.tool()
    async def reload_tools() -> str:
        """热重载所有 MCP 工具并同步更新网关 schema。
        
        调用后 C# 端重新加载工具 DLL，网关自动重新拉取最新的 inputSchema，
        使新增的参数（如 get_recent_logs 的 level 筛选）立即生效。
        """
        if _mod is None:
            return json.dumps({"error": "Game mod not connected"})
        
        resp = _mod.call_tool("reload_tools", {})
        err = resp.get("error")
        if err:
            return json.dumps(err, ensure_ascii=False)
        
        csharp_result = resp.get("result", resp)
        
        try:
            disc_resp = _mod.call_tool("list_tools", {})
            if not disc_resp.get("error"):
                tools_info = disc_resp.get("result", {}).get("tools", [])
                if tools_info:
                    unregister_dynamic_tools()
                    count = _register_tool_list(tools_info)
                    log.info(f"reload_tools: schema re-sync registered {count} tools")
                    try:
                        await send_tool_list_changed()
                        log.info("reload_tools: sent tools/list_changed")
                    except Exception as n:
                        log.warning(f"reload_tools: list_changed failed: {n}")
        except Exception as e:
            log.warning(f"reload_tools: schema re-sync failed: {e}")
        
        return json.dumps(csharp_result, ensure_ascii=False, indent=2)

    @mcp.tool()
    async def deploy_mod(mod_path: str, game_path: str | None = None,
                          restart_delay: int = 5) -> str:
        """部署 Mod 到游戏并重启验证。
        
        将指定文件夹复制到游戏的 Mods 目录，重启游戏，等待重新连接，
        然后验证 Mod 是否加载成功并检查日志错误。
        
        支持 Windows。macOS 会返回错误提示。
        
        Args:
            mod_path: Mod 文件夹路径（如 E:\\WitchModPlayer\\Nightwatcher）
            game_path: 游戏安装路径（可选，自动检测 Steam 安装位置）
            restart_delay: 重启前等待秒数（默认 5）
        """
        import os, shutil, subprocess, sys, time
        
        # ── Platform check ──
        if sys.platform != "win32":
            return json.dumps({
                "success": False,
                "error": f"deploy_mod not available on {sys.platform}",
            }, ensure_ascii=False)
        
        mod_name = os.path.basename(os.path.normpath(mod_path))
        if not mod_name or not os.path.isdir(mod_path):
            return json.dumps({
                "success": False,
                "error": f"Invalid mod path: {mod_path}",
            }, ensure_ascii=False)
        
        # ── Resolve game path ──
        resolved_game_path = game_path or _LAST_GAME_PATH
        if not resolved_game_path and _mod is not None:
            try:
                resp = _mod.call_tool("get_game_info", {})
                if not resp.get("error"):
                    gi = resp.get("result", {})
                    rp = gi.get("gameRoot") or ""
                    if rp and os.path.isdir(rp):
                        resolved_game_path = rp
            except Exception:
                pass
        
        if not resolved_game_path or not os.path.isdir(resolved_game_path):
            # Try common Steam paths
            steam_paths = [
                "F:\\steam\\steamapps\\common\\Witch's Apocalyptic Journey",
                os.path.expandvars("%ProgramFiles%\\Steam\\steamapps\\common\\Witch's Apocalyptic Journey"),
                os.path.expandvars("%ProgramFiles(x86)%\\Steam\\steamapps\\common\\Witch's Apocalyptic Journey"),
                "D:\\steam\\steamapps\\common\\Witch's Apocalyptic Journey",
            ]
            for sp in steam_paths:
                sp = os.path.normpath(sp)
                if os.path.isdir(sp):
                    resolved_game_path = sp
                    break
        
        if not resolved_game_path or not os.path.isdir(resolved_game_path):
            return json.dumps({
                "success": False,
                "error": "Cannot find game install path. Provide game_path or connect to game first.",
            }, ensure_ascii=False)
        
        cache_game_path(resolved_game_path)
        
        # Determine data dir and mods dir
        data_dir = None
        for d in os.listdir(resolved_game_path):
            full = os.path.join(resolved_game_path, d)
            if os.path.isdir(full) and d.endswith("_Data"):
                data_dir = full
                break
        
        if not data_dir:
            return json.dumps({
                "success": False,
                "error": f"Cannot find _Data directory under {resolved_game_path}",
            }, ensure_ascii=False)
        
        mods_dir = os.path.join(data_dir, "Mods")
        if not os.path.isdir(mods_dir):
            return json.dumps({
                "success": False,
                "error": f"Mods directory not found: {mods_dir}",
            }, ensure_ascii=False)
        
        target_dir = os.path.join(mods_dir, mod_name)
        
        # ── Copy mod files ──
        try:
            if os.path.exists(target_dir):
                shutil.rmtree(target_dir)
            shutil.copytree(mod_path, target_dir)
            log.info(f"deploy_mod: copied {mod_path} → {target_dir}")
        except Exception as e:
            return json.dumps({
                "success": False,
                "error": f"Failed to copy mod: {e}",
            }, ensure_ascii=False)
        
        # ── Find game executable ──
        exe_candidates = []
        product_name = os.path.basename(resolved_game_path)
        exe_candidates.append(os.path.join(resolved_game_path, f"{product_name}.exe"))
        exe_candidates.append(os.path.join(resolved_game_path, f"{product_name}.app", "Contents", "MacOS", product_name))
        for f in os.listdir(resolved_game_path):
            if f.lower().endswith(".exe"):
                exe_candidates.append(os.path.join(resolved_game_path, f))
        
        game_exe = None
        for ec in exe_candidates:
            if os.path.isfile(ec):
                game_exe = ec
                break
        
        if not game_exe:
            return json.dumps({
                "success": False,
                "error": f"Game executable not found in {resolved_game_path}",
            }, ensure_ascii=False)
        
        # ── Kill game process ──
        exe_name = os.path.basename(game_exe)
        try:
            subprocess.run(["taskkill", "/F", "/IM", exe_name],
                           capture_output=True, timeout=10)
            log.info(f"deploy_mod: killed {exe_name}")
        except Exception as e:
            log.warning(f"deploy_mod: kill attempt: {e}")
        
        time.sleep(1)
        
        # Wait for process exit
        for _ in range(30):
            try:
                r = subprocess.run(
                    ["tasklist", "/FI", f"IMAGENAME eq {exe_name}"],
                    capture_output=True, text=True, timeout=5
                )
                if exe_name not in r.stdout:
                    break
            except Exception:
                pass
            time.sleep(0.5)
        
        time.sleep(restart_delay)
        
        # ── Start game process ──
        try:
            subprocess.Popen([game_exe], shell=True)
            log.info(f"deploy_mod: started {game_exe}")
        except Exception as e:
            return json.dumps({
                "success": True,
                "warning": f"Mod deployed but game start failed: {e}. Start manually.",
                "modName": mod_name,
                "targetPath": target_dir
            }, ensure_ascii=False)
        
        # ── Wait for heartbeat ──
        connected = False
        for i in range(60):
            time.sleep(1)
            if _heartbeat is not None and _heartbeat.connected:
                connected = True
                time.sleep(2)  # let tools fully register
                break
        
        if not connected:
            return json.dumps({
                "success": True,
                "warning": "Mod files deployed but game did not reconnect within 60s. Start manually.",
                "modName": mod_name,
                "targetPath": target_dir,
            }, ensure_ascii=False)
        
        # ── Verify mod loaded ──
        mod_found = False
        mod_errors = []
        try:
            ms_resp = _mod.call_tool("dump_mod_state", {})
            if not ms_resp.get("error"):
                ms = ms_resp.get("result", {})
                for key in ("mods", "loadedMods", "activeMods"):
                    lst = ms.get(key, [])
                    if isinstance(lst, list):
                        for m in lst:
                            mname = ""
                            if isinstance(m, dict):
                                mname = m.get("name", "") or m.get("modName", "") or ""
                            elif isinstance(m, str):
                                mname = m
                            if mod_name.lower() in mname.lower():
                                mod_found = True
                                break
        except Exception as e:
            mod_errors.append(f"dump_mod_state error: {e}")
        
        # Check logs for errors from this mod
        try:
            log_resp = _mod.call_tool("get_recent_logs",
                                       {"count": 200, "level": "Error"})
            if not log_resp.get("error"):
                entries = log_resp.get("result", [])
                if isinstance(entries, list):
                    for entry in entries:
                        msg = entry.get("message", "")
                        if mod_name.lower() in msg.lower():
                            mod_errors.append(msg)
        except Exception as e:
            mod_errors.append(f"log check error: {e}")
        
        return json.dumps({
            "success": True,
            "modName": mod_name,
            "sourcePath": mod_path,
            "targetPath": target_dir,
            "modLoaded": mod_found,
            "errors": mod_errors if mod_errors else None,
        }, ensure_ascii=False, indent=2)

    return 3  # ping + reload_tools + deploy_mod


# ── Dynamic C# tool discovery ─────────────────────────────────────────

def register_dynamic_tools() -> int:
    """Fetch the tool list from the C# mod and register each as an MCP tool.

    MUST be called from the asyncio event loop thread (after `_mod.call_tool`
    succeeds, i.e. after the first heartbeat). Idempotent: tools already
    registered are skipped.

    Returns the number of NEW tools registered (does not count skipped ones).
    """
    if _mod is None or _mcp is None:
        log.warning("register_dynamic_tools: mod/mcp not initialised")
        return 0

    # Ask the C# mod for its full tool registry
    try:
        resp = _mod.call_tool("list_tools", {})
    except Exception as e:
        log.warning(f"register_dynamic_tools: list_tools call failed: {e}")
        return 0

    err = resp.get("error")
    if err:
        log.warning(f"register_dynamic_tools: list_tools returned error: {err}")
        return 0

    result = resp.get("result") or resp
    if not isinstance(result, dict):
        log.warning(f"register_dynamic_tools: unexpected result type: {type(result)}")
        return 0

    csharp_tools = result.get("tools", [])
    if not csharp_tools:
        log.warning("register_dynamic_tools: C# mod returned empty tool list")
        return 0

    return _register_tool_list(csharp_tools)


def register_dynamic_sync(mcp: SimpleMCP, tools_list: list) -> int:
    """Register C# tools from a pre-fetched tool list (e.g. fetched at startup).

    Unlike register_dynamic_tools(), this doesn't call the mod — it uses the
    list provided. Called from main() BEFORE mcp.run() so tools appear in
    the initial tools/list response.
    """
    return _register_tool_list(tools_list)


def _register_tool_list(csharp_tools: list) -> int:
    """Internal helper: register a list of C# tool definitions on _mcp.

    csharp_tools: list of dicts with keys: name, description, inputSchema.

    Must be called from the asyncio event loop thread (or synchronously
    before mcp.run()).
    """
    global _mcp
    if _mcp is None:
        return 0

    tm = getattr(_mcp, "_tool_manager", None)
    if tm is None:
        return 0

    already_registered = set(tm._tools.keys())
    count = 0

    for t in csharp_tools:
        name = t.get("name")
        if not name:
            continue

        # Skip already-registered tools (idempotency)
        if name in already_registered:
            continue

        desc = t.get("description") or ""
        schema = t.get("inputSchema") or {"type": "object"}

        # Build a handler with a real signature derived from the C# inputSchema.
        sig = _build_signature_from_schema(schema)
        handler = _make_handler(name)
        handler.__name__ = name
        handler.__signature__ = sig
        handler.__doc__ = desc or f"C# mod tool: {name}"

        _mcp.add_tool(handler, name=name, description=desc, input_schema=schema)
        count += 1

    if count:
        log.info(f"Registered {count} C# tools (total now: {len(tm._tools)})")
    return count


# ── Dynamic handler construction ─────────────────────────────────────

def _make_handler(tool_name: str):
    """Build a closure-bound async handler for one C# mod tool.

    The function body uses **kwargs so it can receive whatever FastMCP's
    pydantic validation passes through; the visible signature (used by
    FastMCP for schema generation + call-time validation) is supplied
    separately via __signature__ override.
    """
    async def _handler(**kwargs):
        return _forward(tool_name, kwargs)
    return _handler


def _build_signature_from_schema(schema: dict) -> inspect.Signature:
    """Convert a JSON-Schema-style inputSchema (properties/required) into a
    Python inspect.Signature that FastMCP's func_metadata will consume.

    - Required params → POSITIONAL_OR_KEYWORD with no default
    - Optional params → POSITIONAL_OR_KEYWORD with default=None
    - Type annotations are mapped from JSON Schema "type" when possible,
      otherwise Any is used (Pydantic accepts anything).
    """
    properties = schema.get("properties", {}) or {}
    required = set(schema.get("required", []) or [])

    params: list[inspect.Parameter] = []
    for prop_name, prop_schema in properties.items():
        json_type = (prop_schema or {}).get("type")
        annotation = _SCHEMA_TYPE_MAP.get(json_type, Any) if json_type else Any

        if prop_name in required:
            default = inspect.Parameter.empty
        else:
            # JSON-Schema "default" wins if present; otherwise None.
            default = prop_schema.get("default", None) if prop_schema else None

        params.append(inspect.Parameter(
            name=prop_name,
            kind=inspect.Parameter.POSITIONAL_OR_KEYWORD,
            annotation=annotation,
            default=default,
        ))

    return inspect.Signature(params)


def unregister_dynamic_tools() -> int:
    """Remove all dynamically-registered tools, keeping only the core ones
    (e.g. `ping`).

    Useful for re-registration flows (e.g. reload_mod_tools).
    MUST be called from the asyncio event loop thread.

    Returns the number of tools removed.
    """
    if _mcp is None:
        return 0
    tm = getattr(_mcp, "_tool_manager", None)
    if tm is None:
        return 0

    removed = 0
    for name in list(tm._tools.keys()):
        if name in _CORE_TOOL_NAMES:
            continue
        try:
            tm.remove_tool(name)
            removed += 1
        except Exception as e:
            log.debug(f"Failed to remove tool {name}: {e}")

    if removed:
        log.info(f"Unregistered {removed} dynamic tools "
                 f"(kept {len(tm._tools)} core)")
    return removed