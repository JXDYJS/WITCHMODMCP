# Mod Patterns — Reference

CSV formats, API references, event tables, and hook points for Witch mod development.
Experience guides (directory structure, workflow, troubleshooting) are in `.agents/skills/witchSkill/SKILL.md`.

## CSV Data Format

### Standard CSV Structure

```
Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action
# 第二行是注释行, auto-ignored
1001,1,1,,InitScript_lua_here,,UseScript_lua_here,,Icon/Card/name,,Attack
```

Key rules:
- **Row 2** is ignored (comment row), data starts at Row 3
- **UTF-8** encoding
- **Id** column is always required and must be unique within file
- **Script columns**: any column with "Script" in name is Lua code
- **Text CSVs** do NOT mirror Data CSVs — they carry the localized display columns (`Name`/`Description` + language variants). See "Text CSV Format" below.
- **Runtime ID**: `{ModFolder}_{CsvFileName}_{RawId}`

> ⚠️ **CSV 列名以模板 `Lib/DataConfigs/` 的真实表头为准**，不要在模板外臆造列。游戏按列名（而非顺序）读取，`Cost`/`CardType`/`Damage`/`Defend`/`Magic`/`Heal`/`Buff`/`Exhaust` 等均**不存在**于真实 Card CSV 表头中。

### Card CSV Columns

**File location:** `Data/Card/<filename>.csv`

| Column | Type | Description |
|--------|------|-------------|
| `Id` | string | Unique card ID (raw; runtime ID adds `{ModFolder}_{CsvFileName}_` prefix) |
| `Rarity` | int | Numeric rarity: 1=Common, 2=Uncommon, 3=Rare, 4=Special |
| `Expend` | int | Energy cost |
| `Tag` | string | Card tags (comma-separated): `Retain`, `Burnout`, `Recycle`, `Ascension` |
| `InitScript` | string | Lua run on card initialization (sets `BaseScript` + `DesVal1-4`) |
| `DrawScript` | string | Lua run when the card is drawn |
| `UseScript` | string | Lua run when the card is played (main effect) |
| `DropScript` | string | Lua run when the card is discarded |
| `Icon` | string | Icon image path (no extension) |
| `Effects` | string | Visual effect path (optional) |
| `Action` | string | Card type: `Attack`, `Skill`, or empty |
| `PackBelong` | string | *(optional)* Which card pack this belongs to (runtime ID). **Omit to put the card in the default pool.** |

> **No `Cost` / `CardType` / `TargetType` / `DamageType` / `Damage` / `Defend` / `Magic` / `Heal` / `Buff` / `SelfBuff` / `Exhaust` / `Ethereal` columns exist.** Damage/shield/heal/buff effects are implemented entirely in `UseScript` via `SetStatus` + `Damage` / `ChangeHp` / `ChangeDefence` / `AddBuff` / `AddDescription`. "消耗/虚无" semantics are expressed through `Tag` / card scripts, not dedicated columns.
>
> **`BaseScript` is not a CSV column** — it is set inside `InitScript` via `self.Vars:set_Item("BaseScript", "AttackCardItem"|"CommonCardItem")`.

### Text CSV Format (Card)

**File location:** `Text/Card/<filename>.csv`

Real column header (from template `Text/Card/blood.csv`):
```
Id,是否锁定,Type,Note,Name,Name_en,Name_zh-Hant,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
```

- `Id` must match the Data CSV `Id` (raw)
- `Name` is the Simplified-Chinese name; there is **no `Name_zh-Hans` column** — language variants are `Name`(zh-Hans), `Name_en`, `Name_zh-Hant`, `Name_ja`
- `Description` supports `{0}~{3}` placeholders replaced by `DesVal1-4` (set in `InitScript`), and `{buff_id}` replaced by the buff's display name
- A card without a Text CSV entry has a blank name and is considered incomplete

### Buff CSV Columns

**File location:** `Data/Buff/<filename>.csv`

Real column header (from template `Data/Buff/buff.csv`):
```
Id,InitScript,ApplyScript,ClearScript,ReducePerTurn,ReducePerAttacked,ReducePerUse,UpperBound,Icon,Type,Rarity,Effects,SoundEffects,Action,CanZero
```

| Column | Type | Description |
|--------|------|-------------|
| `Id` | string | Unique buff ID (used as `buff_id` in scripts) |
| `InitScript` | string | Lua on buff initialization (display update) |
| `ApplyScript` | string | Lua triggered when buff is applied (use `self:AddEvent`) |
| `ClearScript` | string | Lua triggered when buff is cleared |
| `ReducePerTurn` | int | Stacks reduced per turn |
| `ReducePerAttacked` | int | Stacks reduced when attacked |
| `ReducePerUse` | int | Stacks reduced on action |
| `UpperBound` | int | Maximum stack count |
| `Icon` | string | Icon image path (no extension) |
| `Type` | string | Buff category — **localized display word** (e.g. `正面`/`负面`/`能力`/`属性`), not an enum keyword |
| `Rarity` | int | Rarity display value |
| `Effects` | string | Visual effect path (optional) |
| `SoundEffects` | string | Sound effect path (optional) |
| `Action` | string | Animation type (optional) |
| `CanZero` | string | Whether stacks may reach zero |

> **No `MaxLayer` / `isClear` / `isDispel` / `UseScript` / `Duration` / `LinkScript` columns exist.** Stack decay is configured by `ReducePerTurn` / `ReducePerAttacked` / `ReducePerUse` (not a boolean `isClear`); lifecycle scripts are `ApplyScript`/`ClearScript` (not `UseScript`).

### Career CSV Columns

**File location:** `Data/Career/<filename>.csv`

Real column header (from template `Data/Career/career.csv`):
```
Id,SanMax,SkillScript,Animation,Vocal,Skill1,Skill2,ChoiceIcon,DollIcon,Character,Avatar,CareerImage,ActionImage1,ActionImage2,Dialogue,EmojiPath,AttackEffect,SkillEffect,HitEffect,DefendEffect
```

| Column | Type | Description |
|--------|------|-------------|
| `Id` | string | Career ID (raw) |
| `SanMax` | int | Max SAN (HP) |
| `SkillScript` | string | Lua — passive skills, event listeners, initialization |
| `Animation` | string | Animation directory path |
| `Vocal` | string | Voice/animation library path |
| `Skill1` | string | **Runtime ID** of first active skill card |
| `Skill2` | string | Runtime ID of second active skill card |
| `ChoiceIcon` | string | Character selection icon path |
| `DollIcon` | string | Doll/animated icon path |
| `Character` | string | Full character art path |
| `Avatar` | string | Portrait/headshot path |
| `CareerImage` | string | Career selection image path |
| `ActionImage1` | string | Skill 1 icon path |
| `ActionImage2` | string | Skill 2 icon path (if has 2 skills) |
| `Dialogue` | string | Dialogue sprite directory path |
| `EmojiPath` | string | Emoji sprite path |
| `AttackEffect` / `SkillEffect` / `HitEffect` / `DefendEffect` | string | Effect names |

> **No `HpMax` / `RoleDataId` / `CardAsset` / `CardList` / `RelicList` / `PartnerList` / `Attribute` / `PackBelong` columns exist.** Starting cards/partners/relics are handled by other mechanisms (skill cards referenced via `Skill1`/`Skill2`, starting buffs via `SkillScript`), not CSV columns.

## Hook Points (Common Targets)

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

## Animation & Asset Pipeline

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
  "Direction": "Right"
}
```

- `AnimationPerFrame`: seconds per frame
- `isLoop`: whether it loops
- `Direction`: sprite layout direction — **`"Right"` / `"Left"`** (real rdl mod uses `"Right"`), not `"row"`

> Frame dimensions vary by animation (real rdl `Attack` uses a **1536×640** sprite sheet, not individual 300×300 frames). Check the actual PNG size for the animation you're replacing.

### Image Specifications

| Asset Type | Size | Notes |
|------------|------|-------|
| Buff icon | **32×32** | PNG, in `ModResource/Icon/Buff/` (e.g. EdictOfStars `Icon/Buff/bloodstain.png`) |
| Relic icon | varies | PNG in `ModResource/Icon/Relic/` — sizes vary per mod (e.g. 55×55); verify your own art |
| Card art | Variable | In `ModResource/Images/` |
| Card pack cover | 300×440 | Outer frame + silhouette layer |
| Skill animation frame | Varies | PNG frame strip — check actual sheet dimensions |

### Resource Redirection (Asset Swap Pattern)

```lua
-- In Entry.lua:
self:RedirectSourcePath("original/path", "mod/path")
```

Used by rdl mod to replace game animations without modifying Data CSV files.

## ScriptExecutor API Reference

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
-- Add card to hand by cardListId and tag filter:
-- AddCardByCardList(string count, string tag = "all")
-- The 2nd argument is a TAG filter over the draw pile (e.g. "Attack" or "all"),
-- NOT a card ID. Example (draw 1 card from the card list with tag "all"):
self:AddCardByCardList("1", "all")

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

-- Give a blessing (static method on PlayerInfo; needs the CS. prefix in xLua)
CS.ScriptExecutor.PlayerInfo.AddBless("blessing_id")

-- Access player data
CS.ScriptExecutor.PlayerInfo  -- PlayerInfo object
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
-- Vars is a C# IDictionary<string,string> — use set_Item, NOT direct property assignment
self.Vars:set_Item("DesVal1", tostring(6))  -- replaces {0}
self.Vars:set_Item("DesVal2", tostring(3))  -- replaces {1}
self.Vars:set_Item("DesVal3", tostring(2))  -- replaces {2}
self.Vars:set_Item("DesVal4", tostring(1))  -- replaces {3}
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

## Fight Event System

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
RoleTable.Instance.PropertyChanged:Add(function(sender, args)
    if args.PropertyName == "Money" then
        -- money changed
    end
end)
```

## Complete Card CSV Column Reference

All columns available for Card CSV (`Data/Card/*.csv`) — verified against template `Lib/DataConfigs/Data/Card/*.csv` headers:

| Column | Type | Required | Description |
|--------|------|----------|-------------|
| `Id` | string | Yes | Unique ID within file (raw; runtime ID adds `{ModFolder}_{CsvFileName}_`) |
| `Rarity` | int | Yes | 1=Common, 2=Uncommon, 3=Rare, 4=Special |
| `Expend` | int | Yes | Energy cost |
| `Tag` | string | No | Comma-separated tags (`Retain`, `Burnout`, `Recycle`, `Ascension`) |
| `InitScript` | string | Yes* | Lua: runs at init — must set `BaseScript` via `self.Vars:set_Item("BaseScript", ...)` |
| `DrawScript` | string | No | Lua: runs when drawn |
| `UseScript` | string | Yes* | Lua: runs when played (main effect) |
| `DropScript` | string | No | Lua: runs when discarded |
| `Icon` | string | No | Icon path (no `.png`) |
| `Effects` | string | No | Visual effect path |
| `Action` | string | No | Card type: `Attack` / `Skill` / empty |
| `PackBelong` | string | No | Card pack runtime ID; **omit → default card pool** |

\* `InitScript`/`UseScript` effectively required for a functional card.

> **Columns that do NOT exist:** `Cost` (use `Expend`), `CardType`, `TargetType`, `DamageType`, `Damage`, `Defend`, `Magic`, `Heal`, `Buff`, `SelfBuff`, `Exhaust`, `Ethereal`, `UpgradeScript`, `TriggerScript`, `ConditionScript`, `SoundEffects`. All damage/shield/heal/buff behavior lives in Lua (`UseScript`/`InitScript`). `BaseScript` is set inside `InitScript`, not a CSV column.


