#!/usr/bin/env python3
"""
tools — MCP Tool definitions for WitchModMCP.

Stage 3: Low-risk read-only + diagnostic output tools (16 tools).
Stage 4: High-risk mutation + flow-control tools with guardrails.

All tools forward to the game mod's JSON-RPC server via ModConnection.
Parameter keys are translated from Pythonic snake_case to C# PascalCase.
"""

import json
from mcp.server.fastmcp import FastMCP
from mcp_gateway.mod_client import ModConnection
from mcp_gateway.heartbeat import HeartbeatManager


def _to_pascal(d: dict) -> dict:
    """Convert snake_case dict keys to PascalCase for the C# Newtonsoft-backed mod.

    Examples:
        root_name → RootName
        max_depth → MaxDepth
        type_name → TypeName
    """
    result = {}
    for k, v in d.items():
        parts = k.split("_")
        pascal_key = "".join(p[0].upper() + p[1:] for p in parts if p)
        result[pascal_key] = _to_pascal(v) if isinstance(v, dict) else v
    return result


def _forward(mod: ModConnection, heartbeat: HeartbeatManager,
             tool_name: str, arguments: dict | None = None) -> str:
    """Forward a tool call to the game mod, with connection check.

    Returns a JSON string suitable for MCP text content.
    """
    if not heartbeat.connected:
        return json.dumps({
            "error": "Game mod is not reachable. Start the game with WitchModMCP loaded.",
            "hint": "Heartbeat has not yet connected. Wait for the mod to finish loading."
        }, ensure_ascii=False)

    pascal_args = _to_pascal(arguments) if arguments else None
    resp = mod.call_tool(tool_name, pascal_args)

    err = resp.get("error")
    if err:
        return json.dumps(err, ensure_ascii=False)

    return json.dumps(resp.get("result", resp), ensure_ascii=False, indent=2)


def register_readonly_tools(mcp: FastMCP, mod: ModConnection,
                             heartbeat: HeartbeatManager) -> int:
    """Register all 16 low-risk tools (read-only diagnostics + diagnostic output).

    Returns the number of tools registered.
    """

    # ═══════════════════════════════════════════════════════════════════
    # Core discovery tools
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def list_tools() -> str:
        """Return the full MCP tool registry from the game mod.

        Lists every tool with name, description, and inputSchema.
        Use this to discover available tools when you are unsure what
        operations are possible.

        Read resource://witchmod/tools/core for module overview.
        """
        return _forward(mod, heartbeat, "list_tools")

    @mcp.tool()
    def list_commands() -> str:
        """Return all available game console commands with parameter signatures.

        Lists every registered console debug command (/give, /heal, etc.)
        with parameter names, types, and descriptions.

        Read resource://witchmod/tools/core for eval_command usage patterns.
        """
        return _forward(mod, heartbeat, "list_commands")

    # ═══════════════════════════════════════════════════════════════════
    # Meta / state snapshot tools
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def get_scene_state() -> str:
        """Detect the current game page/state with modal and overlay details.

        Returns the active page (MAIN_MENU / HUB / LOBBY / MAP / FIGHT / EVENT /
        SHOP / BLESS / STORY / UNKNOWN), plus any active modals, transitions,
        or overlays that may block interaction.

        ALWAYS call this first before any gameflow tool (load_scene, start_run, etc).
        Read resource://witchmod/tools/meta for full documentation.
        """
        return _forward(mod, heartbeat, "get_scene_state")

    @mcp.tool()
    def get_game_data() -> str:
        """Return a compact snapshot of current game state.

        Includes:
          - player: HP, SAN, money, card/relic/bless counts
          - fight: inFight flag, power, shield, fightType
          - runtime: level, time, truth, exp

        Safe to call at any time. Use as the first diagnostic step.
        Read resource://witchmod/tools/meta for full documentation.
        """
        return _forward(mod, heartbeat, "get_game_data")

    @mcp.tool()
    def check_mode_saves(mode: str | None = None) -> str:
        """Inspect save files for a game mode.

        Args:
            mode: Game mode name filter (e.g. "Standard"). If omitted, shows all.

        Read resource://witchmod/tools/meta for usage patterns.
        """
        args = {"mode": mode} if mode else {}
        return _forward(mod, heartbeat, "check_mode_saves", args)

    @mcp.tool()
    def list_game_modes() -> str:
        """List all available game modes and their save status.

        Includes both built-in and mod-registered modes.
        Use this when selecting a mode for start_new_game.

        Read resource://witchmod/tools/meta for full documentation.
        """
        return _forward(mod, heartbeat, "list_game_modes")

    # ═══════════════════════════════════════════════════════════════════
    # Combat read-only tools
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def get_fight_state() -> str:
        """Return full battle state snapshot.

        Includes: player/enemy HP, shield, buffs, intents, hand cards,
        draw pile, discard pile, exhaust pile, current energy.

        Safe read-only tool. Use before deciding combat actions.
        Read resource://witchmod/tools/combat for full parameter reference.
        """
        return _forward(mod, heartbeat, "get_fight_state")

    # ═══════════════════════════════════════════════════════════════════
    # Lobby read-only tool
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def get_lobby_state() -> str:
        """Return current career selection lobby configuration.

        Shows: selected career, partner, allocated attributes (Strength/Lucky/
        Perceive/Wisdom), active card packs, and available options.

        Read resource://witchmod/tools/lobby for full parameter reference.
        """
        return _forward(mod, heartbeat, "get_lobby_state")

    # ═══════════════════════════════════════════════════════════════════
    # Introspection / reflection tools
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def inspect(type_name: str, member_path: str | None = None,
                max_depth: int = 3, max_items: int = 20) -> str:
        """Reflect over C# object fields/properties via runtime reflection.

        Args:
            type_name: Full C# type name (e.g. "RoleTable", "FightManager").
            member_path: Optional dot-separated member path (e.g. "Instance.San").
            max_depth: Max nesting depth for object traversal (default 3).
            max_items: Max items per collection level (default 20).

        Use to explore the game's internal state and discover API surface.
        Read resource://witchmod/tools/diagnostics for full parameter reference.
        """
        return _forward(mod, heartbeat, "inspect", {
            "type_name": type_name,
            "member_path": member_path,
            "max_depth": max_depth,
            "max_items": max_items,
        })

    @mcp.tool()
    def query_config(table_name: str | None = None,
                     item_id: int | None = None,
                     limit: int = 5) -> str:
        """Query game config tables (CardConfig, RelicConfig, BuffConfig, etc).

        Args:
            table_name: Table name (e.g. "CardConfig"). If omitted, lists all tables.
            item_id: Specific item ID to look up.
            limit: Max sample rows when browsing (default 5).

        Read resource://witchmod/tools/diagnostics for full parameter reference.
        """
        args = {"limit": limit}
        if table_name:
            args["table_name"] = table_name
        if item_id is not None:
            args["item_id"] = item_id
        return _forward(mod, heartbeat, "query_config", args)

    @mcp.tool()
    def dump_mod_state() -> str:
        """List all currently loaded mods with assembly version info.

        Shows: mod name, assembly version, plus related assemblies.
        Use to verify which mods are active and check load order issues.

        Read resource://witchmod/tools/diagnostics for full parameter reference.
        """
        return _forward(mod, heartbeat, "dump_mod_state")

    @mcp.tool()
    def get_recent_logs(count: int = 50) -> str:
        """Return the most recent N log entries from the in-memory ring buffer.

        Args:
            count: Number of recent log lines (default 50).

        Captures Unity Application.logMessageReceived output.
        Read resource://witchmod/tools/diagnostics for full parameter reference.
        """
        return _forward(mod, heartbeat, "get_recent_logs", {"count": count})

    @mcp.tool()
    def get_scene_tree(root_name: str | None = None,
                       max_depth: int = 10,
                       max_children: int = 50,
                       include_components: bool = True,
                       include_inactive: bool = False) -> str:
        """Walk the Unity scene GameObject hierarchy.

        Args:
            root_name: Filter to specific root GameObject name (e.g. "Main Camera").
            max_depth: Max tree depth (default 10).
            max_children: Max children per node (default 50).
            include_components: Include component type names (default True).
            include_inactive: Include disabled GameObjects (default False).

        Read resource://witchmod/tools/diagnostics for full parameter reference.
        """
        return _forward(mod, heartbeat, "get_scene_tree", {
            "root_name": root_name,
            "max_depth": max_depth,
            "max_children": max_children,
            "include_components": include_components,
            "include_inactive": include_inactive,
        })

    # ═══════════════════════════════════════════════════════════════════
    # Diagnostic output tools (DevTools)
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def get_screenshot(format: str = "png", quality: int = 75) -> str:
        """Capture the current game screen and return base64-encoded image data.

        Args:
            format: "png" or "jpg" (default "png").
            quality: JPEG quality 1-100 (ignored for PNG).

        The result includes: width, height, size (bytes), base64 data.
        Read resource://witchmod/devtools/diagnostics for full parameter reference.
        """
        return _forward(mod, heartbeat, "get_screenshot", {
            "format": format,
            "quality": quality,
        })

    @mcp.tool()
    def raycast_mouse(screen_x: int | None = None,
                      screen_y: int | None = None,
                      max_results: int = 10) -> str:
        """Cast rays from a screen position and report hit GameObjects.

        Performs triple raycast: Canvas GraphicRaycaster, 3D Physics.Raycast,
        and 2D Physics2D.Raycast. Identifies what's under the mouse cursor.

        Args:
            screen_x: Screen X coordinate. If None, uses current mouse position.
            screen_y: Screen Y coordinate. If None, uses current mouse position.
            max_results: Max hits per raycast (default 10).

        Each hit includes: gameObjectName, hierarchyPath, source, isCanvas,
        components, distance, depth, sortingOrder.

        Read resource://witchmod/devtools/diagnostics for full parameter reference.
        """
        args: dict = {"max_results": max_results}
        if screen_x is not None:
            args["screen_x"] = screen_x
        if screen_y is not None:
            args["screen_y"] = screen_y
        return _forward(mod, heartbeat, "raycast_mouse", args)

    @mcp.tool()
    def decompile_source(output_dir: str | None = None) -> str:
        """Decompile Witch.dll and Witch.Core.dll to a local directory.

        THIS TOOL EXPORTS CODE TO DISK, NOT TO MEMORY. It writes decompiled C#
        source files to the specified output directory. Use your AI client's
        local file search/RAG capabilities to analyze the exported code.

        Args:
            output_dir: Target directory. Defaults to workspace/.cache/game_src/
                        (or the MCP_DECOMPILE_DIR environment variable).

        The tool caches results by hash — subsequent calls skip if unchanged.
        Read resource://witchmod/devtools/diagnostics for full parameter reference.
        """
        args = {}
        if output_dir:
            args["output_dir"] = output_dir
        return _forward(mod, heartbeat, "decompile_source", args)

    return 16  # total tool count
