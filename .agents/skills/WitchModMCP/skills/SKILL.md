---
name: witch-mod-mcp-index
description: "Index of all WitchModMCP skill modules for browsing available tools and documentation."
---

# WitchModMCP — Module Index

This folder contains detailed documentation for each module. For the gateway architecture, protocol, and decompilation guide, see the parent [SKILL.md](../SKILL.md).

## Tool Modules

| Module | Tools | Description |
|--------|-------|-------------|
| [Core](./core/SKILL.md) | `list_tools`, `list_commands`, `reload_tools`, `eval_command` | Tool discovery, console command execution, hot-reload |
| [Meta](./meta/SKILL.md) | `get_scene_state`, `get_game_data`, `check_mode_saves`, `list_game_modes` | Global state probes — page detection, player snapshot, save inspection |
| [Combat](./combat/SKILL.md) | `get_fight_state`, `play_card`, `end_turn`, `set_card_pile`, `set_fight_entity` | Battle read-write loop — state snapshot, card play, entity modification |
| [Lobby](./lobby/SKILL.md) | `get_lobby_state`, `set_lobby_state` | Career selection hall read & write |
| [Gameflow](./gameflow/SKILL.md) | `enter_game`, `start_new_game`, `start_run`, `load_scene`, `claim_rewards` | Game state machine navigation |
| [Diagnostics](./diagnostics/SKILL.md) | `inspect`, `query_config`, `dump_mod_state`, `get_scene_tree`, `get_recent_logs`, `raycast_mouse`, `set_rng_seed`, `get_screenshot`, `give_item` | Developer backdoor tools |

## Knowledge Base Modules

| Module | Description |
|--------|-------------|
| [Game Insights](./game-insights/SKILL.md) | Game architecture, data structures, C# API patterns, decompiled source reference |
| [Mod Patterns](./mod-patterns/SKILL.md) | Mod structure, CSV formats, hook system, animation pipeline, best practices |

## How to Use These Tools

**Through the gateway (MCP stdio):** You don't need a Python client. Just call `tools/call` with the tool name and parameters. Example:
```json
// tools/call request
{"method": "tools/call", "params": {"name": "get_scene_state", "arguments": {}}}
```

**Python examples in sub-modules** show the direct-access pattern (`g.call(...)`) for reference. The parameters and return values are the same either way.

## Usage Pattern

1. Load the module's `SKILL.md` for parameter documentation and examples.
2. Start with the read tools before using write tools.
3. Follow the module's specific best-practices section.

## Cross-Module Workflows

| Workflow | Steps |
|----------|-------|
| **Full test run** | Meta `get_scene_state` → Gameflow `enter_game` → Gameflow `start_new_game` → Lobby `set_lobby_state` → Gameflow `start_run` → Gameflow `load_scene` → Combat `get_fight_state` → Combat `play_card`/`end_turn` |
| **Debug mod loading** | Core `reload_tools` → Diagnostics `dump_mod_state` → Diagnostics `get_recent_logs` |
| **Investigate config bug** | Diagnostics `query_config` → Diagnostics `inspect` → (optional) decompiled source |
| **Learn about modding** | Patterns `(knowledge)` → Insights `(knowledge)` → build mod → test via tools |
