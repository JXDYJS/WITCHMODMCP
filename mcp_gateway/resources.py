#!/usr/bin/env python3
"""
resources — Expose skill documentation as MCP Resources.

Each resource maps to a physical .md file under the mod's skill tree.
Files are read FROM DISK on every request — modify them live without
restarting the server.

Usage:
    from mcp_gateway.resources import register_resources
    register_resources(mcp, workspace_dir)
"""

import os
from pathlib import Path
from mcp.server.fastmcp import FastMCP

try:
    from mcp.types import Resource
    _HAS_MCP_TYPES = True
except ImportError:
    _HAS_MCP_TYPES = False


# ── File reader ──────────────────────────────────────────────────────

def _read_file(path: str) -> str:
    """Read a file from disk, returning content or an error message."""
    try:
        with open(path, "r", encoding="utf-8") as f:
            return f.read()
    except FileNotFoundError:
        return (
            f"# Resource Unavailable\n\n"
            f"The skill document at `{path}` was not found.\n"
            f"Make sure the WitchModMCP mod is properly installed."
        )
    except Exception as e:
        return f"# Read Error\n\nFailed to read `{path}`: {e}"


# Skill 文档根目录：相对于 mcp_gateway/ 的位置。
# 这是 MCP 项目自身的部署目录名，不是用户 mod 源码目录。
_SKILL_ROOT = Path(__file__).resolve().parent.parent / "【MOD文件夹】" / "mcp_skills"


def _resolve(domain: str, *parts: str) -> str:
    """Resolve a path relative to the skill root for a given domain."""
    root = _SKILL_ROOT
    if domain == "devtools":
        root = root / "devtools"
    return str(root.joinpath(*parts))


# ── Registration ─────────────────────────────────────────────────────

def register_resources(mcp: FastMCP) -> int:
    """Register all skill documentation resources on the MCP server.

    Each resource handler reads its file from disk on every invocation,
    so live edits to .md files take effect immediately.

    Returns the number of resources registered.
    """

    base = lambda *p: _resolve("base", *p)
    dev  = lambda *p: _resolve("devtools", *p)

    # ── Root index ──────────────────────────────────────────────────

    @mcp.resource(
        "resource://witchmod/index",
        name="WitchModMCP — Root Index",
        description="Architecture overview, module index, tool routing table, "
                    "skill doc sync mechanism. Start here before any tool call.",
    )
    def _res_index() -> str:
        return _read_file(base("SKILL.md"))

    # ── Knowledge base ──────────────────────────────────────────────

    @mcp.resource(
        "resource://witchmod/insights",
        name="Game Architecture Insights",
        description="Game internals: tech stack, singletons, config data system, "
                    "mod loading/dependency, hook system, fight system, animation pipeline.",
    )
    def _res_insights() -> str:
        return _read_file(base("insights", "SKILL.md"))

    @mcp.resource(
        "resource://witchmod/patterns",
        name="Mod Development Patterns",
        description="Complete mod authoring reference: directory structure, "
                    "ModConfig format, CSV data specs, Lua/C# entry templates, "
                    "hook reference, animation specs, code patterns, "
                    "validation checklist, troubleshooting.",
    )
    def _res_patterns() -> str:
        return _read_file(base("patterns", "SKILL.md"))

    # ── Tool modules: base ──────────────────────────────────────────

    @mcp.resource(
        "resource://witchmod/tools/index",
        name="Base Tool Modules Index",
        description="Overview of all 6 base tool modules (Core, Meta, Combat, "
                    "Lobby, Gameflow, Diagnostics) with cross-module workflows.",
    )
    def _res_tools_index() -> str:
        return _read_file(base("base", "SKILL.md"))

    @mcp.resource(
        "resource://witchmod/tools/core",
        name="Core Module — list_tools, list_commands, reload_tools, eval_command",
        description="Tool registry, console command discovery, DLL hot-reload, "
                    "arbitrary command execution. MUST READ before calling eval_command.",
    )
    def _res_core() -> str:
        return _read_file(base("base", "core", "SKILL.md"))

    @mcp.resource(
        "resource://witchmod/tools/meta",
        name="Meta Module — get_scene_state, get_game_data, check_mode_saves, list_game_modes",
        description="First-step orientation tools: page detection, player snapshot, "
                    "save inspection, game mode listing. Read before any game-state query.",
    )
    def _res_meta() -> str:
        return _read_file(base("base", "meta", "SKILL.md"))

    @mcp.resource(
        "resource://witchmod/tools/combat",
        name="Combat Module — get_fight_state, play_card, end_turn, set_card_pile, set_fight_entity",
        description="Battle state snapshot, card play with target/modal selection, "
                    "turn control, card pile manipulation, entity attribute modification. "
                    "MUST READ before calling play_card or set_fight_entity.",
    )
    def _res_combat() -> str:
        return _read_file(base("base", "combat", "SKILL.md"))

    @mcp.resource(
        "resource://witchmod/tools/gameflow",
        name="Gameflow Module — enter_game, start_new_game, start_run, load_scene, claim_rewards",
        description="Navigate the game state machine: main menu → hub → lobby → map → fight. "
                    "MUST READ before calling load_scene or start_run.",
    )
    def _res_gameflow() -> str:
        return _read_file(base("base", "gameflow", "SKILL.md"))

    @mcp.resource(
        "resource://witchmod/tools/lobby",
        name="Lobby Module — get_lobby_state, set_lobby_state",
        description="Career selection hall: read/modify career, partner, attributes, "
                    "card packs. MUST READ before calling set_lobby_state.",
    )
    def _res_lobby() -> str:
        return _read_file(base("base", "lobby", "SKILL.md"))

    @mcp.resource(
        "resource://witchmod/tools/diagnostics",
        name="Diagnostics Module — inspect, query_config, dump_mod_state, scene_tree, logs, raycast, screenshot, give_item",
        description="C# reflection, config table queries, mod state dump, scene hierarchy, "
                    "log capture, mouse raycasting, screenshot capture, item injection. "
                    "MUST READ before calling give_item or inspect.",
    )
    def _res_diagnostics() -> str:
        return _read_file(base("base", "diagnostics", "SKILL.md"))

    # ── Tool modules: DeveloperTools ─────────────────────────────────

    @mcp.resource(
        "resource://witchmod/devtools",
        name="DeveloperTools — Overview",
        description="Advanced dev tools: decompile_source, raycast_mouse, get_screenshot, "
                    "plus enhanced combat/gameflow/lobby/diagnostics tool docs.",
    )
    def _res_devtools() -> str:
        return _read_file(dev("SKILL.md"))

    @mcp.resource(
        "resource://witchmod/devtools/combat",
        name="DeveloperTools Combat — extended, includes claim_rewards",
        description="Extended combat module docs (Chinese): fight state, card play, "
                    "turn end, pile control, entity mod, AND claim_rewards. "
                    "MUST READ before calling play_card or set_fight_entity.",
    )
    def _res_devtools_combat() -> str:
        return _read_file(dev("skills", "combat", "SKILL.md"))

    @mcp.resource(
        "resource://witchmod/devtools/gameflow",
        name="DeveloperTools Gameflow — extended, with state machine diagram",
        description="Extended gameflow docs (Chinese): page detection, navigation, "
                    "full workflow scripts, state machine diagram.",
    )
    def _res_devtools_gameflow() -> str:
        return _read_file(dev("skills", "gameflow", "SKILL.md"))

    @mcp.resource(
        "resource://witchmod/devtools/lobby",
        name="DeveloperTools Lobby — extended, with configure-and-start workflow",
        description="Extended lobby docs (Chinese): career selection, partner, "
                    "attributes, card packs, quick-configure-and-start workflow.",
    )
    def _res_devtools_lobby() -> str:
        return _read_file(dev("skills", "lobby", "SKILL.md"))

    @mcp.resource(
        "resource://witchmod/devtools/diagnostics",
        name="DeveloperTools Diagnostics — screenshot, raycast, RNG seed, decompile_source",
        description="DevTools-specific diagnostic tools: get_screenshot, raycast_mouse, "
                    "set_rng_seed, decompile_source. Includes cross-reference table.",
    )
    def _res_devtools_diagnostics() -> str:
        return _read_file(dev("skills", "diagnostics", "SKILL.md"))

    return 15  # total resource count
