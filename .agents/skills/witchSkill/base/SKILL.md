---
name: witch-mod-mcp-index
description: "Index of all WitchModMCP skill modules for browsing available tools and documentation."
---

# WitchModMCP — Module Index

This folder contains detailed documentation for each module. For transport protocol and decompilation guide, see the parent [SKILL.md](../SKILL.md).

## Modules

| Module | Tools | Description |
|--------|-------|-------------|
| [Core](./core/SKILL.md) | `list_tools`, `list_commands`, `reload_tools`, `eval_command` | Tool discovery, console command execution, hot-reload |
| [Meta](./meta/SKILL.md) | `get_scene_state`, `get_game_data`, `get_game_info`, `check_mode_saves`, `list_game_modes`, `get_recent_logs`, `get_event_state` | Global state probes — page detection, player snapshot, game install info, save inspection, event UI state, log reading |
| [Combat](./combat/SKILL.md) | `get_fight_state`, `play_card`, `use_skill`, `get_skills_state`, `end_turn`, `set_card_pile`, `set_fight_entity`, `get_deck_selection`, `select_deck_cards` | Battle-read/write loop — state snapshot, card play, skill use, entity modification, card selection |
| [Lobby](./lobby/SKILL.md) | `get_lobby_state`, `set_lobby_state` | Career selection hall read & write — career, partner, attributes, card packs |
| [Gameflow](./gameflow/SKILL.md) | `enter_game`, `start_new_game`, `start_run`, `map_select_state`, `map_select_assign`, `map_select_clear`, `map_select_confirm`, `load_scene`, `claim_rewards` | Game state machine navigation — menu → hub → lobby → map → passage → fight |
| [Diagnostics](./diagnostics/SKILL.md) | `inspect`, `query_config`, `search_config`, `dump_mod_state`, `get_scene_tree`, `get_recent_logs`, `raycast_mouse`, `set_rng_seed`, `get_screenshot`, `give_item`, `get_env_info`, `scan_ui`, `click_ui` | Developer backdoor tools — reflection, config queries, scene/UI inspection, item injection, mod skill path discovery |
| [Game Insights](../insights/SKILL.md) | (knowledge base, no tools) | Game architecture, CSV schemas, Lua effect API, mod directory structure — load before creating mod content |

## Usage pattern

Each module is self-contained. When a user's intent matches a module's domain:

1. Load the module's `SKILL.md` for parameter documentation and examples.
2. Start with the read tools in that module before using write tools.
3. Follow the module's specific best-practices section.
4. **For mod creation tasks** (adding cards, buffs, card packs, etc.): Also load `witch-mod-mcp-game-insights` (at `../insights/SKILL.md`) for CSV schemas and Lua effect API documentation. Do NOT probe the game runtime to discover data formats.

## Cross-module workflows

Common multi-step workflows span multiple modules:

| Workflow | Steps |
|----------|-------|
| **Full test run** | Meta `get_scene_state` → Gameflow `enter_game` → Gameflow `start_new_game` → Lobby `set_lobby_state` → Gameflow `start_run` → Gameflow `load_scene` → Combat `get_fight_state` → Combat `play_card`/`end_turn` |
| **Debug mod loading** | Core `reload_tools` → Diagnostics `dump_mod_state` → Diagnostics `get_recent_logs` |
| **Investigate config bug** | Diagnostics `query_config` → Diagnostics `inspect` → (optional) decompiled source |
