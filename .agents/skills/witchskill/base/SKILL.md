---
name: witch-mod-mcp-base
description: "WitchModMCP base tools: all MCP tools for mod development — discovery, game state, combat, lobby, gameflow, diagnostics."
---

# Base Tools — Module Index

| Module | Tools | Description |
|--------|-------|-------------|
| [Core](./core/SKILL.md) | `list_tools`, `list_commands`, `reload_tools`, `eval_command` | Discovery, console commands, hot-reload |
| [Meta](./meta/SKILL.md) | `get_scene_state`, `get_game_data`, `check_mode_saves`, `list_game_modes` | Page detection, player snapshot, saves |
| [Combat](./combat/SKILL.md) | `get_fight_state`, `play_card`, `end_turn`, `set_card_pile`, `set_fight_entity` | Battle read-write loop |
| [Lobby](./lobby/SKILL.md) | `get_lobby_state`, `set_lobby_state` | Career selection hall |
| [Gameflow](./gameflow/SKILL.md) | `enter_game`, `start_new_game`, `start_run`, `load_scene`, `claim_rewards` | State machine navigation |
| [Diagnostics](./diagnostics/SKILL.md) | `inspect`, `query_config`, `dump_mod_state`, `get_scene_tree`, `get_recent_logs`, `raycast_mouse`, `set_rng_seed`, `get_screenshot`, `give_item` | Developer backdoor tools |

## Usage

Call tools via MCP stdio through the gateway:
```json
{"method": "tools/call", "params": {"name": "<tool_name>", "arguments": {...}}}
```
