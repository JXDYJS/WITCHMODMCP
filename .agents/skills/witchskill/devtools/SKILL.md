---
name: witch-mod-mcp-devtools
description: "DeveloperTools extension: enhanced combat/gameflow/diagnostics tools — screenshot, raycast, RNG control, decompile source."
---

# DeveloperTools

DeveloperTools is an extension mod that provides enhanced implementations and exclusive tools. All DevTools are also documented in the relevant Base module files, listed here for quick reference.

## Tools Provided

| Tool | Category | Source Mod | Also Documented In |
|------|----------|------------|-------------------|
| `get_screenshot` | Diagnostics | DeveloperTools | `base/diagnostics` |
| `raycast_mouse` | Diagnostics | DeveloperTools | `base/diagnostics` |
| `set_rng_seed` | Diagnostics | DeveloperTools | `base/diagnostics` |
| `decompile_source` | Diagnostics | DeveloperTools | `base/diagnostics` |
| Enhanced `get_fight_state` | Combat | DeveloperTools | `base/combat` |
| Enhanced `play_card` | Combat | DeveloperTools | `base/combat` |
| `set_card_pile` | Combat | DeveloperTools | `base/combat` |
| `set_fight_entity` | Combat | DeveloperTools | `base/combat` |
| `claim_rewards` | Combat | DeveloperTools | `base/gameflow` |
| Enhanced `enter_game` | Gameflow | DeveloperTools | `base/gameflow` |
| Enhanced `start_new_game` | Gameflow | DeveloperTools | `base/gameflow` |
| `start_run` | Gameflow | DeveloperTools | `base/gameflow` |
| `check_mode_saves` | Gameflow | DeveloperTools | `base/meta` |
| `list_game_modes` | Gameflow | DeveloperTools | `base/meta` |
| `get_lobby_state` | Lobby | DeveloperTools | `base/lobby` |
| `set_lobby_state` | Lobby | DeveloperTools | `base/lobby` |

## Source Decompilation

See [base/diagnostics](../base/diagnostics/SKILL.md) for `decompile_source` usage.

> **RULE**: Before reading ANY decompiled game source, call `decompile_source` first.
