---
name: witch-mod-mcp
description: "Mod development aid for the game Witch (女巫/魔法少女 roguelike deckbuilder): inspect and drive a running game instance through the WitchModMCP in-game HTTP server to develop, debug, and verify mods. Use when a mod developer wants to read live game state (player HP/SAN/money/deck, fight status, run progress), reflect over C# objects, query config tables, dump the scene tree or loaded-mod state, tail logs, or trigger console commands / items / scenes to reproduce and test mod behavior. Not a cheat/trainer tool for regular players. Triggers: WitchModMCP, Witch mod dev, MCPPort, get_game_data, eval_command, query_config, inspect RoleTable, dump_mod_state, reload_tools, 女巫 mod 开发, 调试 mod, 游戏状态, play_card, end_turn, load_scene, give_item, get_fight_state, set_lobby_state, raycast_mouse, set_rng_seed."
---

# WitchModMCP

Talk to a running instance of the game **Witch** via the WitchModMCP mod. The mod embeds an HTTP server inside the game process; you send it JSON-RPC-style requests and it runs game logic on the Unity main thread, then returns JSON.

This is a **mod-development tool**, not a player-facing cheat/trainer. Its purpose is to help a mod author understand the game's runtime, verify that a mod behaves correctly, and reproduce/debug issues.

## How the transport works

- Endpoint: `POST http://localhost:<port>/` — the port is set by `MCPPort` in the mod's `ModConfig.json` (**default `3100`**).
- Request body (JSON-RPC 2.0 style):

  ```json
  { "jsonrpc": "2.0", "id": 1, "method": "<tool_name>", "params": { } }
  ```

  `method` is the tool name directly (e.g. `"get_game_data"`). The only special method is `list_tools`. `params` is omitted for no-arg tools.
- Response uses PascalCase keys from Newtonsoft: `Result`, `Error`, `Id`. The result payload itself uses camelCase field names.
- Errors follow JSON-RPC codes: `-32601` method/tool not found, `-32602` invalid params, `-32603` internal error, `-32700` parse error.

## Core rules

1. **Confirm the server is alive first** with `ping` before a batch of calls. If it is unreachable, the game is not running, the mod failed to load, or the port is wrong — check `ModConfig.json` `MCPPort`. Do not retry blindly.
2. **`list_tools` is the source of truth.** Always run `list_tools` to see what is actually registered in this build (tools can be hot-added via `reload_tools`).
3. **Read before you write.** Prefer read-only tools to understand state. Mutation tools change live game state — only call them when the user clearly wants a change.
4. **Every call blocks the game's main thread briefly.** Keep parameters tight (`limit`, `maxDepth`, `maxItems`, `maxChildren`) to avoid huge payloads and frame hitches.

## Python helper

Use `scripts/witch_mcp.py` (stdlib only, no `pip install`).

```python
import sys
sys.path.insert(0, "scripts")
from witch_mcp import WitchMcp

g = WitchMcp(port=3100)
if not g.ping():
    raise SystemExit("WitchModMCP unreachable - is the game running?")

print(g.list_tools())
```

CLI form (quick one-offs):
```bash
python scripts/witch_mcp.py ping
python scripts/witch_mcp.py get_game_data
python scripts/witch_mcp.py --port 3100 eval_command "{\"command\": \"help give\"}"
```

Generic escape hatch: `g.call("<tool_name>", {...params})`.

## Module Index

WitchModMCP tools are organized into domain modules. Load the relevant module for detailed documentation:

| Module | Tools | Triggers |
|--------|-------|---------|
| [Core](./skills/core/SKILL.md) | `list_tools`, `list_commands`, `reload_tools`, `eval_command` | discovery, console command, eval_command |
| [Meta](./skills/meta/SKILL.md) | `get_scene_state`, `get_game_data`, `check_mode_saves`, `list_game_modes` | scene state, game data, 场景检测, 页面状态 |
| [Combat](./skills/combat/SKILL.md) | `get_fight_state`, `play_card`, `end_turn`, `set_card_pile`, `set_fight_entity` | 战斗, 出牌, 打牌, combat |
| [Lobby](./skills/lobby/SKILL.md) | `get_lobby_state`, `set_lobby_state` | 大厅, 职业, 卡包, career, lobby |
| [Gameflow](./skills/gameflow/SKILL.md) | `enter_game`, `start_new_game`, `start_run`, `load_scene`, `claim_rewards` | 启程, 开始游戏, 跳转, gameflow |
| [Diagnostics](./skills/diagnostics/SKILL.md) | `inspect`, `query_config`, `dump_mod_state`, `get_scene_tree`, `get_recent_logs`, `raycast_mouse`, `set_rng_seed`, `get_screenshot`, `give_item` | 调试, 反射, 查配置, debug, diagnostics |

For a full module-by-module listing, open [skills/SKILL.md](./skills/SKILL.md).

### Extension: DeveloperTools

[DeveloperTools](../developer-tools/SKILL.md) is an optional extension mod that adds 18 tools on top of the base WitchModMCP toolset. It provides enhanced/alternative implementations in several domains:

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

Skill `.md` docs live inside each mod's folder under `mcp_skills/`. When the game is running, `sync_skills.py` discovers all mods via `get_env_info` and copies their docs to a local cache for the AI to read:

```bash
cd /path/to/WitchModMCP
python sync_skills.py --port 3100 --cache-dir .cache/skills_cache
```

After syncing, the AI can find docs at `.cache/skills_cache/WitchModMCP.DeveloperTools/`.

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
│     → g.call("decompile_source",                           │
│         {"outputDir": "<workspace_path>/game_src"})        │
│     Returns {status, manifestPath, dlls: {                 │
│       "Witch.dll":      {hash, dir},                       │
│       "Witch.Core.dll": {hash, dir} }}                     │
│     If status=="fresh" → skip, already cached              │
│     If status=="decompiled" → it was just rebuilt          │
│                                                            │
│  2. Resolve paths from dlls field                          │
│     → witchSrc = outputDir + "/" + dlls["Witch.dll"].dir   │
│     → coreSrc  = outputDir + "/" + dlls["Witch.Core"].dir │
│     These are the directories containing .cs files         │
│                                                            │
│  3. NOW you may grep/read files under witchSrc / coreSrc   │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

**DO this:**
```python
import sys; sys.path.insert(0, "scripts")
from witch_mcp import WitchMcp
g = WitchMcp(port=3100)
r = g.call("decompile_source", {"outputDir": "./game_src"})
# r.status is "fresh" or "decompiled"
witchDir = "./game_src/" + r["dlls"]["Witch.dll"]["dir"]
coreDir  = "./game_src/" + r["dlls"]["Witch.Core.dll"]["dir"]
# now read .cs files under witchDir and coreDir
```

**NEVER do this:**
```python
# Reading from an arbitrary path without calling decompile_source first
# The cache may be missing or stale.
```

### Cache directory layout

Each DLL is cached under `{outputDir}/{sha256_hash}/`. The hash only changes when the DLL changes, so re-running `decompile_source` with the same `outputDir` on an unchanged game is instant (`status: "fresh"`).

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
