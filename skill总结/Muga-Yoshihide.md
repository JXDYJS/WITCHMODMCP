# Muga-Yoshihide Mod Analysis

## Mod Overview

**Author**: 路未 (Lù Wèi)
**Version**: 1.0
**Description**: A full character mod based on Yoshihide (良秀) from the game Limbus Company. The mod introduces a "Wuwo" (无我) stacking mechanic — a cross-battle persistent resource that increases damage but reduces max HP. The character's gameplay revolves around burning cards and accumulating Wuwo stacks.

**Core Mechanic — Wuwo (无我)**:
- Each stack: +1% damage (doubled for "天杀星刀" tagged cards), -1 max HP (minimum 50), +2x shield at battle start
- Wuwo stacks persist across battles (saved in `SpecialVars`)
- **Milestone 1 (50 stacks)**: Unlocks "心-天杀星" — draw 1 on burn, gain 1 power per 2 burns
- **Milestone 2 (100 stacks)**: Unlocks "磨砺完全的艺术" — restore 3x deducted max HP, gain 1 power on burn

**Active Skills**:
- Skill 1: 阿赖耶识 (Alaya) — Gain 1 Wuwo, add "天杀" to hand. CD: 5
- Skill 2: 将我抹去。也将你抹去 — Enemy: time stop + marks. Ally: 100% max HP true damage. CD: 10 (7)

## Directory Structure

```
Muga-Yoshihide/
  ModConfig.json                        # Mod metadata
  Icon.png                              # Workshop icon
  .workshop-id / .workshop-sync.json    # Workshop sync
  Scripts/
    Entry.lua                           # Minimal entry (5 lines)
  Data/
    Card/cardsample.csv                 # 9 cards
    Buff/buffsample.csv                 # 5 buffs (+ 2 milestone buffs)
    Career/careersample.csv             # 1 career definition
    Relic/relicsample.csv               # 1 relic (Spider Nest)
    CardPack/cardsample.csv             # 1 card pack
    RoleData/roledatasample.csv         # Role metadata
  Text/
    Card/cardsample.csv                 # Card localization
    Buff/buffsample.csv                 # Buff localization
    Career/careersample.csv             # Career localization
    Relic/relicsample.csv               # Relic localization
    CardPack/cardsample.csv             # Card pack localization
    RoleData/roledatasample.csv         # Role name localization
  ModResource/
    AnimationLib/yoshihide/
      Attack/  (Attack_00-02.png + config.json)
      Defend/  (Defend_00.png + config.json)
      Hit/     (Hit_00.png + config.json)
      Idle/    (Idle_00-12.png + config.json)
      Skill/   (Skill_00-02.png + config.json)
    Icon/
      Buff/   (5 buff PNGs: Muga, HeavenlyMark, Duanyuan, TheEgo, Xin)
      Relic/  (SpiderNest.png)
    Images/
      Avatar/yoshiohide_logo.png
      Card/ (7 card image PNGs)
      CardPack/muga_cardpage.png
      CareerImage/yoshihide_1.png
      Character/yoshihide_1.png
      Skill/ (TSXDALYS_left.png, TSXDALYS_right.png)
```

## Entry Point Analysis (`Entry.lua`)

The Entry.lua is extremely minimal (5 lines):

```lua
function ModConfig:Setup()
    self:AddMethodHookBefore("SettingUI.OnEnable", function ()
        CS.Commands.Log("Muga-Yoshihide", "测试SettingUI.OnEnable")
    end)
end
```

This hook is essentially a debug/test hook that logs when the settings UI opens. **All actual game logic is in the CSV-embedded Lua scripts** — a notable contrast to Mokou which uses Entry.lua for its Fuel system. This mod demonstrates that complex character mechanics can be implemented entirely within CSV cell scripts without entry-point hooks.

## Data Config Format

### Card Data (`Data/Card/cardsample.csv`)

**Columns**: Same as other mods (`Id`, `Rarity`, `Expend`, `Tag`, `InitScript`, `DrawScript`, `UseScript`, `DropScript`, `Icon`, `Effects`, `Action`, `PackBelong`).

**Cards:**

| Id | Rarity | Cost | Tags | Description |
|---|---|---|---|---|
| `alaya` | 4 | 0 | Skill | Gain 1 Wuwo, add "天杀" to hand. CD: 5 |
| `erase` | 4 | 0 | Skill | Enemy: 1 time stop + 5 marks. Ally: 100% max HP true damage + 1断缘. CD: 10 |
| `slay_the_haven` | 3 | 1 | Recycle,天杀星刀 | Burn 1, 3 random attacks (5 dmg each + Wuwo scaling). +1 Wuwo |
| `slash` | 1 | 1 | Recycle,天杀星刀 | Burn 1, 10 dmg + mark. +1 Wuwo. Add copy to discard |
| `depict` | 2 | 1 | — | Burn 2, +2 Wuwo, add 3 random cards from pool |
| `splash` | 1 | 2 | 天杀星刀 | Burn 1, 3× Wuwo-level damage. +1 Wuwo |
| `paint` | 2 | 0 | — | Gain power/draw = hand size. Burn hand |
| `erase_me_erase_you` | 3 | 2 | Burnout | +3 Wuwo, all enemies -30% max HP |
| `memory_of_someone` | 1 | 0 | — | Spend 2 Wuwo, gain 3 power |

**Key Note**: All cards belong to `Muga-Yoshihide_cardsample_cardpack_muga` except `alaya` and `erase` (no PackBelong — these are starter skill cards).

### Buff Data (`Data/Buff/buffsample.csv`)

**Columns**: Same + `CanZero` column.

| Id | Type | Max | Notes |
|---|---|---|---|
| `wuwo` | 能力(Ability) | 999 | Core stacking resource. CanZero absent → dies at 0? |
| `heavenly_mark` | 能力 | 99 | Damage amp + max HP reduction on hit |
| `duanyuan` | 能力 | 99 | 500% damage increase (×6 multiplier via `ChangeDamage`) |
| `xin_tianshaxing` | 能力 | 99 | Milestone: draw on burn, power on 2 burns. `CanZero=True` |
| `polished_art` | 能力 | 99 | Milestone: restore HP, power on burn. `CanZero=True` |

**Duanyuan (断缘) Buff**: Uses `Hurt` event with `d:ChangeDamage(tostring(math.floor(d.damage*6)))` — a ×6 damage multiplier. This is applied when "erase me erase you" targets an ally.

### Career Data (`Data/Career/careersample.csv`)

**SkillScript**: The massive inline Lua handles:
1. **Initialization**: Creates `SkillTime` keys for both skills. Loads `SpecialVars["wuwo"]` — cross-battle persistent Wuwo counter.
2. **StartRound**: 
   - Decrements skill cooldowns
   - Checks Wuwo level ≥ 50 → grants `xin_tianshaxing` milestone
   - Checks Wuwo level ≥ 100 → grants `polished_art` milestone
   - Applies Wuwo-based max HP reduction and shield
3. **BurnCard**: Decrements skill cooldowns.
4. **Win/Escape**: Saves current Wuwo level back to `SpecialVars["wuwo"]`.

### Relic Data (`Data/Relic/relicsample.csv`)

**Spider Nest (蛛巢)** — Rarity 3:
- `FightStart`: If alone (party count ≤ 1), max energy +2
- `BurnCard`: Gain 1 Wuwo

### RoleData (`Data/RoleData/roledatasample.csv`)

**Columns**: `Id`, `Avatar`, `CharacterImage` — simple reference mapping for the role.

## Text System

Same multi-language structure as other mods (zh-Hans, zh-Hant, en, ja).

Career text uses `<name>` and `<des>` XML tags for rich tooltip formatting in passive skill descriptions.

## Resource Management

### AnimationLib
- Idle: 13 frames (00-12), looping
- Attack: 3 frames (00-02), non-looping
- Skill: 3 frames (00-02), non-looping
- Defend: 1 frame, non-looping
- Hit: 1 frame, non-looping
- All `config.json` use `0.1` seconds per frame

### Images
- Card images: 7 custom PNGs (depict, erase_me_erase_you, memory_of_someone, paint, slash, slay_the_haven, splash)
- Skill images: 2 (TSXDALYS_left.png, TSXDALYS_right.png)
- Standard career assets: Avatar, CareerImage, Character, CardPack

## Key Patterns & Techniques

### 1. **Cross-Battle Persistent State**
```lua
local sv = CS.ScriptExecutor.PlayerInfo.SpecialVars
sv:set_Item("wuwo", tostring(lv))
```
Uses `SpecialVars` (a cross-run? cross-battle dictionary) to save Wuwo stacks. On `Win` and `Escape` events, Wuwo is persisted. On battle start, it's loaded back.

### 2. **Milestone/Evolution System**
The `StartRound` handler checks Wuwo thresholds (50, 100) and grants milestone buffs permanently once reached. The milestone buffs (`xin_tianshaxing`, `polished_art`) have `CanZero=True` and register event handlers for `BurnCard`.

### 3. **Max HP Reduction as Cost**
Wuwo's effect on max HP is applied in the Career SkillScript using the `StartRound` event. The reduction is `-1 max HP per stack (minimum 50)`.

### 4. **Card Copying to Discard**
```lua
self:AddCardToDeckById("Muga-Yoshihide_cardsample_slash", true)
```
The second parameter (`true`) adds the copy to the discard pile rather than the deck. This is used by `slash` to generate infinite copies.

### 5. **Random Card Generation from Pool**
```lua
local pool = {"Muga-Yoshihide_cardsample_slay_the_haven", ..., "luckycard_5"}
for i = 1, 3 do
    local idx = math.random(#pool)
    local id = pool[idx]
    if string.find(id, "Muga-Yoshihide") then
        self:AddCardById(id)
    else
        -- Add Burnout tag to vanilla cards
        pcall(function() 
            local dc = CS.DataConfig(id, CS.DataType.Card)
            local tag = dc.Vars:get_Item("Tag") or ""
            dc.Vars:set_Item("Tag", tag .. ",Burnout")
            self:CreateCard(dc)
        end)
    end
end
```
`depict` randomly generates cards from a pool. Vanilla game cards get the `Burnout` tag added dynamically.

### 6. **Dual-Purpose Card `erase`**
The `erase` skill card behaves differently based on target type:
- Enemy (`string.sub(t.InstanceId,1,1) == "e"`): Grants time stop + heavenly marks
- Ally (player/friendly): Deals 100% max HP true damage + grants 断缘 (×6 damage)

### 7. **`ForAllStatus` Pattern**
```lua
self:ForAllStatus(function(t)
    if t ~= nil and t.InstanceId ~= nil and string.sub(t.InstanceId,1,1) == "e" then
        local maxHp = t.MaxHp or 100
        local newMax = math.max(math.floor(maxHp * 0.7), 1)
        t:SetMaxHp(tostring(newMax))
    end
end)
```
Used in `erase_me_erase_you` to reduce all enemies' max HP by 30%.

### 8. **Hand-Size Scaling**
```lua
local hand = self.HandCard
local count = hand.Count
self:ChangePower(tostring(count))
self:DrawCount(tostring(count))
self:BurnCard(tostring(count))
```
`paint` scales its effect with hand size — gaining power, drawing cards, then burning them all.

### 9. **Damage Scaling with Wuwo**
Every 天杀星刀 card applies:
```lua
local dmg = math.floor(base * (1 + wuwoLevel * 0.02 + markLevel * 0.1))
```
- +2% damage per Wuwo stack
- +10% damage per Heavenly Mark stack on the target

### 10. **Minimal Entry.lua**
The mod proves that complex character logic can be implemented entirely in CSV scripts without Entry.lua hooks. The Entry.lua here is essentially vestigial.

## C#/Lua Interop

- **No DLL**. Pure Lua + CSV.
- Uses `CS.ScriptExecutor.PlayerInfo.SpecialVars` for cross-battle persistence.
- Uses `CS.ScriptExecutor.PlayerInfo.SkillTime` for cooldown tracking.
- Uses `self:ForAllStatus()` to iterate all entities.
- Uses `self:AddCardToDeckById(id, toDiscard)` for deck manipulation.
- Uses `self:ChangeMaxHp()` and `t:SetMaxHp()` for max HP manipulation.
- Uses `self:SetMaxHp()` on targets for max HP reduction.
- Uses `self:AddCardById()`, `self:CreateCard()`, `self:AddCardToDeckById()` for card generation.

## Extractable Lessons

1. **SpecialVars for persistence**: Use `CS.ScriptExecutor.PlayerInfo.SpecialVars` for cross-battle data that survives character runs.

2. **Max HP as resource**: Reducing max HP as a cost creates long-term consequences and interesting strategic decisions.

3. **Milestone system**: Check cumulative stats in `StartRound` to unlock permanent buffs that change gameplay.

4. **Dual-purpose cards**: Check `string.sub(t.InstanceId, 1, 1)` to differentiate enemies from allies.

5. **Deck pollution**: `AddCardToDeckById` with the discard flag creates self-replicating cards.

6. **Dynamic tag addition**: PCall to modify `dc.Vars` to add `Burnout` tag to vanilla cards at generation time.

7. **ForAllStatus iteration**: Use for effects that need to affect all entities on the battlefield.

8. **Hand-size-dependent effects**: Access `self.HandCard.Count` for effects that scale with hand size.

9. **Cooldown decrement on events**: Decrement cooldowns not just on round start but also on other events (BurnCard) to create faster-paced gameplay.

10. **Zero-threshold buffs**: `CanZero=True` on milestone buffs allows them to be "inactive" markers that still register event handlers.
