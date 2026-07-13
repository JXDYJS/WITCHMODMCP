---
name: witch-mod-mcp
description: "Unified Witch Mod Development skill: inspect and drive a running game instance through the WitchModMCP gateway to develop, debug, and verify mods for the game Witch (女巫/魔法少女 roguelike deckbuilder). Use when a mod developer wants to read live game state, reflect over C# objects, query config tables, dump scene/mod state, tail logs, or trigger console commands / items / scenes. Triggers: WitchModMCP, Witch mod dev, MCPPort, get_game_data, eval_command, query_config, inspect, dump_mod_state, reload_tools, 女巫 mod 开发, 调试 mod, 游戏状态, play_card, end_turn, load_scene, give_item, get_fight_state, set_lobby_state, raycast_mouse, set_rng_seed, decompile_source, 截图, 反编译."
---

# WitchModMCP — Unified Mod Development Skill

Talk to a running instance of the game **Witch** to develop, debug, and verify mods.

## Architecture

```
AI (opencode)
  │  stdin/stdout (MCP JSON-RPC)
  ▼
mcp_gateway/server.py                ← MCP stdio server
  │  - proxies tools/call → HTTP
  │  - handles auth (Bearer token)
  │  - background heartbeat
  │  - auto-syncs skill docs + decompile source on first heartbeat
  │  - normalises PascalCase → camelCase
  ▼
WitchModMCP Mod (in Unity game)
  │  HTTP server on port MCPPort (default 3100)
  │  JSON-RPC 2.0, returns PascalCase via Newtonsoft
```

**The AI does NOT connect directly to port 3100.** The gateway handles all communication. Use `tools/list` and `tools/call` via standard MCP stdio.

## Skill Structure

This unified skill contains all tools from both WitchModMCP and DeveloperTools:

| Sub-skill | Contents |
|-----------|----------|
| [Base Tools](./base/SKILL.md) | Core, Meta, Combat, Lobby, Gameflow, Diagnostics — all MCP tools |
| [DeveloperTools](./devtools/SKILL.md) | Enhanced combat/gameflow/diagnostics tools (screenshot, raycast, decompile) |
| [Game Insights](./insights/SKILL.md) | Architecture knowledge: singletons, config system, hook system, automation API |
| [Mod Patterns](./patterns/SKILL.md) | Writing mods: CSV formats, Lua API, C# templates, walkthrough, troubleshooting |

## Core Rules

1. **`tools/list` is the source of truth.** All tools merged at runtime.
2. **Read before you write.** Prefer read-only tools first.
3. **Every call blocks the game's main thread** — keep parameters tight.
4. **Check `get_scene_state` first.** Tools are page-dependent.

## Quick Intent → Module Mapping

| Intent | Sub-skill | Tool |
|--------|-----------|------|
| "What page/state?" | Base | `get_scene_state` |
| "Player HP/money/deck?" | Base | `get_game_data` |
| "List console commands" | Base | `list_commands` → `eval_command` |
| "Give gold / card / relic" | Base | `give_item` |
| "Jump to boss fight" | Base | `load_scene` |
| "Show card config #123" | Base | `query_config` |
| "Reflect C# object" | Base | `inspect` |
| "Play card X at enemy Y" | Base | `play_card` |
| "Screenshot / raycast / RNG seed" | DevTools | `get_screenshot`, `raycast_mouse`, `set_rng_seed` |
| "Decompile game source" | DevTools | `decompile_source` |
| "How does CardConfig work?" | Insights | (knowledge base) |
| "How to write Entry.lua?" | Patterns | (knowledge base) |

## Mod Development Workflow

```
1. Study → read insights/ + patterns/
2. Code  → write CSV + Lua (or C# DLL)
3. Deploy→ copy to Mods/ folder, enable in-game
4. Test  → load_scene fakefight → play_card → verify
5. Debug → get_recent_logs → inspect → query_config
6. Publish → WorkshopUploader.exe
```
