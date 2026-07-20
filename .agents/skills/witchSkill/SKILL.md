---
name: witch-mod-mcp
description: "Mod development aid for the game Witch (女巫/魔法少女/魔女/终末旅途 roguelike deckbuilder): inspect and drive a running game instance through the WitchModMCP in-game HTTP server to develop, debug, and verify mods. Use when a mod developer wants to read live game state (player HP/SAN/money/deck, fight status, run progress), reflect over C# objects, query config tables, dump the scene tree or loaded-mod state, tail logs, or trigger console commands / items / scenes to reproduce and test mod behavior. Not a cheat/trainer tool for regular players. Triggers: WitchModMCP, Witch mod dev, MCPPort, get_game_data, eval_command, query_config, inspect RoleTable, dump_mod_state, reload_tools, scan_ui, click_ui, 女巫 mod 开发, 调试 mod, 游戏状态, play_card, end_turn, load_scene, give_item, get_fight_state, set_lobby_state, raycast_mouse, set_rng_seed."
---

# WitchModMCP

WitchModMCP is a mod development tool for the game **Witch** (女巫/魔法少女/魔女/终末旅途 roguelike deckbuilder). It helps you inspect live game state, test mod behaviour, query config tables, control fights, navigate scenes, and debug issues — all through standard MCP tools.

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
   ⚠️ **CSV Row 2 是注释行**（被游戏忽略），数据必须从 **Row 3** 开始写。示例：
   ```
   Id,Rarity,Expend,Tag,InitScript,...
   唯一标识,稀有度,费用,标签,初始化Lua脚本,...   ← 这行是注释，会被跳过
   my_card,1,1,,self.Vars:set_Item("BaseScript", "AttackCardItem");...   ← 数据从这行开始
   ```
   如果只有 header + data 两行，data 行会被当作注释跳过，导致卡牌/卡包数据加载为 0。

5. Copy to game Mods folder and restart.
   ⚠️ **每次部署前先删除旧目录！** `Remove-Item -Recurse -Force "$gameDir\Mods\YourMod"` 再 `Copy-Item`。如果目标已存在时重复执行 `Copy-Item -Recurse`，会把源目录**嵌套复制**到目标内部（`YourMod/YourMod/`），导致游戏读到的是旧文件。

   **重启游戏方式：** AI 可以直接 Kill 游戏进程再启动，因为 SKILL 知道游戏安装路径：
   ```powershell
   Get-Process -Name "Witch*" -ErrorAction SilentlyContinue | Stop-Process -Force
   Start-Sleep -Seconds 3
   Start-Process -FilePath "F:\steam\steamapps\common\Witch's Apocalyptic Journey\Witch's Apocalyptic Journey.exe"
   Start-Sleep -Seconds 25
   ```
   CSV/Lua 变更必须重启游戏才能生效。C# DLL 热重载用 `reload_tools` 工具即可（见规则 24）。
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

**The game mod's HTTP server binds to localhost only (not exposed to network) and has no auth.** If you write a Python test script, connect directly to `http://localhost:3100/`. A ready-to-use client is at `[skill]/testing/witch_mcp.py` — copy it to your workspace root and import.

## Core Rules

### MCP Tool Rules

1. **`list_tools` is the source of truth.** Always run it first.
2. **Read before you write.** Prefer read-only tools. Mutation tools change live game state.
3. **Every call blocks the game's main thread briefly.** Keep parameters tight (`limit`, `maxDepth`, etc.).
4. **If `tools/list` fails**, the gateway cannot reach the game mod. Check that the game is running, WitchModMCP is loaded, and the port/token match.

### Mod Content Rules

5. **Clone the template repo first** (`git clone https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git`). The CSV headers in the template ARE the schema. Do NOT probe the game runtime (`query_config` / `inspect`) to discover data formats. **NEVER use `New-Item` / `mkdir` to create mod folders manually** — you must `Copy-Item` from the cloned template to preserve required files.

6. **Card Pack CSV requires `Data/CardPack/`** — this directory is NOT in the template. You must create it manually. See [insights §13.1](./insights/SKILL.md#131-adding-a-card-pack--cards-simplest-mod) for full steps.

7. **Runtime ID format: `{ModFolder}_{CsvFileName}_{RawId}`** — always use `search_config` to verify actual runtime IDs after writing CSVs. The `ModFolder` must match the folder name (not `ModName` in config). CsvFileName is the `.csv` filename without extension.

8. **`*` prefix on card/relic IDs** → excluded from random pools. Used for:
   - Character starter skill cards (Career `Skill1`/`Skill2`)
   - Event-only cards
   - Hidden/reward-only relics

9. **For character mods:** Always load [insights SKILL.md](./insights/SKILL.md) and reference §11.5c (SkillScript pattern) and §13.3 (step-by-step character creation). The SkillScript Lua code is the hardest part — use the standardized template with `SkillTime` + `SpecialVars` + `AddEvent("StartRound")` + `AddEvent("Win")` + `AddEvent("Escape")`.

### Test Verification — Always Verify After Changes

**Writing the code is only half the work. You MUST test your mod with live MCP tools.** Never assume a CSV is correct or a Lua script works without testing.

**Primary method — verify with MCP tools directly:**
```
# 1. Check data loaded (all content types)
search_config({"pattern": "YourModFolder"})
# Should show cards, cardpacks, buffs, careers, relics etc.

# 2. For card pack mods — check the pack loaded
search_config({"pattern": "YourMod_cardpack"})

# 3. Enter game and check lobby
enter_game → start_new_game → get_lobby_state

# 4. Start a run and inject the card into a fight
start_run → load_scene({"type": "fakefight"}) → give_item({"type": "card", "value": "RuntimeId"})

# 5. Play the card
get_fight_state → play_card

# 6. For character mods — check career loaded
search_config({"pattern": "YourMod_career"})
# Start a run with the new character
set_lobby_state({"career": "YourMod_csvfile_careerid"})
start_run → load_scene({"type": "fakefight"})
get_fight_state  # Check character is present

# 7. Check logs on failure
get_recent_logs({"count": 100})
```

**Advanced method — write a Python test script:**
Copy `[skill]/testing/witch_mcp.py` to your workspace root, then write:

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

Run: `python test_my_mod.py`

### Debug Workflow — Mod Content

When a mod doesn't work (card not found, pack not showing, data not loading):

1. **Always read game logs first** — call `get_recent_logs({"count": 100})` and search for any `[Mod]`, `[Error]`, or CSV loading messages. The game prints clear error messages when CSV parsing fails, Lua compilation fails, or mod config is invalid.
2. **Check mod was found at startup** — search logs for `[Mod] 发现: YourModName.YourAuthor`. If absent, check folder name matches `ModName` and `Enabled: true`.
3. **Check data loading errors** — search for `Error` or `fail` near your mod name in the logs. CSV column name mismatches, missing `BaseScript`, and invalid `PackBelong` are all logged.
4. **If data loaded but still broken** — use `eval_command("check <RuntimeId>")` to test if the ID is registered, or start a run and try `give_item` to inject the card into a fight.
5. **Use `search_config` to find runtime IDs** — when you need to check if a card/buff/cardpack was actually loaded into `DataConfigCache`. Pass a partial ID or keyword to `search_config({"pattern": "plague"})` to see matching runtime IDs and verify data loading.
6. **Only use `inspect` / `query_config` as last resort** — when logs are clean but you still need to verify internal state. Never use them to discover CSV schemas (use the template's `Lib/DataConfigs/` for that).

#### Card/CardPack/Character Debugging

| Symptom | Check This |
|---------|-----------|
| Card pack not showing in lobby | ① `search_config({"pattern": "YourMod_cardpack"})` — is the pack loaded? ② Check `Data/CardPack/` exists and CSV is valid ③ Check `Text/CardPack/` exists for name/description |
| Cards not in pack | ① `search_config({"pattern": "YourMod_card"})` — are cards loaded? ② `PackBelong` must match the pack's **runtime ID** exactly ③ Use `get_recent_logs` to search for PackBelong errors |
| Card has blank name | Missing `Text/Card/` entry — must have matching `Id` |
| Card crashes on play | ① `InitScript` missing `BaseScript` — add `self.Vars:set_Item("BaseScript", "AttackCardItem")` ② Lua syntax error in `UseScript` — check xLua compatibility (no `[]`, use `get_Item`) |
| Character not showing | ① `search_config({"pattern": "YourMod_career"})` — is career loaded? ② Is `Data/RoleData/` present? ③ Check all image paths exist in Career CSV |
| Lua "object reference not set" | Nil check missing — always guard with `if self.Self ~= nil then ... end` |
| Skill cooldown not counting | `SkillTime` key not initialized in SkillScript — add `if not st:ContainsKey(key) then st:set_Item(key, 0) end` |

#### General Debug Rules

15. **Runtime ID namespace**: `{ModFolderName}_{CsvFileName}_{RawId}`. E.g., `EdictOfStars_starcards_1001`. Always verify with `search_config`.
16. **PackBelong must point to a real CardPack** entry in `Data/CardPack/` (or auto-created from cards' PackBelong).
17. **Must have Text CSV** — without it, game shows blank names. Mirror Data/ structure.
18. **BaseScript is required** in Card CSV `InitScript`: `AttackCardItem` (targeted damage) or `CommonCardItem` (self/global/AoE).
19. **`*` prefixed Ids** are starter cards (excluded from random pool).
20. **CSV Row 2 is a comment** (ignored by the game).
21. **Lua uses colon calls**: `self:AddBuff(id, level)`, not `self.AddBuff(id, level)`.
22. **xLua cannot use `[]` for dictionaries**: use `dict:get_Item("key")` / `dict:set_Item("key", "value")`.
23. **C# types use `CS.` prefix**: `CS.UnityEngine.Debug.Log(...)`.
24. **CSV/Lua 变更必须重启游戏**（C# DLL 变更可用 `reload_tools` 热重载）。AI 应直接 `Stop-Process` + `Start-Process` 杀启进程，无需手动操作。

## Module Index

WitchModMCP tools are organized into domain modules. Load the relevant module for detailed documentation:

| Module | Tools / Docs | Triggers |
|--------|----------|---------|
| [Core](./base/core/SKILL.md) | `list_tools`, `list_commands`, `reload_tools`, `eval_command` | discovery, console command, eval_command |
| [Meta](./base/meta/SKILL.md) | `get_scene_state`, `get_game_data`, `get_game_info`, `check_mode_saves`, `list_game_modes`, `get_recent_logs` | scene state, game data, game info, 场景检测, 页面状态, 日志 |
| [Combat](./base/combat/SKILL.md) | `get_fight_state`, `play_card`, `use_skill`, `get_skills_state`, `end_turn`, `set_card_pile`, `set_fight_entity`, `get_deck_selection`, `select_deck_cards` | 战斗, 出牌, 打牌, combat, 技能, 选牌, 弃牌 |
| [Lobby](./base/lobby/SKILL.md) | `get_lobby_state`, `set_lobby_state` | 大厅, 职业, 卡包, career, lobby |
| [Gameflow](./base/gameflow/SKILL.md) | `enter_game`, `start_new_game`, `start_run`, `map_select_state`, `map_select_assign`, `map_select_clear`, `map_select_confirm`, `load_scene`, `claim_rewards` | 启程, 开始游戏, 跳转, gameflow, 地图选点, 节点编排 |
| [Deck](./base/deck/SKILL.md) | `get_outdeck_state`, `outdeck_move_card`, `outdeck_decompose` | 牌组, deck, 装备/备选, 分解, 卡牌管理, outdeck |
| [Diagnostics](./base/diagnostics/SKILL.md) | `inspect`, `query_config`, `search_config`, `dump_mod_state`, `get_scene_tree`, `get_recent_logs`, `raycast_mouse`, `set_rng_seed`, `get_screenshot`, `give_item`, `get_env_info`, `scan_ui`, `click_ui` | 调试, 反射, 查配置, debug, diagnostics, 环境信息 |
| [Game Insights](./insights/SKILL.md) | **§11 CSV schemas**, **§13 Quick-Start Guides** (add cards §13.1, add career §13.3, SkillScript patterns §11.5c, testing §13.7) | CSV schemas, Lua effect API, mod directory structure, built-in buff IDs |
| [Templates](./templates/using-templates.md) | (reference) | ModTemplate / DllTemplate usage, CSV column reference, example mod |
| [Code Patterns](./code-patterns/entry-patterns.md) | (reference) | Entry.lua patterns, Hook patterns, career mod architecture |

For a full module-by-module listing, open [base/SKILL.md](./base/SKILL.md).

### Extension: DeveloperTools

[DeveloperTools](./skills/SKILL.md) is an optional extension mod. See its docs for enhanced tool coverage.

## Skill documentation sync

Skill `.md` docs live inside each mod's folder under `mcp_skills/`. The gateway auto-syncs docs on first heartbeat.

## Common intents → module routing

| Intent | Module / Section | Tool / Action |
|--------|-----------------|-------|
| "Start a new Mod" | (this skill) | `git clone https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git` → copy template |
| **"Add a card pack (with cards)"** | **[insights](./insights/SKILL.md) §13.1** | Create `Data/CardPack/*.csv` + Cards with `PackBelong` |
| **"Add cards to existing pools"** | **[insights](./insights/SKILL.md) §13.2** | Create `Data/Card/*.csv` (omit `PackBelong`) |
| **"Add a playable character"** | **[insights](./insights/SKILL.md) §13.3** | Career CSV + SkillScript + RoleData + skill cards + Text + animation |
| **"Write SkillScript (career passive)"** | **[insights](./insights/SKILL.md) §11.5c** | Lua pattern: SkillTime, SpecialVars, AddEvent, starting buffs |
| "Add relics / buffs" | [insights](./insights/SKILL.md) §11.4 / §11.5e | Create `Data/Relic/*.csv` or `Data/Buff/*.csv` |
| "Write Lua card effects" | [insights](./insights/SKILL.md) §11.3 / §13.5 | Lua effect API + character-mechanic patterns |
| "Add Lua hooks (Entry.lua)" | Code Patterns + [insights §13.6](./insights/SKILL.md#136-adding-entrylua-with-method-hooks) | `self:AddMethodHookBefore/After` |
| "Convert SkillScript to one line" | [insights](./insights/SKILL.md) §13.4 | Multi-line → minified single-line Lua for CSV |
| "Test / debug my mod content" | [insights](./insights/SKILL.md) §13.7 | search_config + get_recent_logs + give_item → verify |
| "What page/state is the game in?" | Meta | `get_scene_state` |
| "What are the player's HP/money/deck?" | Meta | `get_game_data` |
| "What game version / install path?" | Meta | `get_game_info` |
| "What console commands exist?" | Core | `list_commands` → `eval_command` |
| "I need gold / a relic / a card" | Diagnostics | `give_item` |
| "Take me to a boss fight" | Gameflow | `load_scene` |
| "Show card config" | Diagnostics | `query_config` |
| "Search runtime config by keyword" | Diagnostics | `search_config` |
| "Find a card/buff/cardpack runtime ID" | Diagnostics | `search_config` |
| "Read RoleTable.Instance.San" | Diagnostics | `inspect` |
| "Which mods are loaded?" | Diagnostics | `dump_mod_state` |
| "What GameObjects are in the scene?" | Diagnostics | `get_scene_tree` |
| "What clickable UI elements are available on this page?" | Diagnostics | `scan_ui` |
| "Click a specific UI element" | Diagnostics | `scan_ui` → `click_ui` |
| "Show recent game logs" | Diagnostics | `get_recent_logs` |
| "Use character skill 1/2" | Combat | `use_skill` |
| "Check skill cooldowns" | Combat | `get_skills_state` |
| "Play card X at enemy Y" | Combat | `play_card` |
| "End my turn" | Combat | `end_turn` |
| "Choose/select a card from a list" | Combat | `get_deck_selection` → `select_deck_cards` |
| "Discard cards" | Combat | `get_deck_selection` → `select_deck_cards` (same DeckUI mechanism) |
| "Set up a lobby with career X / pack Y" | Lobby | `set_lobby_state` |
| "Start a new run" | Gameflow | `start_new_game` → `set_lobby_state` → `start_run` |
| "I recompiled my tool DLL" | Core | `reload_tools` → `list_tools` |
| "Fill map node slots / start passage" | Gameflow | `map_select_state` → `map_select_assign` → `map_select_confirm` |
| "Move to next node in passage" | Gameflow | `map_select_confirm` (same tool) |
| "Manage my deck (equip/unequip cards)" | Deck | `get_outdeck_state` → `outdeck_move_card` |
| "Decompose/destroy a card" | Deck | `get_outdeck_state` → `outdeck_decompose` |
| "Check deck limits" | Deck | `get_outdeck_state` |
| "What developer tools are available?" | Diagnostics | `get_env_info` |
| "Dump environment info" | Diagnostics | `get_env_info` |

> **New mod creation workflow (card/cardpack/character):** Load this skill → `git clone` the template → load [insights/SKILL.md](./insights/SKILL.md) for CSV schema details → refer to §13.x quick-start guides for step-by-step instructions.

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
