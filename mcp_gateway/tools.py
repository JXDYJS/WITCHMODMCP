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

    return 16


def register_mutation_tools(mcp: FastMCP, mod: ModConnection,
                             heartbeat: HeartbeatManager) -> int:
    """Register 22 high-risk mutation + flow-control tools with guardrails.

    Each tool description mandates reading a specific resource URI before
    first invocation. The AI client MUST honour these guardrails.

    Returns the number of tools registered.
    """

    # ═══════════════════════════════════════════════════════════════════
    # HIGH — Arbitrary command execution
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def eval_command(command: str) -> str:
        """[GUARDED: READ resource://witchmod/tools/core BEFORE FIRST USE]

        Execute an arbitrary game console command. This is the MOST POWERFUL
        and MOST DANGEROUS tool — you can destroy game state, crash the game,
        or corrupt saves with a single wrong command.

        MANDATORY PRE-CALL CHECKLIST:
        1. FIRST: read_resource("resource://witchmod/tools/core") for full docs.
        2. Call list_commands() to see available commands and their parameters.
        3. Call get_scene_state() to verify the current game page supports this cmd.
        4. Start with read-only commands (help, status) before mutations.
        5. NEVER guess a command name or parameter — verify with list_commands().

        IF YOU HAVE NOT READ THE CORE MODULE DOCS, YOU ARE UNAUTHORIZED.
        """
        return _forward(mod, heartbeat, "eval_command", {"command": command})

    # ═══════════════════════════════════════════════════════════════════
    # HIGH — Item injection
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def give_item(item_type: str, value: str) -> str:
        """[GUARDED: READ resource://witchmod/tools/diagnostics BEFORE FIRST USE]

        Inject an item or resource into the player. Supports 30+ types
        including money, cards, relics, blessings, key items, and more.

        MANDATORY PRE-CALL CHECKLIST:
        1. FIRST: read_resource("resource://witchmod/tools/diagnostics").
        2. Call get_game_data() to see current inventory before injecting.
        3. Call get_scene_state() to confirm injection is safe at current page.
        4. Verify item_type against the diagnostics docs supported-types list.

        IF YOU HAVE NOT READ THE DIAGNOSTICS DOCS, YOU ARE UNAUTHORIZED.
        """
        return _forward(mod, heartbeat, "give_item", {
            "type": item_type, "value": value,
        })

    # ═══════════════════════════════════════════════════════════════════
    # HIGH — Lobby state modification
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def set_lobby_state(career: str | None = None,
                        partner: str | None = None,
                        strength: int | None = None,
                        lucky: int | None = None,
                        perceive: int | None = None,
                        wisdom: int | None = None,
                        card_pack: str | None = None,
                        card_pack_enable: bool | None = None,
                        confirm: bool = False) -> str:
        """[GUARDED: READ resource://witchmod/tools/lobby BEFORE FIRST USE]

        Modify the career selection lobby configuration. Changes take effect
        when you call start_run(), affecting the entire run.

        MANDATORY PRE-CALL CHECKLIST:
        1. FIRST: read_resource("resource://witchmod/tools/lobby").
        2. Call get_lobby_state() to see current config and valid options.
        3. Call get_scene_state() to confirm game is on the LOBBY page.
        4. If confirm=False, this returns a PREVIEW only (read-only safety).
           You MUST review the preview, then call again with confirm=True.
        5. Verify attribute totals are within allowed bounds.

        IF confirm=False AND YOU HAVE NOT REVIEWED THE PREVIEW, STOP.
        IF YOU HAVE NOT READ THE LOBBY DOCS, YOU ARE UNAUTHORIZED.
        """
        args = {}
        if career is not None: args["career"] = career
        if partner is not None: args["partner"] = partner
        if strength is not None: args["strength"] = strength
        if lucky is not None: args["lucky"] = lucky
        if perceive is not None: args["perceive"] = perceive
        if wisdom is not None: args["wisdom"] = wisdom
        if card_pack is not None: args["card_pack"] = card_pack
        if card_pack_enable is not None: args["card_pack_enable"] = card_pack_enable
        args["confirm"] = confirm
        return _forward(mod, heartbeat, "set_lobby_state", args)

    # ═══════════════════════════════════════════════════════════════════
    # HIGH — Combat entity mutation
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def set_fight_entity(entity_type: str,
                         hp: int | None = None,
                         max_hp: int | None = None,
                         shield: int | None = None,
                         power: int | None = None,
                         buff_id: int | None = None,
                         buff_stack: int | None = None) -> str:
        """[GUARDED: READ resource://witchmod/tools/combat BEFORE FIRST USE]

        DIRECTLY modify combat entity attributes (player or enemy). Can
        trivialize fights, break combat scripts, or cause undefined behaviour.

        MANDATORY PRE-CALL CHECKLIST:
        1. FIRST: read_resource("resource://witchmod/tools/combat").
        2. Call get_fight_state() to see current entity stats before modifying.
        3. Call get_scene_state() to confirm game is in FIGHT page.
        4. Use sparingly — prefer play_card() for legitimate combat actions.
        5. Document every modification so you can reverse it.

        IF YOU HAVE NOT READ THE COMBAT DOCS, YOU ARE UNAUTHORIZED.
        """
        args = {"entity_type": entity_type}
        if hp is not None: args["hp"] = hp
        if max_hp is not None: args["max_hp"] = max_hp
        if shield is not None: args["shield"] = shield
        if power is not None: args["power"] = power
        if buff_id is not None: args["buff_id"] = buff_id
        if buff_stack is not None: args["buff_stack"] = buff_stack
        return _forward(mod, heartbeat, "set_fight_entity", args)

    # ═══════════════════════════════════════════════════════════════════
    # HIGH — Card pile manipulation
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def set_card_pile(pile: str, card_id: int, action: str = "add",
                      position: int = 0) -> str:
        """[GUARDED: READ resource://witchmod/tools/combat BEFORE FIRST USE]

        Low-level card pile manipulation: add/remove/move cards between
        hand, draw pile, discard pile, and exhaust pile. Can break card
        game logic if used carelessly.

        MANDATORY PRE-CALL CHECKLIST:
        1. FIRST: read_resource("resource://witchmod/tools/combat").
        2. Call get_fight_state() to see current pile contents.
        3. Verify card_id via query_config("CardConfig") if unsure.
        4. Prefer play_card() for normal plays — use this only for debugging.

        IF YOU HAVE NOT READ THE COMBAT DOCS, YOU ARE UNAUTHORIZED.
        """
        return _forward(mod, heartbeat, "set_card_pile", {
            "pile": pile, "card_id": card_id, "action": action,
            "position": position,
        })

    # ═══════════════════════════════════════════════════════════════════
    # MEDIUM — RNG seed control
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def set_rng_seed(seed: int | None = None,
                     force_state: str | None = None) -> str:
        """[READ resource://witchmod/devtools/diagnostics before using]

        Force-set the RNG seed pool for reproducible testing. Use this
        to reproduce random behaviours consistently across runs.

        Args:
            seed: Fixed RNG seed value. If None, resets to system random.
            force_state: Force a specific random state name.
        """
        args: dict = {}
        if seed is not None: args["seed"] = seed
        if force_state is not None: args["force_state"] = force_state
        return _forward(mod, heartbeat, "set_rng_seed", args)

    # ═══════════════════════════════════════════════════════════════════
    # MEDIUM — DLL hot-reload
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def reload_tools() -> str:
        """[READ resource://witchmod/tools/core before using]

        Hot-reload all MCP tool DLLs without restarting the game. Use after
        recompiling C# mod code. May cause brief stalls or state issues.
        """
        return _forward(mod, heartbeat, "reload_tools")

    # ═══════════════════════════════════════════════════════════════════
    # MEDIUM — Combat flow
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def play_card(card_index: int | None = None,
                  card_id: int | None = None,
                  target_index: int | None = None,
                  choice_index: int = 0) -> str:
        """[READ resource://witchmod/tools/combat before using]

        Play a card from hand during combat. Provide card_index (0-based
        position in hand) or card_id (numeric card ID).

        MANDATORY PRE-CALL CHECKLIST:
        1. FIRST: read_resource("resource://witchmod/tools/combat").
        2. Call get_fight_state() to see hand contents, energy, valid targets.
        3. Call get_scene_state() to confirm FIGHT page.
        4. Verify you have enough energy for the card.
        5. For targeted cards, provide target_index.
        6. For modal-choice cards, provide choice_index.

        IF YOU HAVE NOT READ THE COMBAT DOCS, YOU ARE UNAUTHORIZED.
        """
        args: dict = {"choice_index": choice_index}
        if card_index is not None: args["card_index"] = card_index
        if card_id is not None: args["card_id"] = card_id
        if target_index is not None: args["target_index"] = target_index
        return _forward(mod, heartbeat, "play_card", args)

    @mcp.tool()
    def end_turn() -> str:
        """[READ resource://witchmod/tools/combat before using]

        Force-end the current player turn. The game proceeds to enemy turn
        and resolves all pending effects, buffs, and intents.

        Call get_fight_state() first to understand the current board state.
        """
        return _forward(mod, heartbeat, "end_turn")

    # ═══════════════════════════════════════════════════════════════════
    # MEDIUM — Game flow navigation
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def enter_game() -> str:
        """[READ resource://witchmod/tools/gameflow before using]

        Click "Start Game" on the main menu to enter the HUB (game hut).
        Only works from the MAIN_MENU page.

        Call get_scene_state() first to confirm you are on MAIN_MENU.
        After calling, poll get_scene_state() until page changes to HUB.
        """
        return _forward(mod, heartbeat, "enter_game")

    @mcp.tool()
    def start_new_game(mode: str) -> str:
        """[READ resource://witchmod/tools/gameflow before using]

        Select a game mode and enter the career selection LOBBY.
        Only works from the HUB page.

        Call list_game_modes() first to see available modes.
        Call get_scene_state() to confirm HUB page.
        After calling, poll get_scene_state() until page changes to LOBBY.
        """
        return _forward(mod, heartbeat, "start_new_game", {"mode": mode})

    @mcp.tool()
    def start_run() -> str:
        """[READ resource://witchmod/tools/gameflow before using]

        Click "Embark" (启程) in the career lobby to start a run.
        Only works from the LOBBY page. Lobby configuration must be set
        before calling (use get_lobby_state / set_lobby_state).

        Call get_scene_state() first to confirm LOBBY page.
        After calling, poll get_scene_state() until page changes to MAP.
        """
        return _forward(mod, heartbeat, "start_run")

    @mcp.tool()
    def load_scene(scene_type: str, scene_id: str | None = None) -> str:
        """[GUARDED: READ resource://witchmod/tools/gameflow BEFORE FIRST USE]

        Jump directly to a specific scene (event, fight, or fake fight).
        This BYPASSES normal game flow — use with extreme caution. May
        skip critical run setup, story triggers, or save state.

        MANDATORY PRE-CALL CHECKLIST:
        1. FIRST: read_resource("resource://witchmod/tools/gameflow").
        2. Call get_scene_state() to understand current context.
        3. Understand that jumping mid-run may corrupt the run state.
        4. Prefer enter_game -> start_new_game -> start_run natural flow.

        IF YOU HAVE NOT READ THE GAMEFLOW DOCS, YOU ARE UNAUTHORIZED.
        """
        args = {"scene_type": scene_type}
        if scene_id: args["scene_id"] = scene_id
        return _forward(mod, heartbeat, "load_scene", args)

    @mcp.tool()
    def claim_rewards() -> str:
        """[READ resource://witchmod/tools/gameflow before using]

        Claim battle rewards and close the rewards UI after a fight.
        Only works on the post-battle rewards screen.

        Call get_scene_state() first to confirm you are in the rewards flow.
        """
        return _forward(mod, heartbeat, "claim_rewards")

    # ═══════════════════════════════════════════════════════════════════
    # Map navigation
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def map_list_nodes() -> str:
        """[READ resource://witchmod/tools/gameflow before using]

        List all reachable nodes on the current map. Returns node types,
        icons, and paths. Call BEFORE map_choose_node() to see options.

        Call get_scene_state() first to confirm MAP page.
        """
        return _forward(mod, heartbeat, "map_list_nodes")

    @mcp.tool()
    def map_choose_node(node_index: int) -> str:
        """[READ resource://witchmod/tools/gameflow before using]

        Select and travel to a map node by its index (from map_list_nodes()).
        This advances the run — the choice is permanent for this run.

        Call map_list_nodes() first to see available nodes and their indices.
        Call get_scene_state() to confirm MAP page.
        """
        return _forward(mod, heartbeat, "map_choose_node", {
            "node_index": node_index,
        })

    # ═══════════════════════════════════════════════════════════════════
    # Event interaction
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def event_advance_dialogue() -> str:
        """Advance the current event dialogue text.

        Call get_scene_state() first to confirm EVENT page.
        """
        return _forward(mod, heartbeat, "event_advance_dialogue")

    @mcp.tool()
    def event_choose_option(option_index: int) -> str:
        """[READ resource://witchmod/tools/gameflow before using]

        Select an option in an event UI by 0-based index. Event choices
        can have permanent run consequences (gain/lose items, HP change).

        Before calling, read the event text via get_scene_state or screenshot.
        """
        return _forward(mod, heartbeat, "event_choose_option", {
            "option_index": option_index,
        })

    # ═══════════════════════════════════════════════════════════════════
    # Reward selection
    # ═══════════════════════════════════════════════════════════════════

    @mcp.tool()
    def pick_card_reward(card_index: int = 0) -> str:
        """Select a card from the card reward UI by index (0-based).

        Call get_fight_state() or get_scene_state() first to confirm the
        reward UI is active. Skipping a reward is irreversible.
        """
        return _forward(mod, heartbeat, "pick_card_reward", {
            "card_index": card_index,
        })

    @mcp.tool()
    def skip_card_reward() -> str:
        """Skip the current card reward selection. IRREVERSIBLE.

        Once skipped, you cannot get this card reward back for this run.
        """
        return _forward(mod, heartbeat, "skip_card_reward")

    @mcp.tool()
    def pick_blessing_reward(blessing_index: int = 0) -> str:
        """Select a blessing from the blessing choice UI by index (0-based).

        Call get_scene_state() first to confirm the blessing UI is active.
        Blessings provide permanent run bonuses — choose carefully.
        """
        return _forward(mod, heartbeat, "pick_blessing_reward", {
            "blessing_index": blessing_index,
        })

    @mcp.tool()
    def skip_blessing_reward() -> str:
        """Skip the current blessing reward selection. IRREVERSIBLE.

        Once skipped, you cannot get this blessing for the current run.
        """
        return _forward(mod, heartbeat, "skip_blessing_reward")

    return 22
