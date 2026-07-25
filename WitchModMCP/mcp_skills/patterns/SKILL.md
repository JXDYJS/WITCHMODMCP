# Mod Patterns — Reference

CSV formats, API references, event tables, and hook points for Witch mod development.
Experience guides (directory structure, workflow, troubleshooting) are in `.agents/skills/witchSkill/SKILL.md`.

## CSV Data Format

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

## Complete Card CSV Column Reference

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


