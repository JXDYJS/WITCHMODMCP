---
name: witch-mod-mcp
description: "Mod development aid for the game Witch (女巫/魔法少女 roguelike deckbuilder): inspect and drive a running game instance through the WitchModMCP gateway to develop, debug, and verify mods. Use when a mod developer wants to read live game state (player HP/SAN/money/deck, fight status, run progress), reflect over C# objects, query config tables, dump the scene tree or loaded-mod state, tail logs, or trigger console commands / items / scenes to reproduce and test mod behavior. Not a cheat/trainer tool for regular players. Triggers: WitchModMCP, Witch mod dev, MCPPort, get_game_data, eval_command, query_config, inspect RoleTable, dump_mod_state, reload_tools, 女巫 mod 开发, 调试 mod, 游戏状态, play_card, end_turn, load_scene, give_item, get_fight_state, set_lobby_state, raycast_mouse, set_rng_seed."
---

# WitchModMCP — Mod Development Gateway

Talk to a running instance of the game **Witch** to develop, debug, and verify mods. The mod embeds an HTTP server inside the game process; a Python **gateway** proxies MCP stdio ↔ the mod's HTTP. This SKILL.md describes the architecture and how the AI interacts with the game.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  AI (opencode)                                              │
│  Connects via stdio MCP to the gateway server               │
│  All tool calls go through: tools/list → tools/call         │
└──────────────────────┬──────────────────────────────────────┘
                       │ stdin/stdout (MCP JSON-RPC)
┌──────────────────────▼──────────────────────────────────────┐
│  mcp_gateway/server.py (Gateway Server)                     │
│                                                             │
│  - Receives MCP requests from AI via stdio                  │
│  - Proxies tools/call → POST to game mod HTTP server        │
│  - Handles auth (Bearer token) automatically                │
│  - Background heartbeat thread monitors connection          │
│  - On first heartbeat: syncs skill docs + decompile source  │
│  - Normalises PascalCase → camelCase in responses           │
└──────────────────────┬──────────────────────────────────────┘
                       │ HTTP (localhost:3100)
┌──────────────────────▼──────────────────────────────────────┐
│  WitchModMCP Mod (in Unity game process)                    │
│                                                             │
│  - HTTP server on port MCPPort (default 3100)               │
│  - JSON-RPC 2.0 style request/response                     │
│  - Executes game logic on Unity main thread                 │
│  - Returns PascalCase JSON via Newtonsoft                   │
└─────────────────────────────────────────────────────────────┘
```

### How AI should use this

**The AI does NOT connect directly to port 3100.** The gateway server is already running and connected. The AI accesses tools through standard MCP protocol:

- `tools/list` — discover all available tools from all loaded mods
- `tools/call` — invoke any tool with parameters
- `resources/read` — read connection status, tool list

The gateway handles authentication, key normalization (PascalCase → camelCase), and error wrapping.

### Python direct access (fallback)

For scripts that bypass the gateway (e.g. automated test suites), use `E:\Witch\WitchModMCP\.agents\skills\WitchModMCP\scripts\witch_mcp.py`:

```python
import sys
sys.path.insert(0, "E:/Witch/WitchModMCP/.agents/skills/WitchModMCP/scripts")
from witch_mcp import WitchMcp

g = WitchMcp(port=3100)
print(g.list_tools())
```

## Core Rules

1. **`tools/list` is the source of truth.** Always call it first to see what tools are actually registered (can be hot-added via `reload_tools`).
2. **Read before you write.** Prefer read-only tools to understand state. Mutation tools change live game state — only call them when the user clearly wants a change.
3. **Every call blocks the game's main thread briefly.** Keep parameters tight (`limit`, `maxDepth`, `maxItems`, `maxChildren`) to avoid huge payloads.
4. **Check `get_scene_state` first.** The game has distinct pages (MAIN_MENU, LOBBY, FIGHT, MAP, etc); tools are only valid in specific pages.

## Module Index

WitchModMCP tools are organized into domain modules:

| Module | Tools | Triggers |
|--------|-------|---------|
| [Core](./skills/core/SKILL.md) | `list_tools`, `list_commands`, `reload_tools`, `eval_command` | discovery, console command |
| [Meta](./skills/meta/SKILL.md) | `get_scene_state`, `get_game_data`, `check_mode_saves`, `list_game_modes` | scene state, game data, 场景检测 |
| [Combat](./skills/combat/SKILL.md) | `get_fight_state`, `play_card`, `end_turn`, `set_card_pile`, `set_fight_entity` | 战斗, 出牌, play_card, combat |
| [Lobby](./skills/lobby/SKILL.md) | `get_lobby_state`, `set_lobby_state` | 大厅, 职业, career, lobby |
| [Gameflow](./skills/gameflow/SKILL.md) | `enter_game`, `start_new_game`, `start_run`, `load_scene`, `claim_rewards` | 启程, 开始游戏, gameflow |
| [Diagnostics](./skills/diagnostics/SKILL.md) | `inspect`, `query_config`, `dump_mod_state`, `get_scene_tree`, `get_recent_logs`, `raycast_mouse`, `set_rng_seed`, `get_screenshot`, `give_item` | 调试, 反射, 查配置, debug |
| [Game Insights](./skills/game-insights/SKILL.md) | — | Knowledge base: game architecture, data structures, C# API patterns |
| [Mod Patterns](./skills/mod-patterns/SKILL.md) | — | Knowledge base: mod structure, CSV formats, hooks, best practices |

Open [skills/SKILL.md](./skills/SKILL.md) for the full module index.

## DeveloperTools Extension

[DeveloperTools](../developer-tools/SKILL.md) is an optional extension mod. It adds enhanced versions of several tools plus exclusive tools like `get_screenshot`, `raycast_mouse`, `decompile_source`. Tools from both mods are merged — `tools/list` shows everything.

## Skill Sync

The gateway automatically syncs these skill docs (from `mcp_skills/` in each mod's install directory) to the workspace's `.agents/skills/` on first successful heartbeat. No manual copy needed.

## Game Source Decompilation

> **RULE**: Before reading ANY decompiled game source, you MUST call `decompile_source` first.

The `decompile_source` tool (from DeveloperTools) uses ICSharpCode.Decompiler to produce C# source from `Witch.dll` and `Witch.Core.dll`. Cached by DLL hash in `{outputDir}/{hash}/`.

```
{outputDir}/
├── .decompile_manifest.json
├── 8d876.../              ← Witch.dll current hash
│   └── Witch.*.cs ...
└── ca6e9.../              ← Witch.Core.dll current hash
    └── Witch.Core.*.cs ...
```

**Workflow:**
1. Call `decompile_source` with `{outputDir: "<workspace>/.cache/game_src"}`
2. Status `"fresh"` → cache valid; `"decompiled"` → just rebuilt
3. Read .cs files from the hash-named directories

**NEVER** read from an arbitrary path without calling `decompile_source` first.

## Common Intents → Module Routing

| Intent | Module | Tool |
|--------|--------|------|
| "What page/state is the game in?" | Meta | `get_scene_state` |
| "What are the player's HP/money/deck?" | Meta | `get_game_data` |
| "What console commands exist?" | Core | `list_commands` → `eval_command` |
| "I need gold / a relic / a card" | Diagnostics | `give_item` |
| "Take me to a boss fight" | Gameflow | `load_scene` |
| "Show card config #123" | Diagnostics | `query_config` |
| "Read RoleTable.Instance.San" | Diagnostics | `inspect` |
| "Which mods are loaded?" | Diagnostics | `dump_mod_state` |
| "Show recent game logs" | Diagnostics | `get_recent_logs` |
| "Play card X at enemy Y" | Combat | `play_card` |
| "End my turn" | Combat | `end_turn` |
| "Set up a lobby with career X / pack Y" | Lobby | `set_lobby_state` |
| "Start a new run" | Gameflow | `start_new_game` → `set_lobby_state` → `start_run` |
| "How does CardConfig work?" | Insights | (knowledge base) |
| "How do I write a mod Entry.lua?" | Patterns | (knowledge base) |
