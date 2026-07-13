# ModDevelopmentFramework & API Analysis — Reference Documentation

## Overview

**Author**: NineTailsCat | **Version**: 1.0.5

This is an **API documentation and mod development framework** mod. It includes decompiled API references extracted from the game's source code, plus a complete sample C# mod project (CatMod) with every supported CSV data type. It does **not** add gameplay content — it is purely a reference tool for mod developers.

## Directory Structure

```
ModDevelopmentFramework&APIAnalysis/
  ModConfig.json                    — Mod metadata (disabled by default — reference only)
  .workshop-id / .workshop-sync.json
  icon.png
  WorkshopUploader.exe - 快捷方式.lnk  — Workshop uploader shortcut
  我的编程参考.md                      — Personal programming reference notes
  我的MOD开发使用指南.md               — Personal mod development usage guide
  工具/                               — Utility scripts
    查看日志.bat / 查看日志.py         — Log viewing scripts
    ASCII 字符编码查看器.py            — ASCII encoding viewer

  API文档说明 v1.0.23816797/          — Core API documentation
    1游戏内API/                       — In-game APIs
      01-战斗系统API.md               — Combat system API
      02-事件系统API.md               — Event system API
      03-工具与辅助API.md             — Utilities & helpers API
      04-配置表字段速查表.md           — Config table field reference
      05-UI系统API.md                 — UI system API
      06-控制台系统API.md             — Console system API
      07-网络同步与多游戏功能API.md    — Network sync & multiplayer API
    2游戏与mod对接/                   — Game-Mod integration
      01-Mod加载机制.md               — Mod loading mechanism
      02-数据配置系统.md              — Data config system (CSV)
      03-Hook系统.md                  — Hook system (Rougamo AOP)
      04-xLua集成.md                  — xLua integration
      05-C# DLL Mod.md               — C# DLL Mod guide
      06-Mod管理器与UI.md            — Mod manager UI
    3mod开发指南/                     — Mod development guide
      01-快速开始.md                  — Quick start
      02-Mod配置文件.md               — Mod config files
      03-配置编写指南.md              — Config writing guide
      04-效果脚本编写.md              — Effect script writing
      05-资源替换与加载.md            — Resource replacement & loading
      06-动画基础.md                  — Animation basics
      07-场景管理.md                  — Scene management
      08-图片来源尺寸规范.md          — Image size conventions
      09-Configuration.json使用指南.md — Configuration.json usage
      10-UI制作指南.md                — UI creation guide
      11-全局特效效果列表.md          — Global effect list
      12-全局技能效果列表.md          — Global skill effect list
      13-全局祝福效果列表.md          — Global blessing effect list
      14-全局Buff效果列表.md          — Global buff effect list
      15-控制台指令表-全局ID列表.md   — Console commands + full ID list

  示例可参考的项目文件夹/             — Sample C# mod project
    CatMod/                          — Full C# mod example
      CatMod.csproj / CatMod.sln     — VS project files
      Entry.cs                       — C# entry point example
      我的MOD文件夹/                  — Complete mod output
        ModConfig.json               — Sample mod config
        Scripts/Entry.lua            — Sample Lua entry (dual Lua + C# example)
        Data/                        — ALL supported CSV types:
          Achievement, Affection, Blessing, Buff, Card, Career, Coin,
          Destiny, Dialogue, Effect, EnchTag, Enemy, EnemyBless, EnemyCard,
          EventList, Food, Hard, HouseDialogue, Item, Level, Map,
          OutSideShop, Partner, PartnerCard, Relic, RoleData, SlotCal,
          SlotReward, Task, Tutorial
        Text/                        — Same structure as Data/ for localization
          (Also includes: Announcement, CardPack, IllustratedBook,
           KeyWordsDic, Narration)
        changelog.txt / icon.png
```

---

## SECTION 1: In-Game APIs (游戏内API)

### 1.1 Combat API (战斗系统API)

Core combat operations available through `ScriptExecutor` (self) in Lua:

**Health & Stats:**
- `self:SetHp(val)`, `self:SetMaxHp(val)` — Set HP values
- `self:ChangeHp(val)`, `self:PureChangeHp(val)` — Modify HP (supports expressions like `"-5"`)
- `self:ChangeMaxHp(val)` — Modify max HP
- `self:ChangeDefence(val)` — Modify block/defense
- `self:SetPower(val)`, `self:ChangePower(val)` — Modify energy
- `self:ChangeMaxPower(val)` — Modify max energy
- `self:ChangeSkill(val)` — Modify skill cooldown
- `self:Resurrection(val)` — Resurrect with specified HP
- `self:AddAction(count)` — Add action count
- `self:DoAction(index)` — Execute specific action
- `self:ChangeRound()` — Change turn

**Buff Operations:**
- `self:AddBuff(buffId, level)` — Add buff by ID
- `self:RemoveBuff(buffId)` — Remove specific buff
- `self:RemoveBadBuff(val, good)` — Remove negative buffs (good="true" = remove positive)
- `self:RemoveAllBadBuff(obj)`, `self:RemoveAllBuff()` — Mass removal
- `self:RunImmediately(buffId, eventName)` — Trigger buff event immediately
- `self:RandomAddBuff(count)`, `self:RandomAddGoodBuff(count, type)`
- `self:RandomAddBuffAndAbility(count)` — Random buff + power

**Card Operations:**
- `self:AddCardById(id)` — Add card instance by ID
- `self:AddCardToDeckById(Id, toUsed)` — Add card to draw/discard pile
- `self:AddFakeCard(toUsed)` — Create temporary fake card
- `self:AddCard(id)` — Simplified add card
- `self:AddCardByData(Id, AddTag)` — Add card from DataConfig ID with optional tag
- `self:AddCardByCardList(count, tag)` — Add from card list by tag
- `self:AddCardByUsedCardList(count, tag)` — Add from used card list
- `self:RandomAddCard(id)` — Random add card
- `self:DrawCount(val)` — Modify draw count
- `self:ShuffleDeck()`, `self:ShuffleHand()` — Shuffle
- `self:ChangeCardTop(val)` — Modify top card value
- `self:GetCardByTag(count, tag)` — Get cards by tag

**Targeting & Status:**
- `self:SetStatus(status)` — Set targeting status (`"Self"`, `"All"`, `"Target"`)
- `self:ForAllStatus(fn)` — Iterate all combatants
- `self:Damage(val, isTrue)` — Deal damage (true damage option)
- `self:GetBuff(buffId)` — Get buff reference
- `self:HasBuff(buffId)` — Check buff existence

### 1.2 Event API (事件系统API)

**Parameterless Events** (via `self:AddEvent(...)` / `self:AddTempEvent(...)`):
- `Attack`, `AttackDone` — Attack hooks
- `AddEnemy` — Enemy joins field
- `CostPower`, `NoPower`, `NoPowerWhenTry`, `AddPower` — Energy hooks
- `Dead`, `BeforeDead`, `OnEnemyDead` — Death hooks
- `Resurrection`, `ResurrectionEnd` — Resurrection hooks
- `EndRound`, `StartRound`, `StartRoundEnd` — Turn hooks
- `ICreateCardItem`, `CreateCardItem`, `EndCreateCardItem` — Card creation
- `Action`, `BurnCard` — Action and burn
- `Init` — Initialization
- `OnDiceCheck`, `OnDiceValue` — Dice system
- `Win`, `PerWin`, `Escape` — Battle end
- `Shuffle`, `OnCameraMove` — Miscellaneous
- `FightStart`, `Hurt`, `Heal`, `AddBuff` — Combat flow
- `SelectCardEnd`, `OnTriggerEffect`, `ScriptExecute` — Advanced
- `NoCard`, `AllDharmas`, `RandomEffect`, `WisdomLevelChange`

**Parameterized Events** (via `AddEvent_xxx`): 7 types for hooking with cancel support

### 1.3 Utilities API (工具与辅助API)

**UIManager**: Singleton UI manager
- `ShowUI<T>(uiName, pureUI)` — Show UI
- `ShowUIAsync<T>()` — Async show (UniTask)
- `GetUI<T>(uiName)` — Get opened UI
- `CloseUI(uiName)`, `HideUI(uiName)`, `RemoveUI(uiName)`
- `CloseAllUI()`, `Find(uiName)`, `GetAllUI()`
- Key UI names: `"FightUI"`, `"TopBarUI"`, `"SettingUI"`, `"EventUI"`, `"WarehouseUI"`

**Other APIs documented**: Save/Load system, AudioManager, Logger

### 1.4 Config Table Field Reference (配置表字段速查表)

Key CSV headers documented for: Card (12 fields), Buff (14 fields), Enemy, Relic, Career, Item, EventList, Dialogue, Map, Level, Hard, CardPack, EnchTag, Partner, PartnerCard, Blessing, RoleData, Coin, Destiny, Affection, Tutorial, Task, Achievement, Food, HouseDialogue, OutSideShop, Effect, SlotCal, SlotReward, IllustratedBook, Announcement, Narration, EnemyBless, EnemyCard, KeyWordsDic, SlotConfig, SlotBuff

### 1.5 UI API (UI系统API)

**UIBase** — Base class for all UI windows:
- `Show()`, `Close()`, `FadeIn()`, `FadeOut(callback)`, `Hide()`
- `OnEnable()`, `DataUpdate()`, `RegisterEvent()`, `ClearEvent()`, `Help()`
- `Register(name)` — Register UI events via `UIEventTrigger`
- `UpperBlock()` / `CancelUpperBlock()` — UI blocking
- `dice` property — Dice roller

### 1.6 Console API (控制台系统API)

**Commands** static class commands:
- `help [arg]` — Get help
- `cls` — Clear console
- `give <type> <ID/name>` — Give items (card, relic, buff, blessing)
- `copy <type> <InstanceID>` — Copy items
- `remove <type> <ID/name/all>` — Remove items
- `god` — God mode
- `gold <amount>` — Add gold
- `kill` — Kill target
- `heal <amount>` — Heal
- `mana <amount>` — Set energy
- `draw <count>` — Draw cards
- `scene <sceneName>` — Load scene
- Plus many more categorized commands

### 1.7 Network API (网络同步与多游戏功能API)

Based on **Mirror** networking library:
- **LobbyManager**: Lobby/session management (Steam integration)
- **GameServer**: Game state sync, role management, save data
- **FightManager**: Combat synchronization
- **PlayerManager**: Player management

---

## SECTION 2: Game-Mod Integration (游戏与mod对接)

### 2.1 Mod Loading Mechanism (Mod加载机制)

**Standard Mod Directory:**
```
GameRoot/Mods/YourModName/
  ModConfig.json             (required)
  Configuration.json         (optional — mod-specific config)
  Scripts/
    Entry.lua                (optional — Lua entry)
    Entry.dll                (optional — C# DLL entry)
  Data/                      (optional — gameplay data CSVs)
  Text/                      (optional — localized text CSVs)
  ModResource/               (optional — custom resources)
```

Only `ModConfig.json` is mandatory.

**ModConfig.json Fields:**
| Field | Type | Description |
|-------|------|-------------|
| `ModName` | string | Display name |
| `ModVersion` | string | Version string |
| `ModAuthor` | string | Author name |
| `ModDescription` | string | Description (JSON-escaped) |
| `IconPath` | string | Icon relative path |
| `Enabled` | bool | Whether enabled |
| `Dependencies` | List<string> | Dependent mod IDs |
| `MustSame` | bool | MP consistency check |

Additional auto-set fields (via `[JsonIgnore]`): `IsWorkshopMod`, `WorkshopPublishedFileId`, `ConfigEnabled`

### 2.2 Data Config System (数据配置系统)

**CSV conventions:**
- UTF-8 encoding (BOM optional)
- Row 1: English field names
- Row 2: Chinese comments (optional, auto-ignored)
- `Id` must be globally unique — implement namespacing
- Empty Id → row skipped
- Newlines in scripts → `\n`
- Comma-separated lists → `;` separator

**ID Generation at runtime:**
Format: `DirectoryName_FileNameWithoutExtension_Id`
Example: `MyMod_mycards_fireball` from `Mods/MyMod/Data/Card/mycards.csv` with `Id=fireball`

### 2.3 Hook System (Hook系统)

Based on **Rougamo** AOP framework (IL weaving at compile time):
- **ModHookRegistry**: Global hook registry, maintains `Before` and `After` dictionaries
- **ModHookContext**: Contains `Target`, `Arguments`
- **Lua API**:
  - `self:AddMethodHookBefore("Type.Method", function(ctx) end)`
  - `self:AddMethodHookAfter("Type.Method", function(ctx) end)`
- typeDotMethod formats: Short (`SettingUI.OnEnable`) or Full (`Witch.UI.SettingUI.OnEnable`)

Hook flow: Original method → Rougamo weaver → check ModHookRegistry.Before[key] → execute Before hooks → execute original → execute After hooks → return

### 2.4 xLua Integration (xLua集成)

xLua is the Lua runtime. Key points:
- **Exposed C# types**: ScriptExecutor, DataConfig, FightManager, UIManager, GameConfigManager, RoleTable, Commands, ModConfig, ModHookContext, ModHookRegistry
- **Unity types**: UnityEngine.Debug, GameObject, Transform, Object, Time, Random
- **.NET types**: List<T>, Dictionary<K,V>, string, int, float, double, bool
- Static access via `CS.TypeName`
- Instance methods via `instance:Method()`
- Property get/set via `instance.Property`
- Indexers via `instance[Index]`
- `self` in ScriptExecutor context = Lua wrapper

### 2.5 C# DLL Mod (C# DLL Mod)

**Project structure:**
- Target framework: .NET Framework 4.72 (net472)
- References to game DLLs: `Witch.dll`, `Witch.Core.dll`, `Assembly-CSharp.dll`
- `<Private>False</Private>` to prevent copying game DLLs
- Post-build event copies to `Mods/YourMod/Scripts/Entry.dll`

**Entry point pattern:**
```csharp
using Witch.Mod;
namespace MyDllMod {
    public static class Entry {
        [ModInitialize]
        public static void Initialize(ModConfig config) {
            // Setup hooks, register events
        }
    }
}
```

The DLL is loaded via `Assembly.Load(byte[])` and scanned for `[ModInitialize]` methods

### 2.6 Mod Manager UI (Mod管理器与UI)

- In-game mod manager accessible from main menu
- Lists local mods + Steam Workshop subscriptions
- Enable/disable toggle per mod (modifies `ModConfig.json` -> game restart to apply)
- Workshop tab with subscription/auto-sync
- Mod icon resolution via `ModConfig.json` `IconPath`

---

## SECTION 3: Mod Development Guide (mod开发指南)

### Key Topics Covered:

1. **Quick Start**: First mod setup, directory creation
2. **Mod Config Files**: ModConfig.json + Configuration.json patterns
3. **Config Writing Guide**: CSV editing best practices
4. **Effect Script Writing**: Lua effect scripting patterns
5. **Resource Loading**: Replacing and loading custom assets
6. **Animation Basics**: AnimationLib structure
7. **Scene Management**: Scene transitions and management
8. **Image Size Conventions**: Required dimensions for different UI elements
9. **Configuration.json Usage**: Runtime config that users can edit
10. **UI Creation Guide**: Building custom UI windows
11. **Global Effect List**: All avalable game effects
12. **Global Skill Effect List**: Skill effects reference
13. **Global Blessing Effect List**: Blessing effects reference
14. **Global Buff Effect List**: All available buffs
15. **Console Command Reference** + **Full ID List**: Complete console commands + all game object IDs

---

## Key Patterns & Techniques Summary

1. **`[ModInitialize]` attribute**: Entry point marker for C# DLL mods
2. **`Assembly.Load(byte[])`**: DLL loading mechanism — mod DLLs loaded as byte arrays
3. **xLua `CS.` prefix**: Access C# namespaces from Lua via `CS.TypeName`
4. **Rougamo AOP**: Compile-time IL weaving for method hooks
5. **CSV row 2 comments**: Chinese-language header comments in row 2 are auto-ignored
6. **Auto-ID namespacing**: Runtime ID = `FolderName_FileName_Id`
7. **Scripts folder**: `Entry.lua` for Lua, `Entry.dll` for C#, or both for hybrid
8. **Mod resource path convention**: `Mods/ModName/ModResource/...`
9. **Event-driven combat API**: `self:AddEvent("EventName", callback)` pattern
10. **Dual-language mod names**: Workshop title uses `中文 | English` format

## Important Reference

This mod contains the **complete decompiled API** of the game at version `v1.0.23816797`, making it an essential reference for any Witch mod developer. The sample CatMod project provides a ready-to-build mod template with all CSV types demonstrated.
