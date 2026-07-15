---
name: witch-mod-mcp
description: "Mod development aid for the game Witch (女巫/魔法少女 roguelike deckbuilder): inspect and drive a running game instance through the WitchModMCP in-game HTTP server to develop, debug, and verify mods. Use when a mod developer wants to read live game state (player HP/SAN/money/deck, fight status, run progress), reflect over C# objects, query config tables, dump the scene tree or loaded-mod state, tail logs, or trigger console commands / items / scenes to reproduce and test mod behavior. Not a cheat/trainer tool for regular players. Triggers: WitchModMCP, Witch mod dev, MCPPort, get_game_data, eval_command, query_config, inspect RoleTable, dump_mod_state, reload_tools, 女巫 mod 开发, 调试 mod, 游戏状态, play_card, end_turn, load_scene, give_item, get_fight_state, set_lobby_state, raycast_mouse, set_rng_seed."
---

# WitchModMCP

WitchModMCP is a mod development tool for the game **Witch** (女巫/魔法少女 roguelike deckbuilder). It helps you inspect live game state, test mod behaviour, query config tables, control fights, navigate scenes, and debug issues — all through standard MCP tools.

## Quick Start — Creating a New Mod

```
1. git clone https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git

2. Copy the right template:
   Lua Mod (95% of cases):  copy ModTemplate/ → YourModName
   C# DLL Mod (complex):     copy DllTemplate/ → YourModName
   Folder name MUST match ModConfig.json's ModName.

3. Edit ModConfig.json:
   "ModName": "YourModName", "ModAuthor": "YourName", "Enabled": true

4. Write CSV files under Data/ and Text/.
   The CSV headers in the template ARE the schema — no need to memorize columns.
   The template's Scripts/Lib/DataConfigs/ contains ALL original game CSV schemas for reference.

5. Copy to game Mods folder and restart.
```

> **⚠️ 严禁手搓目录：必须从模板复制。** `New-Item` / `mkdir` 手动创建目录会丢失模板中的关键文件（`Scripts/Lib/DataConfigs/` 下的 160+ 个 CSV 列名参考、`Scripts/ScriptSample.lua`、`Icon.png` 等），直接导致 CSV 列名错误或资源缺失。**一律用 `git clone` → `Copy-Item` 从模板复制**。

For detailed template usage, see [templates/using-templates.md](./templates/using-templates.md).

## Architecture

Communication goes through a gateway:

```
AI (opencode)
  │  stdin/stdout (MCP JSON-RPC)
  ▼
mcp_gateway/server.py                ← MCP stdio server
  │  - proxies tools/call → HTTP
  │  - background heartbeat
  │  - auto-syncs skill docs + decompile source on first heartbeat
  ▼
WitchModMCP Mod (in Unity game)
  │  HTTP server on port MCPPort (default 3100) — no auth, localhost only
  │  JSON-RPC 2.0, returns PascalCase via Newtonsoft
```

**The game mod's HTTP server binds to localhost only (not exposed to network) and has no auth.** If you write a Python test script, connect directly to `http://localhost:3100/` (see `witch_mcp.py` for a ready-to-use client).

## Core Rules

### MCP Tool Rules

1. **`list_tools` is the source of truth.** Always run it first.
2. **Read before you write.** Prefer read-only tools. Mutation tools change live game state.
3. **Every call blocks the game's main thread briefly.** Keep parameters tight (`limit`, `maxDepth`, etc.).
4. **If `tools/list` fails**, the gateway cannot reach the game mod. Check that the game is running, WitchModMCP is loaded, and the port/token match.

### Mod Content Rules

5. **Clone the template repo first** (`git clone https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git`). The CSV headers in the template ARE the schema. Do NOT probe the game runtime (`query_config` / `inspect`) to discover data formats. **NEVER use `New-Item` / `mkdir` to create mod folders manually** — you must `Copy-Item` from the cloned template to preserve required files.

### Test Verification — Always Verify After Changes

**Writing the code is only half the work. You MUST test your mod with live MCP tools.** Never assume a CSV is correct or a Lua script works without testing.

**Primary method — verify with MCP tools directly:**
```
# 1. Check data loaded
search_config({"pattern": "YourModFolder"})

# 2. Enter game and check lobby
enter_game → start_new_game → get_lobby_state

# 3. Start a run and inject the card into a fight
start_run → load_scene({"type": "fakefight"}) → give_item({"type": "card", "value": "RuntimeId"})

# 4. Play the card
get_fight_state → play_card

# 5. Check logs on failure
get_recent_logs({"count": 100})
```

**Advanced method — write a Python test script:**
Use `witch_mcp.py` from the workspace to script automated tests:

```python
# test_my_mod.py
from witch_mcp import WitchMcp
mcp = WitchMcp()

# Verify data loaded
result = mcp.search_config("MyMod")
assert result["matchCount"] > 0, "Mod data not loaded!"

# Start game
mcp.call("enter_game")
mcp.call("start_new_game", {"mode": "Normal", "useExistingSave": False})
mcp.call("start_run")
mcp.call("load_scene", {"type": "fakefight"})

# Inject card
mcp.call("give_item", {"type": "card", "value": "MyMod_CsvFile_CardId"})
fight = mcp.call("get_fight_state")
print(f"Cards in hand: {len(fight['FightCards'])}")
```

> The `witch_mcp.py` helper in the workspace connects directly to the mod at `http://localhost:3100/` with no auth required. Use this pattern to write idempotent test scripts for your mod. Run with `python test_my_mod.py`.

### Debug Workflow

When a mod doesn't work (card not found, pack not showing, data not loading):

1. **Always read game logs first** — call `get_recent_logs({"count": 100})` and search for any `[Mod]`, `[Error]`, or CSV loading messages. The game prints clear error messages when CSV parsing fails, Lua compilation fails, or mod config is invalid.
2. **Check mod was found at startup** — search logs for `[Mod] 发现: YourModName.YourAuthor`. If absent, check folder name matches `ModName` and `Enabled: true`.
3. **Check data loading errors** — search for `Error` or `fail` near your mod name in the logs. CSV column name mismatches, missing `BaseScript`, and invalid `PackBelong` are all logged.
4. **If data loaded but still broken** — use `eval_command("check <RuntimeId>")` to test if the ID is registered, or start a run and try `give_item` to inject the card into a fight.
5. **Use `search_config` to find runtime IDs** — when you need to check if a card/buff/cardpack was actually loaded into `DataConfigCache`. Pass a partial ID or keyword to `search_config({"pattern": "plague"})` to see matching runtime IDs and verify data loading.
6. **Only use `inspect` / `query_config` as last resort** — when logs are clean but you still need to verify internal state. Never use them to discover CSV schemas (use the template's `Lib/DataConfigs/` for that).
6. **Runtime ID namespace**: `{ModFolderName}_{CsvFileName}_{RawId}`. E.g., `EdictOfStars_starcards_1001`.
7. **PackBelong must point to a real CardPack** entry in `Data/CardPack/`.
8. **Must have Text CSV** — without it, game shows blank names. Mirror Data/ structure.
9. **BaseScript is required** in Card CSV: `AttackCardItem` (targeted damage) or `CommonCardItem` (self/global).
10. **`*` prefixed Ids** are starter cards (excluded from random pool).
11. **CSV Row 2 is a comment** (ignored by the game).
12. **Lua uses colon calls**: `self:AddBuff(id, level)`, not `self.AddBuff(id, level)`.
13. **xLua cannot use `[]` for dictionaries**: use `dict:get_Item("key")` / `dict:set_Item("key", "value")`.
14. **C# types use `CS.` prefix**: `CS.UnityEngine.Debug.Log(...)`.
15. **All changes require game restart** (except C# DLL can use `reload_tools`).

## Module Index

WitchModMCP tools are organized into domain modules. Load the relevant module for detailed documentation:

| Module | Tools | Triggers |
|--------|-------|---------|
| [Core](./base/core/SKILL.md) | `list_tools`, `list_commands`, `reload_tools`, `eval_command` | discovery, console command, eval_command |
| [Meta](./base/meta/SKILL.md) | `get_scene_state`, `get_game_data`, `check_mode_saves`, `list_game_modes` | scene state, game data, 场景检测, 页面状态 |
| [Combat](./base/combat/SKILL.md) | `get_fight_state`, `play_card`, `end_turn`, `set_card_pile`, `set_fight_entity` | 战斗, 出牌, 打牌, combat |
| [Lobby](./base/lobby/SKILL.md) | `get_lobby_state`, `set_lobby_state` | 大厅, 职业, 卡包, career, lobby |
| [Gameflow](./base/gameflow/SKILL.md) | `enter_game`, `start_new_game`, `start_run`, `load_scene`, `claim_rewards` | 启程, 开始游戏, 跳转, gameflow |
| [Diagnostics](./base/diagnostics/SKILL.md) | `inspect`, `query_config`, `search_config`, `dump_mod_state`, `get_scene_tree`, `get_recent_logs`, `raycast_mouse`, `set_rng_seed`, `get_screenshot`, `give_item` | 调试, 反射, 查配置, debug, diagnostics |
| [Game Insights](./insights/SKILL.md) | (knowledge base, no tools) | CSV schemas, Lua effect API, mod directory structure, built-in buff IDs |
| [Templates](./templates/using-templates.md) | (reference) | ModTemplate / DllTemplate usage, CSV column reference, example mod |
| [Code Patterns](./code-patterns/entry-patterns.md) | (reference) | Entry.lua patterns, Hook patterns, career mod architecture |

For a full module-by-module listing, open [base/SKILL.md](./base/SKILL.md).

### Extension: DeveloperTools

[DeveloperTools](./skills/SKILL.md) is an optional extension mod. See its docs for enhanced tool coverage.

## Skill documentation sync

Skill `.md` docs live inside each mod's folder under `mcp_skills/`. The gateway auto-syncs docs on first heartbeat.

## Common intents → module routing

| Intent | Module | Tool / Action |
|--------|--------|-------|
| "Start a new Mod" | (this skill) | `git clone https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git` → copy template |
| "Add cards / buffs / card packs" | Templates / Game Insights | See template CSV headers + [insights/SKILL.md](./insights/SKILL.md) section 11 |
| "Write Lua card effects" | Game Insights | [insights/SKILL.md](./insights/SKILL.md) section 11.3 Lua effect API |
| "Write Entry.lua with hooks" | Code Patterns | [code-patterns/entry-patterns.md](./code-patterns/entry-patterns.md) |
| "What page/state is the game in?" | Meta | `get_scene_state` |
| "What are the player's HP/money/deck?" | Meta | `get_game_data` |
| "What console commands exist?" | Core | `list_commands` → `eval_command` |
| "I need gold / a relic / a card" | Diagnostics | `give_item` |
| "Take me to a boss fight" | Gameflow | `load_scene` |
| "Show card config" | Diagnostics | `query_config` |
| "Search runtime config by keyword" | Diagnostics | `search_config` |
| "Find a card/buff/cardpack runtime ID" | Diagnostics | `search_config` |
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

> **RULE**: Before reading ANY decompiled game source, you MUST call `decompile_source` first.

> This is purely optional — the skill works without it. Skip if runtime data suffices.

### ═══ SOURCE ACCESS GATE ═══

```
1. Call decompile_source with {"outputDir": "<workspace_path>/game_src"}
   Returns {status, manifestPath, dlls: {"Witch.dll": {hash, dir}, "Witch.Core.dll": {hash, dir}}}
2. Resolve paths from dlls field
3. grep/read under those directories
```

## Skill Directory Structure

```
.agents/skills/witchSkill/
  SKILL.md                           ← This file: architecture, core rules, routing

  templates/                         ← Template repo usage
    using-templates.md                  ModTemplate / DllTemplate how-to
    reference-example.md                Complete example mod (Defect career)

  code-patterns/                     ← Lua/C# patterns from real mods
    entry-patterns.md                   Entry.lua 3 patterns + C# Hook pattern
    buff-as-resource.md                 Buff-as-resource mechanic
    card-transform.md                   Card transform + companion system
    cooldown-dice.md                    Cooldown / dice / milestone / phase cycle
    career-mod.md                       Full career mod architecture

  testing/                           ← Test scripts
    automated-test.py                   Runnable automation test
    verification.md                     Checklist + cross-module workflows + troubleshooting

  deployment/                        ← Deployment
    deploy.md                           Deploy tool + manual copy steps
    build-dll.md                        C# build pipeline

  base/                              ← MCP tool modules
    SKILL.md                            Module index
    core/                               list_tools, list_commands, reload_tools, eval_command
    meta/                               get_scene_state, get_game_data, check_mode_saves, list_game_modes
    combat/                             get_fight_state, play_card, end_turn, set_card_pile, set_fight_entity
    lobby/                              get_lobby_state, set_lobby_state
    gameflow/                           enter_game, start_new_game, start_run, load_scene, claim_rewards
    diagnostics/                        inspect, query_config, dump_mod_state, get_scene_tree, ...

  insights/                          ← Game knowledge base
    SKILL.md                            CSV schemas, Lua API, mod structure, built-in buff IDs

  devtools/                          ← DeveloperTools extension docs
    skills/SKILL.md                     Enhanced tool docs
```
