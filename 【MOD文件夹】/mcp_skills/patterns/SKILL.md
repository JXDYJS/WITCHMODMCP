---
name: witch-mod-mcp-mod-patterns
description: "Knowledge base: best practices and patterns for writing Witch mods. Mod structure, CSV formats, Lua entry points, hook usage, animation pipeline, asset specifications, and common solutions. Use when the AI needs to write or debug mod code. Triggers: mod structure, CSV format, Entry.lua, hook, 模组结构, mod编写, mod patterns, 模式, 编写模组."
---

# Mod Patterns — Writing & Understanding Witch Mods

Knowledge base of patterns, conventions, and best practices extracted from analyzing existing mods, decompiled source, and the WAJ-Modder toolkit.

## Finding the Game Installation

To find where the game is installed, use the MCP tool `get_env_info` which returns `activeModules` with `skillPath`:

```
skillPath example:
  ...\Witch's Apocalyptic Journey_Data\Mods\WitchModMCP\mcp_skills

Game root = skillPath parent's parent's parent's parent:
  skillPath → \mcp_skills\ → Mods\ → WitchModMCP\ → Mods\ → _Data\ → Game Root
```

Derive the game root from any `skillPath` by going up 4 directories:
```
gameRoot = Path(skillPath).parent.parent.parent.parent
```

This works regardless of where Steam is installed — no hardcoded paths needed.

If MCP is unreachable, probe common Steam library locations:
```
C:\Program Files\Steam\steamapps\common\Witch*
C:\Program Files (x86)\Steam\steamapps\common\Witch*
D:\Steam\steamapps\common\Witch*
E:\Steam\steamapps\common\Witch*
...etc for each drive letter
```

## 1. Mod Types

Three categories exist:

| Type | Description | Examples |
|------|-------------|----------|
| **Content Mod** | Adds cards, careers, relics, buffs, events | EdictOfStars, SunExp, PW_Mahjong |
| **Plugin Mod** | Modifies game behavior via hooks/reflection | NanaSkillTracker, DeathRetryMod, LogExp |
| **Asset Mod** | Replaces game assets (animations, images) | rdl |

A single mod can combine all three.

## 2. Directory Structure

Standard mod layout:

```
ModName/
├── ModConfig.json           # Required: mod metadata
├── Icon.png                 # Optional: workshop icon
├── Configuration.json       # Optional: user-configurable options
├── .workshop-id             # Steam workshop ID
├── .workshop-sync.json      # Workshop sync metadata
├── Scripts/
│   ├── Entry.lua            # Lua entry point (optional)
│   ├── Entry.dll            # C# entry point (optional)
│   └── Entry.pdb            # Debug symbols (optional)
├── Data/                    # CSV data tables (optional)
│   ├── Card/
│   ├── Buff/
│   ├── Relic/
│   ├── Career/
│   ├── CardPack/
│   ├── RoleData/
│   ├── Partner/
│   ├── PartnerCard/
│   ├── Blessing/
│   ├── EventList/
│   ├── Map/
│   ├── Hard/
│   ├── Enemy/
│   ├── EnemyCard/
│   ├── Level/
│   ├── EnchTag/
│   └── Dialogue/
├── Text/                    # Localization text CSVs (optional)
│   ├── Card/                # (mirrors Data/ structure)
│   ├── Buff/
│   ├── ...
│   └── KeyWordsDic/
├── ModResource/             # Assets (optional)
│   ├── AnimationLib/        # Skill animations
│   ├── Images/              # Card/relic/buff images
│   └── Icon/                # UI icons
└── SharedResources/         # Shared assets across mods (SunExp pattern)
    ├── Audio/
    ├── CG/
    └── Skins/
```

## 3. ModConfig.json Format

```json
{
  "ModName": "MyMod",
  "ModVersion": "1.0.0",
  "ModAuthor": "AuthorName",
  "ModDescription": "Description",
  "IconPath": "Icon.png",
  "Enabled": true,
  "Dependencies": ["OtherMod.AuthorName"],
  "MustSame": true,
  "WorkshopVisibility": "Private",
  "PublishedFileId": ""
}
```

- `ModId` is auto-generated as `ModName + "." + ModAuthor`
- `MustSame` tracks data config changes (triggers recompile)
- `Dependencies` uses ModId for topological sort ordering

## 4. Configuration.json (User Config)

```json
{
  "_readme": "说明文本, displayed in mod manager UI",
  "ExampleBool": true,
  "ExampleNumber": 42,
  "ExampleString": "hello"
}
```

Arbitrary JSON fields via `JsonExtensionData`. Read from Lua:
```lua
local cfg = self.Configuration  -- ModConfigurationData object
print(cfg.ExampleString)
```

## 5. CSV Data Format

### Standard CSV Structure

```
Id,Name_zh-Hans,Name_zh-Hant,Name_en,Name_ja,Col1,Col2,ScriptCol
# 第二行是注释行, auto-ignored
1001,名称1,名稱1,Name1,名前1,val1,val2,lua_code_here
1002,名称2,名稱2,Name2,名前2,val3,val4,lua_code_here
```

Key rules:
- **Row 2** is ignored (comment row)
- **UTF-8** encoding
- **Id** column is always required and must be unique within file
- **Name/Description** columns: 4 languages = zh-Hans, zh-Hant, en, ja
- **Script columns**: any column with "Script" in name is Lua code
- **Text CSVs** mirror Data CSVs structure, provide localized text
- **Runtime ID**: `{ModFolder}_{CsvFileName}_{RawId}`

### Card CSV Columns (common fields)

| Column | Type | Description |
|--------|------|-------------|
| `Id` | int | Unique card ID |
| `Name_{lang}` | string | Card name |
| `Description_{lang}` | string | Card effect description, supports `{0}~{3}` for DesVal1-4 |
| `Cost` | int | Energy cost |
| `CardType` | enum | `Attack`, `Skill`, `Power`, `Curse`, `Status` |
| `TargetType` | enum | `enemy`, `allEnemy`, `self`, `all`, `randomEnemy` |
| `DamageType` | enum | `physical`, `magical`, `true` |
| `Damage` | int | Base damage |
| `Defend` | int | Shield/block value |
| `Magic` | int | Magic damage |
| `Heal` | int | Healing value |
| `Buff` | string | Buff(s) applied, format: `buff_id,level` |
| `SelfBuff` | string | Buff(s) applied to self |
| `Exhaust` | bool | Whether card exhausts after use |
| `Ethereal` | bool | Whether card is ethereal (discards at turn end) |
| `Rarity` | enum | `common`, `uncommon`, `rare`, `special` |
| `PackBelong` | string | Which card pack this belongs to |
| `InitScript` | string | Lua run on card initialization (sets DesVal1-4) |
| `UseScript` | string | Lua run when card is played |
| `UpgradeScript` | string | Lua run when card is upgraded |
| `TriggerScript` | string | Lua for trigger effects |
| `ConditionScript` | string | Lua condition for card playability |
| `Icon` | string | Icon image path (no extension) |

### Buff CSV Columns

| Column | Type | Description |
|--------|------|-------------|
| `Id` | int | Unique buff ID |
| `Name_{lang}` / `Description_{lang}` | string | Localized text |
| `Type` | enum | `buff`, `debuff`, `neutral` |
| `MaxLayer` | int | Maximum stack count |
| `isClear` | bool | Whether it clears at turn end |
| `isDispel` | bool | Whether it is dispellable |
| `Icon` | string | Icon name (31×31 PNG in ModResource/Icon/) |
| `InitScript` | string | Lua on buff application |
| `UseScript` | string | Lua on buff tick |
| `Duration` | int | Turns duration |
| `LinkScript` | string | Lua linking to another buff |

### Career CSV Columns

| Column | Type | Description |
|--------|------|-------------|
| `Id` | int | Career ID |
| `Name_{lang}` / `Description_{lang}` | string | Localized |
| `SanMax` | int | Max SAN |
| `HpMax` | int | Max HP |
| `RoleDataId` | int | Role data reference |
| `CardAsset` | string | Card back image |
| `CardList` | string | Starting card IDs (comma-separated) |
| `RelicList` | string | Starting relic IDs |
| `PartnerList` | string | Starting partner IDs |
| `Attribute` | string | Attribute template |
| `PackBelong` | string | Card pack ownership |

## 6. Lua Entry Point Pattern

### Minimal Entry.lua

```lua
-- No inheriting from ModConfig, just use self
function Setup(mod)
    -- mod is the ModConfig C# object
    mod:AddMethodHookBefore("FightManager.StartPlayerTurn", function(ctx)
        -- ctx.Target = FightManager instance
        -- ctx.Arguments = method parameters
    end)
end
```

### InitScript / UseScript Pattern

Lua code in CSV Script columns executes in a sandboxed context. Available globals:

```lua
-- Provided by ScriptExecutor:
self              -- DataConfig instance for this card/buff
self.data         -- Raw config data dict
self.Vars         -- Runtime variables (DesVal1-4, ThisCount, etc.)

-- Utility functions (from ScriptExecutor):
math.random()     -- RNG
Dice.Roll()       -- Game's dice system
RoleTable.Inst    -- Player data
StatusManager     -- Buff/status manager

-- Common patterns:
-- Set description value for {0} placeholder:
self.Vars.DesVal1 = tostring(amount)
-- Access config fields:
local dmg = self.data["Damage"]
-- Apply buff:
StatusManager:AddStatus("buff_id", level, source, target)
```

### Persistent Mod Data

```lua
-- SkillTime: persists across battles (used for cooldowns)
-- SpecialVars: persists across entire run
-- Access via self.Vars or ScriptExecutor globals
```

## 7. Hook Points (Common Targets)

From decompiled source analysis, these types/methods are commonly hooked:

| Hook Target | Phase | Use Case |
|-------------|-------|----------|
| `FightManager.StartPlayerTurn` | Before/After | Per-turn setup, modify turn state |
| `FightManager.EndPlayerTurn` | Before/After | End-of-turn effects |
| `FightManager.StartEnemyTurn` | Before/After | Enemy behavior modification |
| `FightManager.OnFightStart` | After | Fight initialization |
| `FightManager.OnFightEnd` | Before/After | Cleanup, rewards |
| `RoleTable.TakeDamage` | Before/After | Damage modification |
| `RoleTable.Heal` | Before/After | Healing modification |
| `RoleTable.GainMoney` | Before/After | Economy modification |
| `CardItem.OnUse` | Before/After | Card effect interception |
| `BuffItem.OnApply` | Before/After | Buff application |
| `BuffItem.OnRemove` | Before/After | Buff removal |
| `StatusManager.AddStatus` | Before | Status add interception |
| `MapManager.OnEnterNode` | After | Map navigation hooks |
| `LobbyManager.OnCareerSelected` | After | Career selection hooks |

## 8. Animation & Asset Pipeline

### AnimationLib Structure

```
AnimationLib/
└── anim_name/
    ├── config.json
    ├── frame_0.png
    ├── frame_1.png
    ├── ...
    └── frame_N.png
```

**config.json:**
```json
{
  "AnimationPerFrame": 0.1,
  "isLoop": true,
  "Direction": "row"
}
```

- Frame dimensions: 300×300 (skill animations)
- `AnimationPerFrame`: seconds per frame
- `isLoop`: whether it loops
- `Direction`: sprite layout direction

### Image Specifications

| Asset Type | Size | Notes |
|------------|------|-------|
| Buff icon | **31×31** | PNG, in `ModResource/Icon/` |
| Relic icon | **128×128** | Framed PNG |
| Card art | Variable | In `ModResource/Images/` |
| Card pack cover | 300×440 | Outer frame + silhouette layer |
| Skill animation frame | 300×300 | PNG frame strip |

### Resource Redirection (Asset Swap Pattern)

```lua
-- In Entry.lua:
self:RedirectSourcePath("original/path", "mod/path")
```

Used by rdl mod to replace game animations without modifying Data CSV files.

## 9. Mod Development Patterns

### Pattern: Buff-as-Resource (MP System)

Many mods implement secondary resources using buffs with custom logic:
- BlackMage: "冰火雷" elemental stacks tracked as buffs
- PW_Mahjong: Mahjong tiles tracked as buffs with mahjong-data encoded in buff levels
- Mokou: Fuel system tracked as buff stack count

```lua
-- Check buff stack count:
local fuel = StatusManager:GetStatus("fuel_buff")
if fuel and fuel >= 3 then
    -- Transform card
end

-- Add a buff (resource):
StatusManager:AddStatus("element_fire", 1, source, target)
```

### Pattern: Cooldown Tracking

Use `SkillTime` (persistent variable) for per-combat cooldowns:

```lua
if self.Vars.SkillTime == nil then
    self.Vars.SkillTime = 0
end
-- Increment once per turn:
self.Vars.SkillTime = self.Vars.SkillTime + 1
if self.Vars.SkillTime >= cooldown then
    -- Skill is ready
end
```

### Pattern: Card Transformation (Mokou)

```lua
-- Replace card in hand with another:
local cardId = self.data["Id"]
if cardId == "Mokou_Card_101" then
    -- Transform to another card
    StatusManager:RemoveStatus("fuel_buff", 3, source)
    -- Add new card to hand
    FightManager.Inst:FightAddCard("Mokou_Card_102")
end
```

### Pattern: Dice Check (Plantago)

```lua
local roll = Dice.Roll()
if roll >= threshold then
    -- Success path
else
    -- Failure path
end
```

### Pattern: Milestone System (Muga)

```lua
-- Track usage count across battles via SpecialVars
if SpecialVars["muga_counter"] == nil then
    SpecialVars["muga_counter"] = 0
end
SpecialVars["muga_counter"] = SpecialVars["muga_counter"] + 1

if SpecialVars["muga_counter"] >= 5 then
    -- Trigger milestone effect
    SpecialVars["muga_counter"] = 0
end
```

### Pattern: Asset-Only Mod (rdl)

Skip Entry.lua logic entirely, use `RedirectSourcePath`:
```lua
function Setup(mod)
    mod:RedirectSourcePath("Characters/Default", "rdl/Characters/MyChar")
end
```

### Pattern: Empty Entry (Plantago, Nana)

Entry.lua has no `Setup` function, or `Setup` does nothing:
```lua
-- All logic is in CSV script columns or DLL
```

### Pattern: C# DLL + Lua Hybrid

For complex logic that can't be done in CSV:
- C# DLL handles heavy lifting (file I/O, reflection, custom UI)
- Lua Entry.lua calls DLL methods
- CSV script columns handle card/buff effects
- Communication via `LuaCallCSharp` attributes

### Pattern: Python Code Generation

Large numbers of cards can be generated from templates:
```python
# Generate 240+ mahjong cards from combinations
for suit in ["wan", "tiao", "tong"]:
    for num in range(1, 10):
        generate_card(suit, num)
```

## 10. Text & Localization

- 4 languages: zh-Hans (简体), zh-Hant (繁體), en (English), ja (日本語)
- Text CSVs mirror Data CSVs with localized Name/Description columns
- `KeyWordsDic/` CSV provides keyword tooltip dictionary
- Description text uses `{0}`-`{3}` for `DesVal1`-`DesVal4` values
- Buff references in text: `{buff_buffId}` for tooltip links
- Card keyword auto-generated: `BuffKeyword_{Id}`, `CardKeyword_{Id}`, `EnchTag_{Id}`

## 11. Registry JSON Pattern (SunExp)

SunExp uses JSON registry files for extending non-CSV systems:

| Registry File | Purpose |
|---------------|---------|
| `audio.registry.json` | Custom audio/BGM |
| `companion.intent.registry.json` | Companion AI behavior |
| `visual.registry.json` | Custom visual effects |
| `cg.registry.json` | CG images |
| `starterdeck.registry.json` | Custom starter decks |
| `familiar.blessing.registry.json` | Familiar blessings |
| `endless_abyss.config.json` | Endless mode config |
| `polymorph.role-crops.json` | Polymorph system |

## 12. Key Modding Conventions

1. **File naming**: Data CSV files should match this pattern: `{FileName}.csv` where FileName corresponds to the config type
2. **ID spacing**: Leave gaps between IDs for future additions. Mod content IDs should avoid conflicting with game IDs (1-5000 reserved)
3. **PackBelong**: Cards/relics/buffs must declare which card pack they belong to via `PackBelong` column
4. **Locked content**: `LockedIds` in GameConfigManager prevents IDs from appearing in normal gameplay
5. **Icon paths**: No file extension in CSV Icon columns; game appends `.png`
6. **CSV comments**: Use `#` prefix in row 2 for column descriptions
7. **DLL debugging**: Include `.pdb` files in Scripts/ for stack traces

## 13. When to Use Lua vs C#

### Use Lua (CSV script columns + Entry.lua) when:

- **Content mods**: Adding cards, buffs, relics, careers, card packs, events — all configurable via CSV
- **Simple card effects**: Damage, shield, heal, apply buffs — use `UseScript` / `InitScript` in Card CSV
- **Resource redirection**: `RedirectSourcePath` to replace animations/images
- **Lightweight hooks**: `AddMethodHookBefore`/`AddMethodHookAfter` for simple intercepts
- **No external dependencies**: All game APIs are exposed to Lua via `CS.*` namespace
- **You want rapid iteration**: No compilation needed, just edit CSV/Lua and reload

**Lua Entry.lua template** (correct pattern using colon syntax):
```lua
-- Entry.lua — Lua mod entry point
-- "ModConfig:Setup" means self = ModConfig
function ModConfig:Setup()
    -- self is the ModConfig C# object
    self:RedirectSourcePath("AnimationLib/SomeChar/Idle", "Mods/MyMod/ModResource/AnimationLib/MyChar/Idle")
    self:AddMethodHookBefore("SettingUI.OnEnable", function(ctx)
        CS.UnityEngine.Debug.Log("[MyMod] SettingUI opened")
    end)
end
```

**CSV card script example** (columns with "Script" are Lua):

| Column | Lua code |
|--------|----------|
| `InitScript` | `self.Vars.DesVal1 = tostring(6)` — sets `{0}` placeholder to 6 |
| `UseScript` | `self:AddBuff(DataId.buff_bleeding, "5")` — applies 5 bleed |
| `ConditionScript` | `return RoleTable.Inst.MaxSan > 50` — only playable if SAN > 50 |

### Use C# (Entry.dll) when:

- **Complex game logic**: Multi-step interactions, async operations, file I/O
- **Custom UI**: Creating runtime Canvas elements, custom windows, in-game overlays
- **Reflection-heavy code**: Analyzing game assemblies, dynamic method invocation
- **External library integration**: HTTP requests, JSON parsing, database access
- **Performance-critical code**: Hot paths that execute every frame or per-card-play
- **The hook target method doesn't exist** in the xLua export list

**C# Entry.dll template:**

Full project structure:
```
MyDllMod/
├── ModConfig.json
├── Icon.png
├── Dev/
│   ├── MyDllMod.csproj        ← Visual Studio / dotnet project
│   ├── MyDllMod.sln           ← Solution file
│   └── Entry.cs               ← Source code
└── Scripts/
    └── Entry.dll              ← Compiled output (copy from Dev/bin/)
```

`ModConfig.json`:
```json
{
  "ModName": "MyDllMod",
  "ModVersion": "1.0",
  "ModAuthor": "AuthorName",
  "ModDescription": "C# DLL Example",
  "IconPath": "Icon.png",
  "Enabled": true,
  "Dependencies": null
}
```

`.csproj` file:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <RootNamespace>MyDllMod</RootNamespace>
    <!-- MUST match ModConfig.json as ModName.ModAuthor to avoid conflicts -->
    <AssemblyName>MyDllMod.AuthorName</AssemblyName>
    <LangVersion>latest</LangVersion>
    <!-- CHANGE THIS to your game install path -->
    <!-- e.g. <GamePath>D:\Steam\steamapps\common\Witch's Apocalyptic Journey</GamePath> -->
    <GamePath>CHANGE_ME_TO_YOUR_GAME_PATH</GamePath>
    <DllPath>$(GamePath)\Witch's Apocalyptic Journey_Data\Managed</DllPath>
    <ProduceReferenceAssembly>False</ProduceReferenceAssembly>
    <GenerateDocumentationFile>False</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(DllPath)\UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Witch">
      <HintPath>$(DllPath)\Witch.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Witch.Core">
      <HintPath>$(DllPath)\Witch.Core.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

`Entry.cs` source:
```csharp
using Witch.Mod;
using Witch.UI.Window;
using UnityEngine;

namespace MyDllMod;

public static class Entry
{
    [ModInitialize]
    public static void Init(ModConfig modConfig)
    {
        Commands.Log("MyDllMod", "Mod loaded!");
    }
}

// Hook classes must be in the same assembly
public static class MyHooks
{
    // HookBefore: runs BEFORE the original method
    [HookBefore(typeof(SettingUI), nameof(SettingUI.OnEnable))]
    public static void OnSettingOpen(SettingUI __instance)
    {
        Commands.Log("MyDllMod", "Settings opened");
    }

    // HookAfter: runs AFTER the original method
    [HookAfter(typeof(FightManager), nameof(FightManager.StartPlayerTurn))]
    public static void OnPlayerTurnStart(FightManager __instance)
    {
        Commands.Log("MyDllMod", "Player turn started");
    }

    // With parameters: match the original method's parameter order
    [HookBefore(typeof(RoleTable), nameof(RoleTable.TakeDamage))]
    public static void BeforeDamage(RoleTable __instance, object[] args)
    {
        // args[0] = damage amount
        Commands.Log("MyDllMod", $"Damage incoming: {args[0]}");
    }
}
```

**Build and deploy:**
```bash
# 1. Build the DLL
dotnet build Dev/MyDllMod.csproj -c Release

# 2. Copy to mod Scripts/ folder
copy Dev\bin\Release\net472\MyDllMod.AuthorName.dll Scripts\Entry.dll
# (Optional) copy .pdb for stack traces
copy Dev\bin\Release\net472\MyDllMod.AuthorName.pdb Scripts\Entry.pdb
```

> **IMPORTANT**: The DLL file must be named `Entry.dll` in the `Scripts/` folder, but its internal assembly name must be `ModName.ModAuthor` (not `Entry`) to avoid conflicts with other DLL mods.

## 14. End-to-End Walkthrough: Create a Mod From Template

This walkthrough creates a mod named "MyFirstMod" that adds a custom card, starting from the tutorial template.

### Step 1: Get the template

Clone the mod tutorial repository to get the official templates:
```bash
git clone https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git
```

The repo contains:
| Path | What it is |
|------|-----------|
| `ModTemplate/` | **Lua mod template** — full directory structure with sample CSVs for every data type, Entry.lua, Lib/ type hints, and all Text/ folders |
| `DllTemplate/` | **C# DLL mod template** — complete Visual Studio project (.csproj + .sln + Entry.cs), pre-built Entry.dll, and ModConfig.json |
| `Example/Defect/` | **Completed example mod** — a playable Slay the Spire "Defect" character with cards, animations, and career config |

### Step 2: Copy and rename

```bash
# For a Lua mod:
copy ModTemplate to your mods folder, rename to MyFirstMod

# For a C# DLL mod:
copy DllTemplate to your mods folder, rename to MyFirstMod
```

The game's mods directory is (resolve `{GAME_INSTALL_DIR}` from `skillPath` as described above):
```
{GAME_INSTALL_DIR}\Witch's Apocalyptic Journey_Data\Mods\
```

Final structure for this tutorial (Lua mod):
```
Mods\MyFirstMod\
├── ModConfig.json
├── Icon.png
├── Scripts\
│   ├── Entry.lua
│   ├── ScriptSample.lua       (reference, can delete)
│   └── Lib\                   (type hints, keep for EmmyLua)
├── Data\                      (keep only the folders you need)
│   ├── Card\
│   │   └── mycards.csv
│   └── CardPack\
│       └── mycardpack.csv
└── Text\
    ├── Card\
    │   └── mycards.csv
    └── CardPack\
        └── mycardpack.csv
```

### Step 3: Write ModConfig.json

```json
{
  "ModName": "MyFirstMod",
  "ModVersion": "1.0",
  "ModAuthor": "MyName",
  "ModDescription": "My first mod!",
  "IconPath": "Icon.png",
  "Enabled": true,
  "Dependencies": null
}
```

> `ModId` = `ModName.ModAuthor` = `"MyFirstMod.MyName"`
> Keep `ModName` and folder name consistent.

### Step 3: Write Data Card CSV (`Data/Card/mycards.csv`)

```csv
Id,Rarity,Cost,CardType,TargetType,DamageType,Damage,Defend,Buff,SelfBuff,Exhaust,Icon,BaseScript,PackBelong,InitScript,UseScript
# 唯一标识,稀有度,费用,类型,目标类型,伤害类型,伤害,护盾,Buff,自身Buff,是否消耗,图标路径,脚本类型,所属卡包,初始化脚本,使用脚本
1001,common,1,Attack,enemy,physical,6,0,,,false,icon_mycard,AttackCardItem,pack_mycardpack,self.Vars.DesVal1=tostring(6),self:AddBuff(DataId.buff_bleeding,"3")
```

Column breakdown:
- `Id=1001` — unique ID within this CSV
- `Rarity=common` — `common`/`uncommon`/`rare`/`special`
- `Cost=1` — energy cost
- `CardType=Attack` — `Attack`/`Skill`/`Power`/`Curse`/`Status`
- `TargetType=enemy` — target type
- `DamageType=physical` — `physical`/`magical`/`true`
- `Damage=6` — base damage
- `Defend=0` — shield value
- `Buff=` — buffs applied to target (format: `buff_id,level`)
- `SelfBuff=` — buffs applied to self
- `Exhaust=false` — whether card is consumed on use
- `Icon=icon_mycard` — icon filename (no extension)
- `BaseScript=AttackCardItem` — `AttackCardItem` (targetable) or `CommonCardItem` (no target)
- `PackBelong=pack_mycardpack` — which card pack this belongs to
- `InitScript` — runs at initialization: sets `{0}` placeholder to 6
- `UseScript` — runs when played: applies 3 bleed stacks

### Step 4: Write Text Card CSV (`Text/Card/mycards.csv`)

```csv
Id,Type,Name,Name_en,Name_zh-Hant,Name_ja,Description,Description_en,Description_zh-Hant,Description_ja
# 唯一标识,类型,名称,英文名,繁中名,日文名,描述,英文描述,繁中描述,日文描述
1001,Card,My Strike,My Strike,我的打击,マイストライク,Deal {0} damage. Apply 3 bleed.,Deal {0} damage. Apply 3 bleed.,造成 {0} 点伤害。施加 3 层流血。,{0}ダメージを与える。出血を3付与する。
```

> The `{0}` in Description is replaced by `DesVal1` (set in `InitScript` = 6).

### Step 5: Write Card Pack CSV (`Data/CardPack/mycardpack.csv`)

```csv
Id,Name,Description,CardList
# 唯一标识,卡包名,描述,包含卡牌列表
pack_mycardpack,My Pack,Contains my custom card,1001
```

### Step 6: Write Card Pack Text (`Text/CardPack/mycardpack.csv`)

```csv
Id,Name,Name_en,Name_zh-Hant,Name_ja,Description,Description_en,Description_zh-Hant,Description_ja
# 唯一标识,名称,英文名,繁中名,日文名,描述,英文描述,繁中描述,日文描述
pack_mycardpack,My Pack,My Pack,我的卡包,マイパック,A pack with my custom card,A pack with my custom card,包含我的自定义卡牌,マイカスタムカードが含まれています
```

### Step 7: Write Entry.lua (`Scripts/Entry.lua`)

```lua
function ModConfig:Setup()
    CS.UnityEngine.Debug.Log("[MyFirstMod] Mod loaded!")
end
```

### Step 8: Test with MCP tools

Enable the mod in-game (Mod Manager UI), then:

```python
# 1. Verify mod loaded
state = g.call("dump_mod_state")
for m in state['mods']:
    print(m['assemblyName'])

# 2. Check card config exists
config = g.call("query_config", {"tableName": "CardConfig", "id": 1001})
print(config['item'])

# 3. Give card to player
g.call("give_item", {"type": "card", "value": "1001"})

# 4. Start a fight and test
g.call("load_scene", {"type": "fakefight"})
fight = g.call("get_fight_state")
```

### Step 9: Add icon

Create `ModResource/Icon/icon_mycard.png` (31×31 for buff icons; card art can be larger in `ModResource/Images/`).

### Step 10: Publish to Steam Workshop

Use the upload tool at:
```
{GAME_INSTALL_DIR}\Witch's Apocalyptic Journey_Data\StreamingAssets\Mod Upload Tool\WorkshopUploader.exe
```

Before uploading, add to `ModConfig.json`:
```json
{
  "WorkshopVisibility": "Private",
  "PublishedFileId": ""
}
```

First upload → tool writes `PublishedFileId` back. Subsequent uploads update the same workshop item.

## 15. ScriptExecutor API Reference

These methods are available in CSV Script columns (InitScript, UseScript, etc.) via `self`.

### Status / Buff Methods

```lua
-- Apply buff to target: self:AddBuff(buffId, level)
self:AddBuff(DataId.buff_bleeding, "5")

-- Remove buff from target
self:RemoveBuff(DataId.buff_bleeding)

-- Trigger buff effect immediately
self:RunImmediately(DataId.buff_bleeding, "OnLevelChange")

-- Set effect scope (call before status effects)
self:SetStatus("Self")              -- self only
self:SetStatus("Target")            -- current target
self:SetStatus("All")               -- all units
self:SetStatus("AllFriend")         -- all friendly
self:SetStatus("AllEnemy")          -- all enemies
self:SetStatus("AllRandomEnemy2")   -- 2 random enemies
self:SetStatus("AllRandomFriend1")  -- 1 random friend
```

### Card Methods

```lua
-- Add card to hand by cardListId and cardId
self:AddCardByCardList("1", "CardId_Here")

-- Play animation action N times
for i = 1, 10 do
    self:DoAction(i)
end

-- Trigger an action event
self:EventTrigger("Action")
```

### Player / Resource Methods

```lua
-- Change player money
self:ChangeMoney(amount)

-- Give a blessing
ScriptExecutor.PlayerInfo.AddBless(DataId.blessing_1)

-- Access player data
ScriptExecutor.PlayerInfo  -- PlayerInfo object
```

### Event System

```lua
-- Add event listener for fight events
self:AddEvent("Action", function(fromdata)
    -- fromdata.data.scriptExecutor:RunScript("UseScript")
end)

self:AddEvent("Hurt", function(fromdata)
    self:ChangeMoney(fromdata.val)
end)

-- Parameterized event types:
self:AddEvent_HurtData("Hurt", function(hurtData)
    -- hurtData contains damage info
end)

self:AddEvent_ActionData("Action", function(actionData)
    -- actionData contains action info
end)

self:AddEvent_NewEnemyData("AddEnemy", function(enemyData)
    -- enemyData contains new enemy info
end)

self:AddEvent_DamageData("Damage", function(damageData)
    -- damageData contains detailed damage info
end)
```

### Description Placeholders

```lua
-- In InitScript, set description values for {0}~{3}
self.Vars.DesVal1 = tostring(6)  -- replaces {0}
self.Vars.DesVal2 = tostring(3)  -- replaces {1}
self.Vars.DesVal3 = tostring(2)  -- replaces {2}
self.Vars.DesVal4 = tostring(1)  -- replaces {3}
```

### xLua Limitations

```lua
-- CANNOT use [] to access dictionaries; use get_Item / set_Item instead
local val = myDict:get_Item("key")
myDict:set_Item("key", "value")

-- Use CS. prefix for C# types
CS.UnityEngine.Debug.Log("message")
CS.Commands.Log("Tag", "message")
```

## 16. Fight Event System

These events can be listened to via `self:AddEvent("EventName", handler)` in card/buff scripts:

| Event Name | Description |
|-----------|-------------|
| `Attack` | Attack event |
| `AddEnemy` | New enemy added |
| `AttackDone` | Attack completed |
| `CostPower` | Energy consumed |
| `NoPower` | Insufficient energy |
| `AddPower` | Energy gained |
| `Dead` | Unit death |
| `ToughCountZero` | Toughness reaches zero |
| `OnEnemyDead` | Enemy death |
| `Resurrection` | Unit revived |
| `EndRound` | Round ended |
| `ICreateCardItem` | Card item creation (fires N times) |
| `CreateCardItem` | Card item created |
| `EndCreateCardItem` | Card item creation finished |
| `NoPowerWhenTry` | Energy insufficient when trying to play |
| `Action` | Action executed |
| `BurnCard` | Card burned |
| `Init` | Fight initialization |
| `OnDiceCheck` | Dice roll check |
| `OnDiceValue` | Dice roll value |
| `Win` | Fight won |
| `Escape` | Fight escaped |
| `StartRound` | Round started |
| `Shuffle` | Deck shuffled |
| `OnCameraMove` | Camera moved |
| `FightStart` | Fight started |
| `Hurt` | Damage taken |
| `Heal` | Healing received |
| `SelectCardEnd` | Card selection ended |
| `OnTriggerEffect` | Effect triggered |
| `ScriptExecute` | ScriptExecutor executed |

### Global Events (Non-Fight)

These use `EventCenter` instead of `ScriptExecutor.AddEvent`:

| Event | Description |
|-------|-------------|
| `UIOpen-{Name}` | UI opened (concat with name, e.g. `UIOpen-SettingUI`) |
| `UIHelp` | UI help requested |
| `UIClose-{Name}` | UI closed |
| `LanguageChange` | Language switched |

### RoleTable Events

`RoleTable` implements `INotifyPropertyChanged`, so you can listen for property changes:
```lua
RoleTable.Inst.PropertyChanged:Add(function(sender, args)
    if args.PropertyName == "Money" then
        -- money changed
    end
end)
```

## 17. Complete Card CSV Column Reference

All columns available for Card CSV (`Data/Card/*.csv`):

| Column | Type | Required | Description |
|--------|------|----------|-------------|
| `Id` | int | Yes | Unique ID within file |
| `Rarity` | enum | Yes | `common`, `uncommon`, `rare`, `special` |
| `Cost` | int | Yes | Energy cost |
| `CardType` | enum | Yes | `Attack`, `Skill`, `Power`, `Curse`, `Status` |
| `TargetType` | enum | For attacks | `enemy`, `allEnemy`, `self`, `all`, `randomEnemy` |
| `DamageType` | enum | For attacks | `physical`, `magical`, `true` |
| `Damage` | int | No | Base damage |
| `Defend` | int | No | Shield/block |
| `Magic` | int | No | Magic damage |
| `Heal` | int | No | Healing |
| `Buff` | string | No | Buff applied: `buff_id,level` |
| `SelfBuff` | string | No | Self buff: `buff_id,level` |
| `Exhaust` | bool | No | Card consumed on use |
| `Ethereal` | bool | No | Discards at turn end |
| `Expend` | int | No | Cards to expend (sacrifice) |
| `Icon` | string | No | Icon path (no `.png`) |
| `BaseScript` | string | **Yes** | `AttackCardItem` (targetable) or `CommonCardItem` (no target) |
| `PackBelong` | string | **Yes** | Card pack ID this belongs to |
| `Tag` | string | No | Comma-separated tags |
| `InitScript` | string | No | Lua: runs at init (set DesVal1-4) |
| `DrawScript` | string | No | Lua: runs when drawn |
| `UseScript` | string | No | Lua: runs when played |
| `DropScript` | string | No | Lua: runs when discarded |
| `UpgradeScript` | string | No | Lua: runs when upgraded |
| `TriggerScript` | string | No | Lua: trigger condition |
| `ConditionScript` | string | No | Lua: playability condition |
| `Effects` | string | No | Visual effect path |
| `Action` | string | No | Animation action |
| `SoundEffects` | string | No | Sound effect |

## 18. Mod Validation Checklist

Before publishing a mod, verify:

### ModConfig.json
- [ ] `ModName` matches folder name
- [ ] `ModAuthor` is filled
- [ ] `ModVersion` is set
- [ ] `Enabled` is `true`
- [ ] `Dependencies` list uses correct `ModName.ModAuthor` format (if any)
- [ ] `WorkshopVisibility` is set (for publishing)

### Data CSV Files
- [ ] Row 1 = header (column names)
- [ ] Row 2 = comments starting with `#` (field descriptions)
- [ ] `Id` column values are unique within file
- [ ] IDs don't conflict with game's reserved range (1-5000)
- [ ] `PackBelong` is set for cards/relics/buffs
- [ ] `BaseScript` is `AttackCardItem` or `CommonCardItem` for cards
- [ ] CSV saved as **UTF-8** encoding
- [ ] Icon paths have no `.png` extension
- [ ] Script columns contain valid Lua code
- [ ] Lua code uses `self:` (colon) not `self.` (dot) for method calls

### Text CSV Files
- [ ] Structure mirrors Data CSV
- [ ] `Name` and `Description` have localized versions (`_en`, `_zh-Hant`, `_ja`)
- [ ] `{0}`-`{3}` placeholders match `DesVal1`-`DesVal4` in `InitScript`

### Assets
- [ ] Buff icons: **31×31** PNG in `ModResource/Icon/`
- [ ] Relic icons: **128×128** framed PNG
- [ ] Skill animations: 300×300 frames in `ModResource/AnimationLib/`
- [ ] `AnimationLib/config.json` has `AnimationPerFrame`, `isLoop`, `Direction`

### Entry Files
- [ ] `Scripts/Entry.lua` uses `function ModConfig:Setup()` (colon syntax)
- [ ] For C# mods: `Scripts/Entry.dll` exists and assembly name is `ModName.ModAuthor`
- [ ] C# `.csproj` targets `net472` and references correct game DLLs

### Runtime Verification (via MCP tools)
- [ ] `dump_mod_state` shows the mod as loaded
- [ ] `get_recent_logs` shows no mod-related errors
- [ ] `query_config` shows your card/buff/relic entries
- [ ] `give_item` can grant your items to the player
- [ ] `load_scene` with `fakefight` lets you play your card
- [ ] `get_fight_state` shows your card in hand

## 19. Troubleshooting Guide

### Mod Not Loading

| Symptom | Check |
|---------|-------|
| Mod not in `dump_mod_state` | `ModConfig.json Enabled=false` → set to `true` |
| `get_recent_logs` shows "ModConfig.json parse failed" | JSON syntax error → validate JSON |
| `get_recent_logs` shows "LuaEnv 不可用" | Game Lua initialization failed → restart game |
| ModId conflict error | Another mod has same `ModName.ModAuthor` → change `ModName` |
| Dependency error | `Dependencies` lists a mod that doesn't exist or is disabled → fix dependency |

### Card/Buff Not Appearing

| Symptom | Check |
|---------|-------|
| `query_config` doesn't show your entry | CSV file not in correct `Data/` subfolder → check `Data/Card/yourfile.csv` |
| Card doesn't appear in game | `PackBelong` not set or card pack not in career selection → verify |
| Card has no name/description | Missing Text CSV → create `Text/Card/yourfile.csv` |
| Card shows as "?" icon | Icon path missing or file not found → check `ModResource/Icon/` |
| Card can't be played | `BaseScript` not set → must be `AttackCardItem` or `CommonCardItem` |
| Card costs 0 energy but shows wrong cost | `Cost` column missing or wrong type → check CSV |

### Lua Script Errors

| Symptom | Check |
|---------|-------|
| `get_recent_logs` shows Lua compile error | Script column has syntax error → check Lua syntax |
| Script column doesn't execute | Column name doesn't contain "Script" → rename column |
| `self:AddBuff` doesn't work | Should be `self:AddBuff(id, level)` → verify parameters |
| Dictionary access fails | xLua doesn't support `dict[key]` → use `dict:get_Item(key)` |
| `DataId.xxx` not found | Wrong ID name → use `query_config` to find correct ID |
| `CS.xxx` is nil | Type not exported to Lua → use `inspect` to check available types |

### Hook Problems

| Symptom | Check |
|---------|-------|
| Hook never fires | Method name wrong or type not exported → check spelling |
| Hook causes crash | Hook method signature mismatch → match parameter types |
| Hook fires but args are wrong | `ctx.Arguments` order differs → check decompiled source |
| `[HookBefore]` doesn't work | Not using proper attribute → verify `[HookBefore(typeof(X), "Y")]` |

### C# DLL Issues

| Symptom | Check |
|---------|-------|
| DLL not loading | Assembly name is "Entry" → change to `ModName.ModAuthor` |
| DLL loads but hooks don't fire | `[HookBefore]` attribute requires correct type/method names |
| Build fails | `.csproj` GamePath wrong → update path to your game install |
| Type or member not found | Game version mismatch → check game version with `Globals.VersionString` |
| Assembly conflicts | Another DLL mod has same assembly name → ensure unique `AssemblyName` |

### Quick Diagnostic Commands

```python
# 1. Check if mod is loaded
state = g.call("dump_mod_state")
print(state['modCount'], "mods loaded")

# 2. Check for errors
logs = g.call("get_recent_logs", {"count": 30})
for entry in logs:
    if 'Error' in entry or 'error' in entry.lower():
        print(entry)

# 3. Verify config tables
cfg = g.call("query_config", {"tableName": "CardConfig", "id": YOUR_ID})
print(cfg)

# 4. Reflect on game objects
inspect = g.call("inspect", {"typeName": "RoleTable", "memberPath": "Instance"})
print(inspect)

# 5. Check scene state
scene = g.call("get_scene_state")
print(scene['page'])
```
