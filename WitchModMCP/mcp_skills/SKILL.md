---
name: witch-mod-mcp
description: "Mod development aid for the game Witch (魔女:终末旅途 roguelike deckbuilder): inspect and drive a running game instance through the WitchModMCP in-game HTTP server to develop, debug, and verify mods. Use when a mod developer wants to read live game state (player HP/SAN/money/deck, fight status, run progress), reflect over C# objects, query config tables, dump the scene tree or loaded-mod state, tail logs, or trigger console commands / items / scenes to reproduce and test mod behavior. Not a cheat/trainer tool for regular players. Triggers: WitchModMCP, Witch mod dev, MCPPort, get_game_data, eval_command, query_config, inspect RoleTable, dump_mod_state, reload_tools, 魔女 mod 开发, 调试 mod, 游戏状态, play_card, end_turn, load_scene, give_item, get_fight_state, set_lobby_state, raycast_mouse, set_rng_seed."
---

# WitchModMCP

WitchModMCP is a mod development tool for the game **Witch** (魔女:终末旅途 roguelike deckbuilder). It helps you inspect live game state, test mod behaviour, query config tables, control fights, navigate scenes, and debug issues — all through standard MCP tools.

## Architecture

**DO NOT connect directly to the game's HTTP port.** Communication goes through a gateway:

```
AI (opencode)
  │  stdin/stdout (MCP JSON-RPC)
  ▼
mcp_gateway/server.py                ← MCP stdio server
  │  - proxies tools/call → HTTP
  │  - handles auth (Bearer token)
  │  - background heartbeat
  │  - auto-syncs skill docs + decompile source on first heartbeat
  ▼
WitchModMCP Mod (in Unity game)
  │  HTTP server on port MCPPort (default 3100)
  │  JSON-RPC 2.0, returns PascalCase via Newtonsoft
```

**The AI does NOT send HTTP requests directly to port 3100.** The gateway handles all communication. Use standard MCP `tools/list` and `tools/call` through the configured stdio transport.

## Core rules

1. **`list_tools` is the source of truth.** Always run `tools/list` first to see what is actually registered in this build (tools can be hot-added via `reload_tools`).
2. **Read before you write.** Prefer read-only tools to understand state. Mutation tools change live game state — only call them when the user clearly wants a change.
3. **Every call blocks the game's main thread briefly.** Keep parameters tight (`limit`, `maxDepth`, `maxItems`, `maxChildren`) to avoid huge payloads and frame hitches.
4. **If `tools/list` fails**, the gateway cannot reach the game mod. Check that: (a) the game is running, (b) WitchModMCP mod is loaded and enabled, (c) the MCP port / auth token in `ModConfig.json` match the gateway configuration.

## Module Index

WitchModMCP tools are organized into domain modules. Load the relevant module for detailed documentation:

| Module | Tools | Triggers |
|--------|-------|---------|
| [Core](./base/core/SKILL.md) | `list_tools`, `list_commands`, `reload_tools`, `eval_command` | discovery, console command, eval_command |
| [Meta](./base/meta/SKILL.md) | `get_scene_state`, `get_game_data`, `check_mode_saves`, `list_game_modes` | scene state, game data, 场景检测, 页面状态 |
| [Combat](./base/combat/SKILL.md) | `get_fight_state`, `play_card`, `end_turn`, `set_card_pile`, `set_fight_entity` | 战斗, 出牌, 打牌, combat |
| [Lobby](./base/lobby/SKILL.md) | `get_lobby_state`, `set_lobby_state` | 大厅, 职业, 卡包, career, lobby |
| [Gameflow](./base/gameflow/SKILL.md) | `enter_game`, `start_new_game`, `start_run`, `load_scene`, `claim_rewards` | 启程, 开始游戏, 跳转, gameflow |
| [Diagnostics](./base/diagnostics/SKILL.md) | `inspect`, `query_config`, `dump_mod_state`, `get_scene_tree`, `get_recent_logs`, `raycast_mouse`, `set_rng_seed`, `get_screenshot`, `give_item` | 调试, 反射, 查配置, debug, diagnostics |

For a full module-by-module listing, open [base/SKILL.md](./base/SKILL.md).

### Extension: DeveloperTools

[DeveloperTools](./skills/SKILL.md) is an optional extension mod that adds 18 tools on top of the base WitchModMCP toolset. It provides enhanced/alternative implementations in several domains:

| Domain | Base tools | DeveloperTools additions |
|--------|-----------|------------------------|
| Combat | `get_fight_state`, `play_card`, `end_turn` | +`set_card_pile`, `set_fight_entity`, `claim_rewards` (all also enhanced) |
| Gameflow | `load_scene` | +`enter_game`, `start_new_game`, `start_run`, `check_mode_saves`, `list_game_modes` |
| Lobby | — | +`get_lobby_state`, `set_lobby_state` |
| Diagnostics | `inspect`, `query_config`, `dump_mod_state`, `get_scene_tree`, `get_recent_logs`, `give_item` | +`get_screenshot`, `raycast_mouse`, `set_rng_seed`, `decompile_source` |
| Meta/State | `get_scene_state`, `get_game_data` | +enhanced `get_scene_state` |

**Key relationships:**
- `list_tools` merges both — run it to see everything available
- Where DeveloperTools has an enhanced version (e.g. `get_fight_state`), it replaces the base version at registration time
- `load_scene` (fake fights), `give_item`, `inspect`, `query_config`, `eval_command`, `reload_tools` remain exclusively in the base mod
- The `decompile_source` tool (DeveloperTools) replaces the old Python-based decompile workflow documented below

## Skill documentation sync

Skill `.md` docs live inside each mod's folder under `mcp_skills/`. The gateway auto-syncs docs on first heartbeat — no manual step needed.

## Common intents → module routing

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
| "What GameObjects are in the scene?" | Diagnostics | `get_scene_tree` |
| "Show recent game logs" | Diagnostics | `get_recent_logs` |
| "Play card X at enemy Y" | Combat | `play_card` |
| "End my turn" | Combat | `end_turn` |
| "Set up a lobby with career X / pack Y" | Lobby | `set_lobby_state` |
| "Start a new run" | Gameflow | `start_new_game` → `set_lobby_state` → `start_run` |
| "I recompiled my tool DLL" | Core | `reload_tools` → `list_tools` |

## Game source code decompilation (optional)

> **RULE**: Before reading ANY decompiled game source, you MUST call `decompile_source` first. Failure to do so risks reading stale or missing output.

> This is purely optional — the skill works without it. Use it when you need to inspect the game's own C# logic (e.g. to understand a config field, find a hook point, or debug unexpected behaviour). If the user declines, you may skip the read entirely.

### ═══ SOURCE ACCESS GATE — READ BEFORE ACCESSING ═══

You only need to go through this gate when you **actually need** to read decompiled game source. If you can answer the question with runtime data (`get_game_data`, `inspect`, `query_config`, etc.), skip this entirely.

```
┌─ GATE ────────────────────────────────────────────────────┐
│                                                            │
│  1. ⚠️  ALWAYS call decompile_source first                 │
│     → r = g.call("decompile_source",                       │
│         {"outputDir": "<workspace_path>/game_src"})        │
│                                                            │
│  2. If r.status == "started" (async subprocess):           │
│     → Get process PIDs from r.processIds                   │
│     → Wait until ALL PIDs exit (poll every 5s)             │
│     → Call decompile_source again with same outputDir      │
│     → Now r.status should be "fresh"                       │
│                                                            │
│  3. Resolve paths from r.dlls                              │
│     → witchSrc = outputDir + "/" + r.dlls["Witch.dll"].dir │
│     → coreSrc  = outputDir + "/" + r.dlls["Witch.Core"].dir│
│                                                            │
│  4. NOW you may grep/read files under witchSrc / coreSrc   │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

**DO this:**
```
Call decompile_source MCP tool → resolve paths from response
```

**NEVER read from an arbitrary path without calling `decompile_source` first. The cache may be missing or stale.**

### Cache directory layout

Each DLL is cached under `{outputDir}/{sha256_hash}/`. The hash only changes when the DLL changes, so re-running `decompile_source` with the same `outputDir` on an unchanged game is instant (`status: "fresh"`).

**Async note:** On first run the tool spawns a separate `dotnet` process and returns immediately with `status: "started"` + `processIds`. Wait for those PIDs to exit, then call `decompile_source` again to get `status: "fresh"` and the cached paths.

```
{outputDir}/
├── .decompile_manifest.json     ← tracks hashes
├── 8d876.../                    ← Witch.dll's current hash
│   └── Witch.*.cs ...
└── ca6e9.../                    ← Witch.Core.dll's current hash
    └── Witch.Core.*.cs ...
```

If you change `outputDir` between sessions, old caches are preserved — the tool will regenerate into the new location.

### Important

- ICSharpCode.Decompiler runs via `dotnet` (included with Unity).
- Decompilation takes ~30 seconds per DLL on first run.
- Only targets **`witch.dll`** and **`witch.core.dll`**.
