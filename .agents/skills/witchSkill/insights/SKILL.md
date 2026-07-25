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

## 5. Hook System — 关键概念

Mod 有两种 hook 机制，**底层完全不同**，理解这一点对选择模板至关重要：

### 5.1 ModHookRegistry（Lua + C# 共用，可 hook 任意方法但只能监听）

> ⚠️ **重要更新：Rougamo 织入的是 ALL 方法，不是部分方法。** 以下描述已根据反编译源码修正。

Rougamo 的 `Modifiable` attribute 配置为 `[Pointcut(AccessFlags.All | AccessFlags.Property | AccessFlags.Method)]`，因此 **Witch.dll 和 Witch.Core.dll 中每个方法、属性 getter/setter 都被 IL 织入**（协程、TargetRpc 等少数例外）。

织入后的每个公开方法都变成一个 Rougamo 生成的包装器，调用链如下：

```
OnEntry (Modifiable)
  → ModHookRegistry.GetBefore("ClassName.MethodName") → 触发所有 Before 回调
  → $Rougamo_原方法名() ← 原方法体被重命名至此
  → OnSuccess (Modifiable)
    → ModHookRegistry.GetAfter("ClassName.MethodName") → 触发所有 After 回调
```

```csharp
// 反编译确认的包装器结构
public bool TryMarkFirstDeadPlayer(string instanceId)
{
    Modifiable m = new Modifiable();
    MethodContext ctx = RougamoPool<MethodContext>.Get();
    ctx.Target = this;
    ctx.Arguments = new object[1] { instanceId };
    try {
        m.OnEntry(ctx);
        bool result = $Rougamo_TryMarkFirstDeadPlayer(instanceId); // 传的是原始参数
        m.OnSuccess(ctx);
        return result;
    } finally { RougamoPool<MethodContext>.Return(ctx); }
}

private bool $Rougamo_TryMarkFirstDeadPlayer(string instanceId) { /* 真正的逻辑 */ }
```

从 Lua 或 C# 注册 hook 都可以：

```csharp
// C# DLL
[HookBefore(typeof(FightManager), "StartPlayerTurn")]
public static void MyHook(ModHookContext ctx) { }
```

```lua
-- Lua Entry.lua
self:AddMethodHookBefore("FightManager.StartPlayerTurn", function(ctx)
    -- ctx.Target, ctx.Arguments
end)
```

#### ⚠️ 关键限制：只能监听，不能修改

虽然 Lua 可以 hook **任意方法**（实际上 Rougamo 覆盖了所有非特殊方法），但能力仅限于**观察**：

| 想做的事 | Lua AddMethodHookBefore/After | Harmony（下面讲） |
|----------|-------------------------------|-------------------|
| 读参数、读 `this` | ✅ `ctx.Arguments[i]` + `ctx.Target` | ✅ |
| **修改参数让原方法看到** | ❌ 包装器把原始变量传给 `$Rougamo_*`，改 `ctx.Arguments` 无效 | ✅ |
| **跳过原方法** | ❌ | ✅ `return false` |
| **修改返回值** | ❌ `result` 在 `OnSuccess` 前已锁定 | ✅ `ref __result` |
| **添加额外逻辑** | ✅ 可以在原方法前后执行任意代码 | ✅ |

核心原因是 Rougamo 包装器（见上面的反编译代码）：
1. 传**原始局部变量**给 `$Rougamo_*`，不是 `ctx.Arguments`
2. 返回值在 `OnSuccess` 之前已经赋值给局部变量，`OnSuccess` 无法干预

所以：**需要拦截/修改核心逻辑的行为（改费用、改伤害、跳过出牌校验等）仍需 Harmony。如果想要在方法前后加日志、触发事件、追踪状态，Lua hook 就够了。**

### 5.2 Harmony（仅 C#，可修改任意方法的行为）

Harmony 通过 IL 运行时重写方法体，**不依赖游戏预埋的 Rougamo 钩点**。当需要修改而非仅仅观察方法行为时使用。

```csharp
[HarmonyPatch(typeof(FightManager), nameof(FightManager.PlayerCanPlayCard))]
class Patch_PlayerCanPlayCard
{
    static bool Prefix(ref bool __result)
    {
        // 修改出牌合法性判定
        __result = true;
        return false; // 跳过原方法
    }
}
```

**Harmony 能做的事（Lua hook 做不到的）：**
- **Prefix**：在方法前执行，可跳过原方法（`return false`），可修改参数和返回值
- **Postfix**：在方法后执行，可读取或修改返回值
- **Transpiler**：直接改写方法的 IL 指令序列
- 可以 hook **任何方法**，包括 Rougamo 无法织入的少数例外（协程、TargetRpc）

Harmony 只能写在 C# Entry.dll 中，不能从 Lua 调用。

### 5.3 总结判断

| 需求 | 用什么 | 原因 |
|------|--------|------|
| 在方法前后加日志、触发事件、追踪状态 | Lua `AddMethodHookBefore/After`（直接用 Entry.lua） | Lua 观察任意方法足够 |
| **修改**费用校验、伤害数值、出牌合法性、能量消耗等 | **必须 C# + Harmony Prefix/Postfix** | Lua 只能监听不能改 |
| **跳过**原方法逻辑（如让某张牌永远可出） | **必须 C# + Harmony Prefix + `return false`** | Lua 没有跳过能力 |
| Hook 协程或 TargetRpc 等 Rougamo 未覆盖的方法 | **必须 C# + Harmony** | Rougamo 织入不了这些 |
| 新增 UI、修改游戏管线 | **必须 C#** | UI 操作需要 Unity API |

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

> ⚠️ **CSV Schema 仅供参考。** 以下列出的列名来自对 BlackMage、Mokou、MoonRite、EdictOfStars、Muga-Yoshihide 等真实 mod 的 CSV 文件头部的观察。不同游戏版本或新增功能可能导致列名变化。**AI 如果需要确认某个 CSV 的确切列名，唯一可靠的方法是：**
> 1. 克隆模板仓库 `git clone https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git`
> 2. 查看 `ModTemplate/Scripts/Lib/DataConfigs/` 下的原始游戏 CSV 列名参考
> 3. 或使用 `decompile_source` 后搜索 `DataType.Card` 等枚举和 `LoadResourceTable` 方法

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
│   ├── CardPack/   *.csv         # Card pack definitions (NOT in ModTemplate — create manually)
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

> ⚠️ **API 仅供参考，不一定完全正确。** 以下列出的 Lua 方法和参数来自对现有 mod 的观察总结，但游戏版本更新或未覆盖的 API 可能导致不准确。**AI 如果需要确认某个 API 的签名、参数或返回值，唯一可靠的方法是使用 `decompile_source` 工具反编译游戏源码后查阅。** 反编译后可在 `game_src/Witch.Core` 等目录中搜索 `ScriptExecutor`、`IScriptExecutor`、`CardItem`、`ExcuterPublicHelper` 等关键类型。

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
| `self:AddCardById(id)` | Add specific card (by runtime ID) to hand (used by Muga, JogasakiNoah) |
| `self:AddCardByCardList(count, name)` | Add random card matching name from card list (BlackMage pattern) |
| `self:AddCard("id")` | Add card to hand (generic, used by EdictOfStars) |
| `self:RandomAddCard("id")` | Add card randomly to hand |
| `self:CreateCard(dataConfig)` | Create a card instance from DataConfig (Muga depict) |
| `self:AddCardToDeckById(id, toHand)` | Add card to deck (true=hand, false=draw pile) |
| `self:BurnCard("1", "0")` | Burn 1 card (0=random, 1=choose) |
| `self:BurnCardByData(dataConfig)` | Burn a specific card instance |
| `self:ForAllStatus(function(t) ... end)` | Iterate all status entities (enemies+self). Check `t.InstanceId` for filtering (used by Muga, MoonRite, JogasakiNoah) |
| `self:RunImmediately(buffId, eventName)` | Trigger a buff's event immediately (MoonRite pattern) |
| `self:AddEvent("EndRound", function() ... end)` | Register event callback (valid events: `StartRound`, `EndRound`, `ActionAfter`, `Hurt`, `BurnCard`, `Win`, `Escape`, `SelectCardEnd`, custom strings) |
| `self.Vars:set_Item("DesVal1", "value")` | Set description display value (DesVal1-4) |
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
plague_spread,2,2,,"self.Vars:set_Item(""BaseScript"", ""CommonCardItem""); self:AddDescription(""1"", ""Buff"", ""3""); self:AddDescription(""2"", ""Buff"", ""5"");",,"self:SetStatus(""AllTarget""); self:AddBuff(""buff_vulnerable"", ""3""); self:SetStatus(""Self""); self:AddBuff(""buff_regenerate"", ""5"");",,Icon/Card/plague,,Skill,YourMod_plaguepack_pack_plague
```

Runtime ID: `YourMod_plague_plague_spread` (if CSV filename is `plague.csv`)

> **Important:** The `PackBelong` column must use the **runtime ID** format `{ModFolder}_{CsvFileName}_{RawId}`, not just the raw `Id` from the CardPack CSV. For example, if your card pack CSV is `Data/CardPack/plaguepack.csv` with `Id=pack_plague`, the runtime ID is `YourMod_plaguepack_pack_plague` — that's what goes in `PackBelong`.

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

### 11.5 CardPack CSV Schema — Registering a New Card Pack

**File location:** `Data/CardPack/<filename>.csv`

The game's code (`GameConfigManager.LoadResourceTable(DataType.CardPack, ...)`) loads `.csv` files from this directory for mods. However, **the ModTemplate does NOT include this directory** — you must create it manually.

| Column | Required? | Description | Example |
|--------|-----------|-------------|---------|
| `Id` | Yes | Unique card pack ID (raw; runtime ID adds prefix) | `cardpack_blackmage` |
| `Type` | Yes | Pack display style: `Normal`, `Basic`, `Expand` | `Normal` |
| `Icon` | Yes | Icon path; game crashes if missing | `Mods/BlackMage/ModResource/Images/CardPack/blackmage` |

**Type values:** The game's UI checks `item["Type"] == "Basic"` for basic pack styling. Use `Normal` or `Expand` for expansion-style layout (which is typical for mod packs).

**Runtime ID generation:** `{ModFolder}_{CsvFileName}_{RawId}`  
- Example: BlackMage mod → file `blackmage.csv` → Id `cardpack_blackmage` → runtime ID: `BlackMage_blackmage_cardpack_blackmage`

**Linking cards to the pack:** In `Data/Card/<file>.csv`, set the `PackBelong` column to the **runtime ID** of the card pack:
```
PackBelong=BlackMage_blackmage_cardpack_blackmage
```

> ⚠️ **Runtime ID must be correct.** If `PackBelong` doesn't match the actual runtime ID, cards won't appear in the pack. The runtime ID format is `{ModFolder}_{CsvFileName}_{RawId}` — use `search_config({"pattern": "YourMod"})` to verify the actual IDs loaded in game.

**Text CSV (required for display):** Create `Text/CardPack/<filename>.csv`. Real mods all use this exact column format:
```csv
Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_ja,Description_en
cardpack_blackmage,,黑色秘术,黑色秘術,Black Mage,黑色秘術,作为破坏之力的引导者，成为黑魔法师吧。,作為破壞之力的引導者，成為黑魔法師吧。,破壊の力を導く者として、「黒魔道士」を名乗るがよい。,"As one who now commands the forces of destruction, I bid you go forth, black mage."
```
- `Id` must match the Data CSV `Id` (raw, not runtime)
- Without this, the pack will have no name/description in the lobby UI

> 💡 **Two approaches for card pack registration:**
> 1. **Data + Text** (BlackMage, EdictOfStars, MoonRite): Create both `Data/CardPack/<file>.csv` (with `Id,Type,Icon`) and `Text/CardPack/<file>.csv` (with localized text).
> 2. **Text-only** (Mokou, Muga-Yoshihide): Create only `Text/CardPack/<file>.csv`. The game auto-creates the card pack from the text entry. Cards still use the runtime ID (`{ModFolder}_{TextCsvFileName}_{RawId}`) in `PackBelong`. This works but the pack won't have a custom icon (game uses default).

**Real example — BlackMage mod:**
```
Data/CardPack/blackmage.csv:
  Id,Type,Icon
  cardpack_blackmage,Normal,Mods/BlackMage/ModResource/Images/CardPack/blackmage

Text/CardPack/blackmage.csv:
  Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_ja,Description_en
  cardpack_blackmage,,黑色秘术,黑色秘術,Black Mage,黑色秘術,作为破坏之力的引导者，成为黑魔法师吧。,作為破壞之力的引導者，成為黑魔法師吧。,破壊の力を導く者として、「黒魔道士」を名乗るがよい。,"As one who now commands the forces of destruction, I bid you go forth, black mage."
```

> 💡 **Character-exclusive packs:** When creating a character mod (Mokou, Muga-Yoshihide style), you can also register the card pack via `Text/CardPack/<file>.csv` without a corresponding `Data/CardPack/` entry (Mokou does this). The card pack will still appear because the game auto-creates packs from cards' `PackBelong` values. In this case, put the pack's `Id` (raw) in the Text CSV, and optionally add `Icon`/`Type` columns.

### 11.5b Career (Character) CSV Schema — Adding a New Playable Character

**File location:** `Data/Career/<filename>.csv`

Adding a new playable character requires the Career CSV plus supporting files (RoleData, Card entries for skill cards, Text CSVs, and animation resources).

**Career CSV Columns:**

| Column | Required | Description | Example |
|--------|----------|-------------|---------|
| `Id` | Yes | Career unique ID (raw) | `mokou` |
| `SanMax` | Yes | Maximum SAN (HP) | `100` |
| `SkillScript` | Yes | **Lua code** — passive skills, event listeners, initialization | see below |
| `Animation` | Yes | Path to animation directory | `Mods/Mokou/ModResource/AnimationLib/Mokou` |
| `Vocal` | No | Voice/animation library | `VocalLib/原初魔女` |
| `Skill1` | Yes | **Runtime ID** of first active skill card | `Mokou_cardsample_mokou_tail` |
| `Skill2` | No | **Runtime ID** of second active skill card | `Mokou_cardsample_mokou_kindling` |
| `ChoiceIcon` | Yes | Character selection icon path | `Mods/Mokou/ModResource/Images/Icon/Mokou` |
| `DollIcon` | Yes | Doll/animated icon path | `DollAni/原初魔女/玩偶_0` or `Mods/...` |
| `Character` | Yes | Full character art path | `Mods/Mokou/ModResource/Images/Character/Mokou` |
| `Avatar` | Yes | Portrait/headshot path | `Mods/Mokou/ModResource/Images/Avatar/Mokou` |
| `CareerImage` | Yes | Career selection image path | `Mods/Mokou/ModResource/Images/CareerImage/Mokou` |
| `ActionImage1` | Yes | Skill 1 icon path | `Mods/EdictOfStars/ModResource/Icon/Card/ishtar_oracle` |
| `ActionImage2` | No | Skill 2 icon path (if has 2 skills) | (path) |
| `Dialogue` | Yes | Dialogue sprite directory path | `Images/Dialogue/Character/原初魔女` |
| `EmojiPath` | No | Emoji sprite path | (path to emoji images) |
| `AttackEffect` | No | Attack effect name | (effect name) |
| `SkillEffect` | No | Skill effect name | (effect name) |
| `HitEffect` | No | Hit effect name | (effect name) |
| `DefendEffect` | No | Defend effect name | (effect name) |
| `FightWidget` | No | Custom fight widget path | (path) |
| `Note` | No | Internal note/description | `Ishtar formal character interface` |

**Runtime ID for career:** `{ModFolder}_{CsvFileName}_{RawId}`  
Example: `Mokou_careersample_mokou`

> ⚠️ **SkillScript API 仅供参考。** 以下模式来自对 Mokou、Muga-Yoshihide、EdictOfStars 等真实 mod 的观察，但 `CS.ScriptExecutor.PlayerInfo` 的可用字段、`self:AddEvent()` 支持的事件名等细节可能随游戏版本变化。**如有疑问，唯一可靠的确认方式是 `decompile_source` 后查阅反编译源码。**

### 11.5c SkillScript — Character Passive & Initialization Lua Code

The `SkillScript` column in Career CSV contains Lua code that runs when the character enters a fight. Based on real mods (Mokou, Muga-Yoshihide, EdictOfStars), the standard pattern is:

```lua
-- 1. Define skill cooldown keys
local k1 = "ModName_cardsample_skill1_id"   -- Skill1's runtime ID
local k2 = "ModName_cardsample_skill2_id"   -- Skill2's runtime ID (if exists)
local p = CS.ScriptExecutor.PlayerInfo
local st = p.SkillTime                      -- Dictionary<string,int> for cooldowns

-- 2. Initialize cooldown trackers (required for skill cards with cd)
if not st:ContainsKey(k1) then st:set_Item(k1, 0) end
if k2 ~= nil and not st:ContainsKey(k2) then st:set_Item(k2, 0) end

-- 3. Track cross-battle persistent stats (optional)
local sv = p.SpecialVars                    -- Dictionary<string,string> for cross-battle data
if not sv:ContainsKey("wuwo") then sv:set_Item("wuwo", "0") end

-- 4. Register StartRound event (cooldown reduction + conditional buffs)
self:AddEvent("StartRound", function()
  local s = CS.ScriptExecutor.PlayerInfo.SkillTime
  if s == nil then return end
  -- Reduce cooldowns
  if s:ContainsKey(k1) then
    local cd = s:get_Item(k1)
    if cd > 0 then s:set_Item(k1, cd - 1) end
  end
  -- Apply conditional buffs based on stacks
  local wb = self.Self:GetBuff("ModName_buffsample_wuwo")
  local wlv = 0
  if wb ~= nil and wb.buffConfig ~= nil then
    wlv = wb.buffConfig.Level or 0
  end
  if wlv >= 50 and self.Self:GetBuff("ModName_buffsample_xin") == nil then
    self:AddBuff("ModName_buffsample_xin", "1")
  end
end)

-- 5. Register BurnCard event (for cards with Burnout synergy, optional)
self:AddEvent("BurnCard", function()
  -- Reduce cooldowns on burn etc.
end)

-- 6. Register Win/Escape events (persist cross-battle stats)
self:AddEvent("Win", function()
  local b = self.Self:GetBuff("ModName_buffsample_wuwo")
  local lv = 0
  if b ~= nil and b.buffConfig ~= nil then lv = b.buffConfig.Level or 0 end
  CS.ScriptExecutor.PlayerInfo.SpecialVars:set_Item("wuwo", tostring(lv))
end)
self:AddEvent("Escape", function() ... end)  -- same pattern as Win

-- 7. Apply starting buffs
self:SetStatus("Self")
self:AddBuff("buff_evergreen", "5")          -- built-in buffs
self:AddBuff("buff_rebirth", "10")
self:AddBuff("ModName_buffsample_immortality", "1")  -- custom buff
```

**Key patterns observed in real character mods:**

| Mod | Cooldown Tracking | Cross-Battle Persistence | Starting Buffs | Special Event |
|-----|-------------------|--------------------------|----------------|---------------|
| Mokou | `SkillTime` dict | None (simple) | `evergreen`, `rebirth`, `immortality` | `BurnCard`, `SelectCardEnd` |
| Muga-Yoshihide | `SkillTime` dict | `SpecialVars["wuwo"]` via `Win`/`Escape` | `wuwo` (stacks) | `Win` saves stacks, conditional buffs at 50/100 stacks |
| EdictOfStars | None (oracle-based) | `SpecialVars` for blood/blessing state | `bloodstain`, `blooming` | Complex companion/blessing hooks |
| Plantago | `SkillTime` dict | `SpecialVars` via `Win`/`Escape` | Starting buffs | `Win`/`Escape` persist |

### 11.5d RoleData CSV Schema

**File location:** `Data/RoleData/<filename>.csv` (required when adding a character/career)

| Column | Description | Example |
|--------|-------------|---------|
| `Id` | Must match Career `Id` (raw) | `ishtar` |
| `Avatar` | Avatar image path | `Mods/EdictOfStars/ModResource/Images/Avatar/Ishtar` |
| `CharacterImage` | Full character image path | `Mods/EdictOfStars/ModResource/Images/Character/Ishtar` |

**Text CSV:** `Text/RoleData/<filename>.csv` with `Id`, `Name`, `Name_en` etc. for localized display name.

### 11.5e Relic CSV Schema

**File location:** `Data/Relic/<filename>.csv`

| Column | Description | Example |
|--------|-------------|---------|
| `Id` | Unique relic ID (`*` prefix excludes from random pools) | `*superheated_phoenix_feather` |
| `Rarity` | 1=Common, 2=Uncommon, 3=Rare, 4=Special | `2` |
| `OwnScript` | Lua: triggered on acquisition (optional) | `""` |
| `FightScript` | Lua: triggered each fight start via `self:AddEvent("Hurt", ...)` | see below |
| `Icon` | Icon path | `Mods/Mokou/ModResource/Icon/Relic/superheated_phoenix_feather` |

**FightScript event patterns:**
```lua
-- Real examples from actual mod relics:
self:AddEvent("Hurt", function(data)
  local lost = tonumber(data.val) or 0
  if lost <= 0 then return end
  self:SetStatus("Self")
  self:AddBuff("buff_extraordinary", tostring(lost))
end)

self:AddEvent("EndRound", function()
  if self.Self == nil or self.Self.Defend > 0 then return end
  self:SetStatus("Self")
  self:AddBuff("buff_rebirth", "30")
end)

self:AddEvent("BurnCard", function()
  self:DrawCount("1")
end)
```

**Text CSV:** `Text/Relic/<filename>.csv` with same `Id`/`Name`/`Name_en`/`Description`/`Description_en` pattern. Also supports `Tips` (flavor text) column.

### 11.5f Partner CSV Schema (Partners/Companions)

**File location:** `Data/Partner/<filename>.csv` and `Data/PartnerCard/<filename>.csv`

Partners (like EdictOfStars' "观星猫") follow similar CSV patterns. See the template's `Lib/DataConfigs/` for exact column names.

### 11.5g Blessing CSV Schema (Blessings)

**File location:** `Data/Blessing/<filename>.csv`

Blessings can also be added. Set `PackBelong` to a card pack runtime ID to associate them with the pack. See the template's `Lib/DataConfigs/` for exact columns.

---

**Runtime ID summary for all content types:**

| Content Type | CSV Location | Runtime ID Format |
|---|---|---|
| Card | `Data/Card/<file>.csv` | `{ModFolder}_{<file>}_{RawId}` |
| Buff | `Data/Buff/<file>.csv` | `{ModFolder}_{<file>}_{RawId}` |
| Card Pack | `Data/CardPack/<file>.csv` | `{ModFolder}_{<file>}_{RawId}` |
| Career | `Data/Career/<file>.csv` | `{ModFolder}_{<file>}_{RawId}` |
| Relic | `Data/Relic/<file>.csv` | `{ModFolder}_{<file>}_{RawId}` |
| RoleData | `Data/RoleData/<file>.csv` | `{ModFolder}_{<file>}_{RawId}` |
| Partner | `Data/Partner/<file>.csv` | `{ModFolder}_{<file>}_{RawId}` |
| Blessing | `Data/Blessing/<file>.csv` | `{ModFolder}_{<file>}_{RawId}` |

> ⚠️ **`ModFolder`** must match `ModConfig.json`'s `ModName` exactly. If the folder is `BlackMage` but `ModName` is `BlackMageNew`, the runtime ID uses the folder name. Always use `search_config` to verify actual loaded IDs.

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

### Step 0: Choose your approach — Lua/CSV vs C# DLL

> 先阅读第 5 节 Hook System，理解 Lua hook（Rougamo 监听）和 C# Harmony（IL 改写）的真实能力边界再决定。

官方教程仓库包含两个模板：

- **`ModTemplate/`** — Lua Mod 模板。适合：纯 CSV 配表内容（卡牌、Buff、卡包等）、Entry.lua 注册 `AddMethodHookBefore/After` 监听事件、SkillScript Lua 写被动。
- **`DllTemplate/`** — C# DLL Hook 模板。适合：需要 **Harmony** 修改/跳过核心方法（费用校验、伤害计算、出牌合法性），或需要 C# 语言特性实现复杂逻辑。

**判定规则：**
- 如果只需要**在方法前后加点逻辑**（日志、追踪、加 Buff）→ Lua `AddMethodHookBefore/After` 就够了，Rougamo 覆盖了所有方法
- 如果需要**修改方法的行为**（让牌免费、跳过冷却校验、改变伤害数值）→ **必须 C# + Harmony**，Lua 监听模式改不了
- 如果需要新增 UI、文件 I/O、外部库 → **C#**

### Step 1: Load this skill
This skill documents all CSV schemas. Do NOT probe the game runtime to discover them.

### Step 2: Clone template and copy

```
git clone https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git
```

根据 Step 0 的选择，复制对应的模板：

```
# Lua/CSV 模组
Copy-Item -Path "apocalyptic-journey-mod-tutorial/ModTemplate" -Destination "YourMod" -Recurse

# C# DLL 模组
Copy-Item -Path "apocalyptic-journey-mod-tutorial/DllTemplate" -Destination "YourMod" -Recurse
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
- **Text CSV is REQUIRED for cards to be fully functional.** The game engine merges `Data/Card/` (gameplay data) and `Text/Card/` (localization) into a single row. The `Commands.give("card", id)` command calls `GetOne(DataType.Card, id)["Name"]` to get the card name for the result message — if the Text CSV is missing, the `Name` column doesn't exist and the command fails. This is by design: a card without localization data is considered incomplete. Always create a matching `Text/Card/<file>.csv` with at minimum `Id` and `Name` columns.
- No need to restart Unity editor for CSV-only mods; the game loads them at startup

---

## 13. Quick-Start Guides — Common Mod Tasks

### 13.1 Adding a Card Pack + Cards (Simplest Mod)

**Goal:** Create a mod that adds a new card pack with several cards, no new character.

**Step-by-step:**

```
Step 1: Clone the template and copy
  git clone https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git
  Copy-Item "apocalyptic-journey-mod-tutorial/ModTemplate" -Dest "MyCardPackMod" -Recurse

Step 2: Edit ModConfig.json
  { "ModName": "MyCardPackMod", "ModVersion": "1.0", "ModAuthor": "You", "Enabled": true }

Step 3: Create Data/CardPack/ directory (missing from template!)
  New-Item -ItemType Directory -Path "MyCardPackMod/Data/CardPack"

Step 4: Create Data/CardPack/mypack.csv
  Id,Type,Icon
  cardpack_mypack,Normal,Mods/MyCardPackMod/ModResource/Images/CardPack/mypack

Step 5: Create Text/CardPack/mypack.csv
  Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_ja,Description_en
  cardpack_mypack,,我的卡包,我的卡包,My Card Pack,マイカードパック,一张卡牌描述。,一張卡牌描述。,カード説明。,A card pack description.

Step 6: Edit Data/Card/cardsample.csv — replace its content
  Id,Rarity,Expend,Tag,PackBelong,InitScript,UseScript,Icon,Action
  my_first_card,1,1,,MyCardPackMod_mypack_cardpack_mypack,"self.Vars:set_Item(""BaseScript"", ""AttackCardItem""); self:AddDescription(""1"", ""Damage"", ""8"");","self:SetStatus(""Target""); self:Damage(""8"");",Mods/MyCardPackMod/ModResource/Images/Card/my_first_card,Attack

Step 7: Edit Text/Card/cardsample.csv — add card text
  Id,Name,Name_en,Description,Description_en
  my_first_card,我的第一张牌,My First Card,造成{0}点伤害。,Deal {0} damage.

Step 8: Add card icon → ModResource/Images/Card/my_first_card.png
  (And pack icon → ModResource/Images/CardPack/mypack.png)

Step 9: Copy the mod folder to the game Mods/ directory
Step 10: Enable mod in-game → select card pack in run → play
```

**Key rules for card packs:**
1. `Data/CardPack/` directory must be created manually (not in template)
2. `PackBelong` on cards must use the **runtime ID**: `MyCardPackMod_mypack_cardpack_mypack`
3. Cards without `PackBelong` won't appear in any pack (orphaned)
4. Card pack `Icon` path that's wrong → game crashes on pack selection screen
5. Text CSV is essential — without it, cards have blank names

### 13.2 Adding Cards to Existing Game Characters (No New Pack)

**Goal:** Add cards that appear in the default card pool (not in a new pack).

- Simply omit `PackBelong` column (or leave it empty) in the Card CSV
- Cards without `PackBelong` appear in the general card pool available to all characters
- ⚠️ The template's Card CSV may have `PackBelong` at the end — either delete the column or leave cells empty

### 13.3 Adding a Playable Character (Career Mod)

**Goal:** Create a full character mod with unique skill cards, buffs, and passive.

**Minimum required files:**

```
MyCharacter/
├── ModConfig.json
├── Data/
│   ├── Card/career.csv          ← Character's skill cards (must use * prefix on Id)
│   ├── Buff/buff.csv            ← Character-specific buffs
│   ├── Career/career.csv        ← Career definition with SkillScript
│   └── RoleData/roledata.csv    ← Avatar + character image references
├── Text/
│   ├── Card/career.csv
│   ├── Buff/buff.csv
│   ├── Career/career.csv
│   └── RoleData/roledata.csv
└── ModResource/
    ├── AnimationLib/CharacterName/  ← Animation frames (Idle/Attack/Defend/Hit/Skill)
    └── Images/
        ├── Icon/character_select.png
        ├── Avatar/portrait.png
        ├── Character/full_art.png
        ├── CareerImage/career_image.png
        └── Card/skill_card_icon.png
```

**Step-by-step character creation:**

**1. Create Data/Career/career.csv with SkillScript:**
```csv
Id,SanMax,SkillScript,Animation,Vocal,Skill1,Skill2,ChoiceIcon,DollIcon,Character,Avatar,CareerImage,ActionImage1,ActionImage2,Dialogue
my_character,100,"local k1=""MyCharacter_career_skill_one"";local k2=""MyCharacter_career_skill_two"";local st=CS.ScriptExecutor.PlayerInfo.SkillTime;if not st:ContainsKey(k1)then st:set_Item(k1,0)end;if not st:ContainsKey(k2)then st:set_Item(k2,0)end;self:AddEvent(""StartRound"",function()local s=CS.ScriptExecutor.PlayerInfo.SkillTime;if s==nil then return end;if s:ContainsKey(k1)then local cd=s:get_Item(k1);if cd>0 then s:set_Item(k1,cd-1)end end;if s:ContainsKey(k2)then local cd=s:get_Item(k2);if cd>0 then s:set_Item(k2,cd-1)end end;end);self:AddEvent(""Win"",function()end);self:AddEvent(""Escape"",function()end);self:SetStatus(""Self"");self:AddBuff(""MyCharacter_buff_starting_buff"",""5"");",Mods/MyCharacter/ModResource/AnimationLib/MyChar,,MyCharacter_career_skill_one,MyCharacter_career_skill_two,Mods/MyCharacter/ModResource/Images/Icon/my_character,Mods/MyCharacter/ModResource/AnimationLib/MyChar/Idle/Idle_00,Mods/MyCharacter/ModResource/Images/Character/MyChar,Mods/MyCharacter/ModResource/Images/Avatar/MyChar,Mods/MyCharacter/ModResource/Images/CareerImage/MyChar,Mods/MyCharacter/ModResource/Images/Card/skill_one,Mods/MyCharacter/ModResource/Images/Card/skill_two,Images/Dialogue/Character/MyCharacter
```

**2. Create Data/RoleData/roledata.csv:**
```csv
Id,Avatar,CharacterImage
my_character,Mods/MyCharacter/ModResource/Images/Avatar/MyChar,Mods/MyCharacter/ModResource/Images/Character/MyChar
```

**3. Create Data/Card/career.csv with *-prefixed skill cards:**
```csv
Id,Rarity,Expend,Tag,InitScript,UseScript,Icon,Action
*skill_one,3,0,,"self.Vars:set_Item(""BaseScript"",""AttackCardItem"");local p=CS.ScriptExecutor.PlayerInfo;local st=p.SkillTime;local cd=0;if st~=nil and st:ContainsKey(""MyCharacter_career_skill_one"")then cd=tonumber(st:get_Item(""MyCharacter_career_skill_one""))or 0 end;self.Vars:set_Item(""DesVal1"",tostring(cd));","local p=CS.ScriptExecutor.PlayerInfo;local k=""MyCharacter_career_skill_one"";local st=p.SkillTime;if st==nil then return end;if not st:ContainsKey(k)then st:set_Item(k,0)end;local cd=st:get_Item(k);if cd>0 then p.ShowCaption(""技能冷却中"");return end;self:SetStatus(""Target"");self:Damage(""30"");st:set_Item(k,3);",Mods/MyCharacter/ModResource/Images/Card/skill_one,Skill
*skill_two,3,0,,...
```

**4. Create Data/Buff/buff.csv:**
```csv
Id,ApplyScript,ReducePerTurn,ReducePerAttacked,ReducePerUse,UpperBound,Icon,Type,Rarity
starting_buff,,0,0,0,999,Mods/MyCharacter/ModResource/Images/Buff/starting_buff,能力,3
```

**5. Text CSVs:** Mirror each Data CSV with `Id`, `Name`, `Name_en`, `Description`, `Description_en`.

**6. Animation:** `AnimationLib/MyChar/` needs at minimum:
- `Idle/` — idle animation frames
- `Attack/` — attack animation frames
- `Defend/` — defend animation frames
- `Hit/` — hit animation frames
- `Skill/` — skill animation frames  
Each animation directory contains `frame_N.png` files and (optionally) `config.json` with `{"AnimationPerFrame": 0.1, "isLoop": false, "Direction": 1}`.
The animation path in Career CSV is set at the `AnimationLib/` level (no trailing slash).

**7. Character selection hook (JogasakiNoah pattern):** If you want to hide a character from selection (e.g., unlockable or hidden), use Entry.lua hooks:
```lua
function ModConfig:Setup()
  self:AddMethodHookAfter("GameEntryUI.UpdateState", function(ctx)
    -- Hide character from selection based on condition
  end)
end
```

**Key rules for character mods:**
1. **SkillScript** must be valid Lua (no syntax errors) — compile errors crash the mod load
2. **Skill card IDs must start with `*`** to exclude them from random card pools
3. **Skill cooldowns** use `PlayerInfo.SkillTime` (Dictionary), initialized in SkillScript
4. **Cross-battle persistence** (stacks, upgrades) uses `PlayerInfo.SpecialVars` — save on `Win`/`Escape` events, restore on fight start
5. **Starting buffs** are applied in SkillScript with `self:SetStatus("Self"); self:AddBuff("buff_id", "level")`
6. **Animation resources** must exist at the path specified — missing animation causes blank/invisible character
7. **Career image paths** all must be valid — any missing path causes at minimum a log error, potentially a crash
8. **Text/Career CSV** supports additional columns for display: `Title`, `Title_en`, `Passive1`, `Passive2`, `Action1`, `Action2` (all localized)

### 13.4 Converting Lua SkillScript to Multi-line (for readability)

Since the `SkillScript` column in Career CSV is a single cell, real mods compress the Lua into one line. For development, you can:
1. Write the Lua in a separate `.lua` file first, test it
2. Minify it into one line (remove newlines, escape `"` to `""`)
3. Paste into the CSV cell

Common `SkillScript` template with all standard events:
```lua
-- Skill cooldown keys (runtime IDs of skill cards)
local s1 = "ModName_csvfile_skill_one"
local s2 = "ModName_csvfile_skill_two"
local p = CS.ScriptExecutor.PlayerInfo
local st = p.SkillTime

-- Initialize cooldown trackers
if not st:ContainsKey(s1) then st:set_Item(s1, 0) end
if not st:ContainsKey(s2) then st:set_Item(s2, 0) end

-- Restore cross-battle persistent stats
local sv = p.SpecialVars
if not sv:ContainsKey("my_stat_key") then sv:set_Item("my_stat_key", "0") end
local stat_val = tonumber(sv:get_Item("my_stat_key")) or 0
if stat_val > 0 then self:AddBuff("ModName_buff_my_stat", tostring(stat_val)) end

-- StartRound: reduce cooldowns, check conditions
self:AddEvent("StartRound", function()
  local s = CS.ScriptExecutor.PlayerInfo.SkillTime
  if s == nil then return end
  if s:ContainsKey(s1) then local cd = s:get_Item(s1); if cd > 0 then s:set_Item(s1, cd - 1) end end
  if s:ContainsKey(s2) then local cd = s:get_Item(s2); if cd > 0 then s:set_Item(s2, cd - 1) end end
  -- Optional: check stack thresholds and grant buffs
end)

-- Win: persist stats
self:AddEvent("Win", function()
  local b = self.Self:GetBuff("ModName_buff_my_stat")
  local lv = 0
  if b ~= nil and b.buffConfig ~= nil then lv = b.buffConfig.Level or 0 end
  CS.ScriptExecutor.PlayerInfo.SpecialVars:set_Item("my_stat_key", tostring(lv))
end)

-- Escape: same as Win
self:AddEvent("Escape", function()
  local b = self.Self:GetBuff("ModName_buff_my_stat")
  local lv = 0
  if b ~= nil and b.buffConfig ~= nil then lv = b.buffConfig.Level or 0 end
  CS.ScriptExecutor.PlayerInfo.SpecialVars:set_Item("my_stat_key", tostring(lv))
end)

-- Apply starting buffs
self:SetStatus("Self")
self:AddBuff("ModName_buff_starting", "1")
```

**Minified version (single line for CSV):**
```lua
local s1=""ModName_csvfile_skill_one"";local s2=""ModName_csvfile_skill_two"";local p=CS.ScriptExecutor.PlayerInfo;local st=p.SkillTime;if not st:ContainsKey(s1)then st:set_Item(s1,0)end;if not st:ContainsKey(s2)then st:set_Item(s2,0)end;local sv=p.SpecialVars;if not sv:ContainsKey(""my_stat_key"")then sv:set_Item(""my_stat_key"",""0"")end;local stat_val=tonumber(sv:get_Item(""my_stat_key""))or 0;if stat_val>0 then self:AddBuff(""ModName_buff_my_stat"",tostring(stat_val))end;self:AddEvent(""StartRound"",function()local s=CS.ScriptExecutor.PlayerInfo.SkillTime;if s==nil then return end;if s:ContainsKey(s1)then local cd=s:get_Item(s1);if cd>0 then s:set_Item(s1,cd-1)end end;if s:ContainsKey(s2)then local cd=s:get_Item(s2);if cd>0 then s:set_Item(s2,cd-1)end end;end);self:AddEvent(""Win"",function()local b=self.Self:GetBuff(""ModName_buff_my_stat"");local lv=0;if b~=nil and b.buffConfig~=nil then lv=b.buffConfig.Level or 0 end;CS.ScriptExecutor.PlayerInfo.SpecialVars:set_Item(""my_stat_key"",tostring(lv))end);self:AddEvent(""Escape"",function()local b=self.Self:GetBuff(""ModName_buff_my_stat"");local lv=0;if b~=nil and b.buffConfig~=nil then lv=b.buffConfig.Level or 0 end;CS.ScriptExecutor.PlayerInfo.SpecialVars:set_Item(""my_stat_key"",tostring(lv))end);self:SetStatus(""Self"");self:AddBuff(""ModName_buff_starting"",""1"");
```

### 13.5 Adding Cards with Character-Specific Mechanics

When your cards reference character-specific buffs (like BlackMage's MP system, MoonRite's moonlight), the Lua pattern is:

```csv
-- In Card CSV UseScript column:
-- 1. Get character-specific buff
local mp = self.Self:GetBuff("ModName_buff_mp")
if mp == nil or mp.buffConfig.Level < cost then return end  -- not enough resource

-- 2. Spend resource
mp.buffConfig.Level = mp.buffConfig.Level - cost
if mp.buffConfig.Level <= 0 then self:RemoveBuff("ModName_buff_mp") end

-- 3. Apply effects
self:SetStatus("Target")
self:Damage("10")
self:SetStatus("Self")
self:AddBuff("ModName_buff_another", "1")
```

**Real example from BlackMage fire card:**
```lua
-- UseScript (excerpted):
local damage = 4
local mpCost = 14
local astral = self.Self:GetBuff("BlackMage_blackmage_astral_fire")
local umbral = self.Self:GetBuff("BlackMage_blackmage_umbral_ice")
local hearts = self.Self:GetBuff("BlackMage_blackmage_umbral_hearts")
if astral ~= nil and astral.buffConfig.Level > 0 then
  damage = math.floor(damage * (1 + astral.buffConfig.Level * 0.5))
end
local mp = self.Self:GetBuff("BlackMage_blackmage_mp")
if mp == nil or mp.buffConfig.Level < mpCost then return end
mp.buffConfig.Level = mp.buffConfig.Level - mpCost
if mp.buffConfig.Level <= 0 then self:RemoveBuff("BlackMage_blackmage_mp") end
self:SetStatus("Target")
self:Damage(tostring(damage), "Normal")
```

### 13.6 Adding Entry.lua with Method Hooks

Most mods don't need an Entry.lua — CSV-only mods work fine. But when you need runtime hooks (cooldown management, fight init, UI changes):

```lua
-- Scripts/Entry.lua
function ModConfig:Setup()
  -- Hook after fight player turn init (e.g., ensure resources)
  self:AddMethodHookAfter("Fight_PlayerTurn.Init", function(_)
    local player = CS.FightPlayer.Instance
    if player == nil or player.Status == nil then return end
    if player.Status:GetBuff("MyMod_buff_mp") == nil then
      player.Status:AddBuff("MyMod_buff_mp", "40")
    end
  end)

  -- Hook before a method
  self:AddMethodHookBefore("CardItem.EffectOfBurnCard", function(ctx)
    if ctx == nil or ctx.dataConfig == nil then return end
    -- Custom burn card handling
  end)

  -- Modify an existing config entry (e.g., raise burn cap)
  self:ModifyDataConfig("buff_burn", "UpperBound", "9999")
end
```

For hook patterns, see [code-patterns/entry-patterns.md](../code-patterns/entry-patterns.md).

### 13.7 Testing and Debugging Mod Content

**After writing CSV files, always verify with MCP tools:**

```bash
# 1. Verify the mod's data loaded into the game
search_config({"pattern": "YourModName"})
# Should return > 0 matchCount. If 0, check logs for CSV errors.

# 2. Check specific runtime IDs
search_config({"pattern": "YourModName_cardpack"})
search_config({"pattern": "YourModName_card"})
search_config({"pattern": "YourModName_buff"})

# 3. Read game logs for mod loading errors
get_recent_logs({"count": 100})
# Search for "[Mod]", "[Error]", cardpack, BaseScript, PackBelong etc.

# 4. Test in-game
enter_game
start_new_game({"mode": "Normal"})
set_lobby_state({"career": "default_career", "cardPacks": ["YourModName_yourfile_cardpack_yourpack"]})
start_run
load_scene({"type": "fakefight"})
give_item({"type": "card", "value": "YourModName_yourfile_your_card_id"})
get_fight_state()
play_card({"index": 0})  -- Play the card you injected
```

**Common errors and fixes:**

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| Card not in pack selection | `PackBelong` wrong → doesn't match actual runtime ID | Use `search_config` to find actual runtime ID; update `PackBelong` |
| Card has blank name | Missing `Text/Card/` entry | Create matching Text CSV with same `Id` |
| Game crashes on pack select | CardPack `Icon` path invalid | Check icon file exists at the specified path |
| Lua error on card play | `InitScript` missing `BaseScript` | Add `self.Vars:set_Item("BaseScript", "AttackCardItem")` or `"CommonCardItem"` |
| `self.Self` is nil | Card targeting issue in InitScript | Add nil check: `if self.Self ~= nil then ... end` |
| Character not in selection | Missing RoleData CSV or animation path invalid | Check all Career CSV paths exist |
| Skill card cooldown not working | `SkillTime` key not initialized in SkillScript | Add `if not st:ContainsKey(key) then st:set_Item(key, 0) end` |
| Cross-battle stat lost | Missing `Win`/`Escape` event handlers | Add Save/Load in SkillScript using `SpecialVars` |
| Buff has no icon | Missing `Icon` column in Buff CSV | Add icon path or use a built-in one |
| Relic crashes game | `FightScript` syntax error | Check Lua syntax, ensure correct event names |
| Mod not appearing in mod list | `Enabled: false` or folder name ≠ ModName | Set `"Enabled": true` and ensure folder name matches |
