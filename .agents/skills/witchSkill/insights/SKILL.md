---
name: witch-mod-mcp-game-insights
description: "Knowledge base: game Witch (女巫) architecture, data structures, C# API patterns, decompiled source internals. Use when the AI needs to understand how the game works internally (not tool usage). Triggers: game architecture, 游戏架构, data tables, 数据表, C# API, singleton, decompile, 反编译, game internals."
---

# Game Insights — Architecture & Internals Knowledge Base

This module contains knowledge extracted from decompiled game source (`decompile_source`), mod analysis, and the API documentation framework. It helps the AI understand the game's internal architecture when debugging or developing mods.

> **IMPORTANT**: This is knowledge base, not tools. Use `inspect` / `query_config` / `decompile_source` for live data.
>
> **⚠️ RULE for AI: Before creating ANY mod content (cards, buffs, card packs, relics, etc.), you MUST load this skill first to understand the data format. Do NOT probe the game runtime with `query_config` or `inspect` to figure out CSV columns — they are documented here.**
>

## 1. Game Technology Stack

| Layer | Technology |
|-------|-----------|
| Game Engine | Unity (IL2CPP / Mono) |
| Scripting | xLua (embedded Lua VM) |
| Modding | C# DLL + Lua hybrid |
| AOP Framework | Rougamo (compile-time IL weaving) |
| Networking | Mirror (for multiplayer) |
| Serialization | Newtonsoft.Json + MemoryPack |
| UI | uGUI (Canvas/TextMeshPro) |
| Async | UniTask (Cysharp) |
| String Building | ZString (Cysharp) |
| LINQ | ZLinq |
| Obfuscation | Loxodon.Framework.Obfuscation |
| .NET Runtime | .NET Framework 4.72 (Unity) |

## 2. Core Singleton Architecture

The game uses a `Singleton<T>` pattern extensively:

```csharp
// Key singletons:
Singleton<GameConfigManager>.Instance  // Config tables + mod loading
Singleton<DialogueManager>.Instance     // Dialogue system
RoleTable.Instance / RoleTable.Inst     // Player runtime data
FightManager.Inst                        // Current fight controller
GameRuntimeData.Instance                 // Runtime game variables
GameApp.Instance                         // Application root
UIManager.Instance                       // UI management
MapManager.Instance                      // Map navigation
PlayerManager.Inst                       // Player management
LobbyManager.Instance                    // Career selection hall
```

## 3. Config Data System (`GameConfigManager`)

### How Config Tables Work

All game content (cards, buffs, relics, careers, etc.) is stored in **CSV files** loaded as `Dictionary<string, string>` rows. The system:

1. **CSV loading**: Reads `.csv` and `.xlsx` files from `Data/` and `Text/` directories
2. **Row format**: Each row is `Dictionary<string, string>`, column names are keys
3. **Script columns**: Columns whose name contains `"Script"` are interpreted as Lua code and pre-compiled
4. **Data types**: `DataType` enum values include `Card`, `Buff`, `Relic`, `Career`, `CardPack`, `Enemy`, `EnemyCard`, `EventList`, `Map`, `Hard`, `Blessing`, `Dialogue`, `Partner`, `PartnerCard`, `RoleData`, `EnchTag`, `KeyWords`, `Level`
5. **Caching**: `Globals.DataConfigCache` is a `ConcurrentDictionary<string, IDataConfig>`

### ID Naming Convention

The game assigns runtime IDs by combining: **ModFolder_CsvFileName_RawId**

For example, if `EdictOfStars/Data/Card/card_1001.csv` contains row with `Id=1001`:
- Runtime ID becomes: `EdictOfStars_Card_1001`

This means all content within a mod is namespaced by folder and file.

### DataConfig Runtime Object

```csharp
public class DataConfig {
    DataType Type;                          // Card, Buff, etc.
    IDictionary<string, string> data;       // Read-only config data
    IDictionary<string, string> Vars;       // Runtime variables:
                                            //   DesVal1-4 (description values)
                                            //   ThisCount
                                            //   layersExperienced
                                            //   InstanceID (GUID)
                                            //   Id
    bool IsNative;                          // Built-in game ID?
    string InstanceID;                      // Unique runtime instance ID
    IScriptExecutor scriptExecutor;          // Lua script engine
}
```

**Script pre-compilation**: When a DataConfig is created, all columns containing `"Script"` in their key are compiled via `PreCompileScripts()`.

## 4. Mod Loading System

### Load Order

From `GameConfigManager.Init()`:

```
1. Init Lua (ScriptExecutor.Init + VisualScriptExecutor.Init)
2. Load built-in configs (Addressables/DataConfigs/Data/ + Text/)
3. Register native IDs
4. Scan Globals.ModsPath for ModConfig.json files
5. Topological sort by Dependencies
6. For each mod (in dependency order):
   a. Load Data/ CSV files via LoadResource
   b. Load Text/ CSV files via LoadResource
   c. Call mod.Setup():
      - Create Lua table with C# proxy
      - Run Entry.lua → call Setup(self)
      - Load Entry.dll → call [ModInitialize] methods
      - Register [ModHook] methods
7. Build keyword dictionary (BuffKeyword_, CardKeyword_, EnchTag_)
8. Pre-compile all Lua scripts
9. Init DialogueManager
```

### Dependency Resolution

Uses topological sort (BFS with in-degree counting):
- Each mod declares `Dependencies` list by ModId (`ModName.Author`)
- Circular or missing dependencies → mod is blocked with error
- Disabled mods are skipped

### ModConfig Fields

```csharp
ModConfig.ModId          = ModName + "." + ModAuthor
ModConfig.DirectoryName  // Absolute path
ModConfig.Enabled         // User-enabled flag
ModConfig.Dependencies    // List<string> of ModId
ModConfig.ConfigEnabled   // Override from Configuration.json?
```

## 5. Hook System

Mods can hook C# methods via `ModHookRegistry`:

```csharp
// Registration:
ModHookRegistry.AddBefore("FightManager.StartPlayerTurn", callback);
ModHookRegistry.AddAfter("FightManager.EndPlayerTurn", callback);

// HookContext:
ModHookContext.Target      // 'this' of the hooked method
ModHookContext.Arguments   // Parameters of the hooked method
```

From C# DLL:
```csharp
[HookBefore(typeof(FightManager), "StartPlayerTurn")]
public static void MyHook(ModHookContext ctx) { }
```

From Lua:
```lua
self:AddMethodHookBefore("FightManager.StartPlayerTurn", function(ctx)
    -- ctx.Target, ctx.Arguments
end)
```

## 6. Console Commands System

`ConsoleLogic.Input()` reflects over the static `Commands` class:

```csharp
// Command methods are public static, parameters are strings
public static string give(string arg1 = "null", string arg2 = "null")
public static string load(string type, string id2 = null)
public static string check(string arg1 = "null")
// etc.
```

`[HelpText("...")]` attribute provides descriptions. Use `eval_command` tool to execute any command.

## 7. Player Data (RoleTable)

The player's runtime state is stored in `RoleTable` (singleton):

| Field | Description |
|-------|-------------|
| `CurHp` / `MaxHp` | Health |
| `San` / `MaxSan` | SAN (sanity) |
| `Money` | Gold |
| `Power` / `MaxPower` | Energy |
| `Status.Defend` | Shield/block |
| `Deck` | Card collection |
| `RelicList` | Relic collection |
| `BlessList` | Blessing collection |

## 8. Fight System

`FightManager.Inst` controls the current battle:

| Property | Description |
|----------|-------------|
| `FightPlayer` | Player entity in fight |
| `AllEnemys` | List of enemy entities |
| `FightCards` | Hand cards |
| `DrawCards` | Draw pile |
| `DiscardCards` | Discard pile |
| `ExhaustCards` | Exhausted cards |

Phases: `Player` → `Enemy` → `Player` → ...

## 9. Animation System

Animations are sprite-based (not 3D model):
- Each animation is a directory of PNG frames
- `AnimationLib/config.json` defines: `AnimationPerFrame`, `isLoop`, `Direction`
- Frame sequence follows naming convention `frame_N.png`
- Animation resolution: 300×300 for skill animations

## 10. Automation API (Built-in)

The game contains a built-in automation framework (`Witch.UI.Automation.*`):

| Class | Purpose |
|-------|---------|
| `RuntimeBattleAutomationService` | Battle automation |
| `RuntimeGameplayAutomationService` | Game flow automation |
| `RuntimeSceneAutomationService` | Scene interaction |
| `RuntimeUiAutomationService` | UI snapshots and interaction |
| `RuntimeUiSnapshot` / `RuntimeUiNode` | UI hierarchy |
| `RuntimeSceneSnapshot` / `RuntimeSceneObjectInfo` | Scene hierarchy |
| `RuntimePlayCardRequest` / `RuntimePlayCardResult` | Auto card play |

This provides an alternative approach for implementing MCP tools.

## 11. Mod Content Data Formats (CSV Schemas)

All mod content is defined in CSV files under `Data/` and `Text/` directories. The game loads `.csv` and `.xlsx` files. Each column is a `Dictionary<string, string>` key. Columns containing `"Script"` in their name are pre-compiled as Lua.

### 11.1 Mod Directory Structure

```
YourMod/
├── ModConfig.json                # Mod metadata (required)
├── Icon.png                      # Mod icon (optional)
├── Scripts/
│   └── Entry.lua                 # Entry point: ModConfig:Setup(self) (optional)
│   └── Entry.dll                 # C# entry point (optional, can coexist with Lua)
├── Data/
│   ├── Card/       *.csv         # Card definitions
│   ├── Buff/       *.csv         # Buff definitions
│   ├── CardPack/   *.csv         # Card pack definitions
│   ├── Relic/      *.csv         # Relic definitions
│   ├── Career/     *.csv         # Career/witch definitions
│   ├── Partner/    *.csv         # Partner definitions
│   ├── Enemy/      *.csv         # Enemy definitions
│   ├── EnemyCard/  *.csv         # Enemy card definitions
│   ├── EventList/  *.csv         # Event definitions
│   ├── Map/        *.csv         # Map definitions
│   ├── Blessing/   *.csv         # Blessing definitions
│   ├── Level/      *.csv         # Level scaling definitions
│   └── RoleData/   *.csv         # Player role data
├── Text/                         # Localization (same subdirectories as Data/)
│   ├── Card/       *.csv
│   ├── Buff/       *.csv
│   └── ...
└── ModResource/                  # Assets (icons, images, etc.)
    ├── Icon/
    │   └── Card/
    └── Images/
        └── CardPack/
```

### 11.2 ModConfig.json

```json
{
  "ModName": "YourModName",
  "ModVersion": "1.0",
  "ModAuthor": "YourName",
  "ModDescription": "Description of the mod",
  "IconPath": "Icon.png",
  "Enabled": true,
  "Dependencies": null,
  "MustSame": true
}
```

- `ModName` + `.` + `ModAuthor` → runtime ModId (e.g., `YourModName.YourName`)
- `Dependencies`: array of ModId strings that this mod depends on; null if none
- `MustSame`: if true, all players in multiplayer must have this mod

### 11.3 Card CSV Schema

**File location:** `Data/Card/<filename>.csv`

| Column | Description | Example |
|--------|-------------|---------|
| `Id` | Unique ID within file; combined with folder+filename at runtime | `1001` or `plague_spread` |
| `Rarity` | 1=Common, 2=Uncommon, 3=Rare, 4=Special | `2` |
| `Expend` | Energy cost | `2` |
| `Tag` | Card tags (comma-separated): `Retain`, `Burnout`, `Recycle`, `Ascension` | `""` |
| `InitScript` | Lua: sets up display info (`BaseScript`, `DesVal1-4`) | see below |
| `DrawScript` | Lua: triggered when drawn (optional) | `""` |
| `UseScript` | Lua: triggered when played (main effect) | see below |
| `DropScript` | Lua: triggered when discarded (optional) | `""` |
| `Icon` | Icon path (`Icon/Card/<name>` or `Mods/<Mod>/ModResource/...`) | `Icon/Card/plague` |
| `Effects` | Visual effect path (optional) | `""` |
| `Action` | Card type: `Attack`, `Skill`, or empty | `Skill` |
| `PackBelong` | Which card pack this card belongs to (runtime ID format) | `ModFolder_cardpack_packid` |

**Runtime ID format:** `{ModFolder}_{CsvFilename}_{RawId}`

Example: `YourMod_Card_plague_spread`

**InitScript patterns:**

For attack cards:
```lua
self.Vars:set_Item("BaseScript", "AttackCardItem")
self:AddDescription("1", "Damage", "8")
self:AddDescription("2", "Buff", "3")
```

For skill cards:
```lua
self.Vars:set_Item("BaseScript", "CommonCardItem")
self:AddDescription("1", "Buff", "3")
self:AddDescription("2", "Buff", "5")
```

**UseScript: Lua effect API (self = ScriptExecutor):**

| Method | Description |
|--------|-------------|
| `self:SetStatus("Self")` | Target self |
| `self:SetStatus("Target")` | Target single enemy |
| `self:SetStatus("AllTarget")` | Target all enemies |
| `self:SetStatus("AllEnemy")` | Target all enemies (alt) |
| `self:SetStatus("AllFriends")` | Target all allies |
| `self:SetStatus("All")` | Target everyone |
| `self:Damage("8")` | Deal 8 damage to current target(s) |
| `self:AddBuff("buff_id", "3")` | Apply 3 stacks of buff to current target(s) |
| `self:ChangeHp("5")` | Heal 5 HP to current target(s) |
| `self:ChangeHp("-5")` | Deal 5 damage (alt) |
| `self:ChangeMaxHp("10")` | Increase max HP by 10 |
| `self:DrawCount("3")` | Draw 3 cards |
| `self:ChangePower("2")` | Gain 2 energy |
| `self:ChangePower("-2")` | Lose 2 energy |
| `self:ChangeDefence("5")` | Gain 5 shield |
| `self:AddCard("id")` | Add card to hand |
| `self:RandomAddCard("id")` | Add card randomly to hand |
| `self:BurnCard("1", "0")` | Burn 1 card (0=random, 1=choose) |
| `self:AddEvent("EndRound", function() ... end)` | Register event callback |
| `self.Vars:set_Item("DesVal1", "value")` | Set description display value |
| `self:AddDescription("1", "Damage", "8")` | Add description line (index, type, value) |

**Built-in buff IDs** (game-native, usable without definition):

| ID | Chinese Name | Description |
|----|-------------|-------------|
| `buff_vulnerable` | 易伤 | Take increased damage |
| `buff_regenerate` | 再生 | Regenerate HP over time |
| `buff_burn` | 烧伤 | Burn damage over time |
| `buff_evergreen` | 常青 | Regeneration/evergreen buff |
| `buff_bleeding` | 流血 | Bleeding damage over time |
| `buff_weak` | 虚弱 | Reduced damage output |
| `buff_degrade` | 退化 | Degrade/debuff |
| `buff_extraordinary` | 卓绝 | Extraordinary buff |
| `buff_resilient` | 坚韧 | Resilient/armor buff |
| `buff_rebirth` | 重生 | Rebirth/resurrection |
| `buff_elements` | 元素 | Elemental power |
| `buff_contagion` | 传染 | Contagion/spread |

**Complete card example (plague_spread):**

Data/Card/plague.csv:
```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action,PackBelong
plague_spread,2,2,,"self.Vars:set_Item(""BaseScript"", ""CommonCardItem""); self:AddDescription(""1"", ""Buff"", ""3""); self:AddDescription(""2"", ""Buff"", ""5"");",,"self:SetStatus(""AllTarget""); self:AddBuff(""buff_vulnerable"", ""3""); self:SetStatus(""Self""); self:AddBuff(""buff_regenerate"", ""5"");",,Icon/Card/plague,,Skill,pack_plague
```

Runtime ID: `YourMod_plague_plague_spread` (if CSV filename is `plague.csv`)

### 11.4 Buff CSV Schema

**File location:** `Data/Buff/<filename>.csv`

| Column | Description | Example |
|--------|-------------|---------|
| `Id` | Unique buff identifier (used as `buff_id` in scripts) | `plague_mark` |
| `InitScript` | Lua: update display info | `""` |
| `ApplyScript` | Lua: triggered when buff is applied (use `self:AddEvent`) | see below |
| `ClearScript` | Lua: triggered when buff is cleared | `""` |
| `ReducePerTurn` | Stacks reduced per turn | `1` |
| `ReducePerAttacked` | Stacks reduced when attacked | `0` |
| `ReducePerUse` | Stacks reduced on action | `0` |
| `UpperBound` | Maximum stack limit | `999` |
| `Icon` | Icon path | `Icon/Buff/vulnerable` |
| `Type` | Buff category: `正面` (positive), `负面` (negative), `能力` (ability), `属性` (attribute) | `负面` |
| `Rarity` | Rarity display | `1` |
| `Effects` | Visual effect path (optional) | `""` |
| `SoundEffects` | Sound effect path (optional) | `""` |
| `Action` | Animation type (optional) | `""` |

### 11.5 CardPack CSV Schema

**File location:** `Data/CardPack/<filename>.csv`

| Column | Description | Example |
|--------|-------------|---------|
| `Id` | Unique card pack ID | `pack_plague` |
| `Type` | Pack type: `Normal` | `Normal` |
| `Icon` | Icon path | `Mods/YourMod/ModResource/Images/CardPack/pack_plague` |

**Note:** To make a card pack show up in the lobby selection, the cards that belong to it must have `PackBelong` set to the pack's runtime ID. Cards in `Data/Card/<file>.csv` with `PackBelong` referencing the pack will be auto-associated.

### 11.6 Text/Localization CSV Schema

**File location:** `Text/Card/<filename>.csv`, `Text/Buff/<filename>.csv`, etc.
(File names should match Data/ counterparts.)

| Column | Description | Example |
|--------|-------------|---------|
| `Id` | Must match the Data/ CSV Id | `plague_spread` |
| `Note` | Internal note (optional) | `""` |
| `Type` | Card type display text | `技能牌` |
| `Name` | Chinese name | `瘟疫蔓延` |
| `Name_en` | English name | `Plague Spread` |
| `Name_zh-Hant` | Traditional Chinese name | `瘟疫蔓延` |
| `Name_ja` | Japanese name | `疫病拡散` |
| `是否完成` | Translation completion flag | `TRUE` |
| `Description` | Chinese description | `对所有敌人施加{0}层{buff_vulnerable}，自身获得{1}层{buff_regenerate}。` |
| `Description_zh-Hant` | Traditional Chinese description | `...` |
| `Description_en` | English description | `Apply {0} stacks of {buff_vulnerable} to all enemies, then gain {1} stacks of {buff_regenerate}.` |
| `Description_ja` | Japanese description | `...` |

**Description placeholders:**
- `{0}`, `{1}`, etc. → replaced by `DesVal1`, `DesVal2`, etc. set in InitScript
- `{buff_id}` → replaced by the buff's display name from keyword dictionary

## 12. Mod Creation Workflow

When asked to create a mod that adds content (cards, buffs, card packs, etc.):

### Step 1: Load this skill
This skill documents all CSV schemas. Do NOT probe the game runtime to discover them.

### Step 2: Clone template and copy

```
git clone https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git
Copy-Item -Path "apocalyptic-journey-mod-tutorial/ModTemplate" -Destination "YourMod" -Recurse
```

**Do NOT `mkdir`/`New-Item` manually.** The template includes `Scripts/Lib/DataConfigs/` (160+ CSV schemas), `Scripts/ScriptSample.lua`, and `Icon.png` that are all required. Directories created from scratch will miss these and cause hard-to-debug failures.

### Step 3: Delete unused template files

Remove template subdirectories your mod doesn't need (e.g., `Data/Enemy/`, `Data/Relic/`, etc.). Keep only `Data/Card/`, `Data/CardPack/`, `Data/Buff/` and corresponding `Text/` entries.

### Step 4: Write CSV files
Use the schemas in section 11 above. The template's `Scripts/Lib/DataConfigs/` has the complete original game CSV column references — consult those for exact column names.

### Step 5: Install the mod
Copy the mod folder to `Witch's Apocalyptic Journey_Data/Mods/`.

### Step 6: Enable and test
- Start the game
- Use `get_scene_state` to confirm game loaded
- Use `search_config({"pattern": "YourModFolder"})` to verify data was loaded into DataConfigCache. If zero matches, CSV loading failed.
- For card packs: use `give_item givepack <PackId>` to get the pack in a run
- For cards: start a run and check if cards appear in the pool
- Use `give_item card <CardId>` to test a specific card in fight

**Important notes:**
- CSV column order does NOT matter; column names are the keys
- All Lua strings in CSV must escape `"` as `""` (Excel convention) or wrap in `""` 
- The `Id` column is the raw ID; runtime ID becomes `{ModFolder}_{CsvFilename}_{RawId}` (no underscores in ModFolder name recommended)
- Cards with `*` prefix in `Id` are starter cards (given at run start, not found in pool)
- Text CSV is optional; if missing, the game will generate placeholder names
- No need to restart Unity editor for CSV-only mods; the game loads them at startup
