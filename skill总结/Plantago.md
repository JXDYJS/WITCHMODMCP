# Plantago Mod Analysis

## Mod Overview

**Author**: buttertiper
**Version**: 1.0
**Description**: A character OC (original character) mod adding "Plantago" (普兰塔戈) — a cute mouse character. The gameplay revolves around stacking Luck (幸运) and Poised (蓄势) for defensive buildup, then converting into Counterattack (反击) power.

**Key Mechanics**:
- **Poised (蓄势)**: Gained from the "Careful!!!" skill. Converted to Counterattack and Shield at turn start.
- **Counterattack (反击)**: Adds damage to the "Counter!!!" skill.
- **Dice Check System**: Skills use `self.CheckDice:Roll()` for luck-based bonus effects.
- **Cooldown System**: Both skills have cooldowns managed through `SkillTime`.

The mod is marked as "Enabled": false (disabled) and has "MustSame": true (requires matching version with other players for multiplayer).

## Directory Structure

```
Plantago/
  ModConfig.json                        # Mod metadata
  Icon.png                              # Workshop icon
  .workshop-id / .workshop-sync.json    # Workshop sync
  Scripts/
    Entry.lua                           # Empty entry (2 lines)
  Data/
    Card/cardsample.csv                 # 2 cards (mouse_guard, mouse_fang — both *)
    Career/careersample.csv             # 1 career definition
  Text/
    Card/cardsample.csv                 # Card localization
    Career/careersample.csv             # Career localization
  ModResource/
    AnimationLib/普兰塔戈/
      Attack/     (普兰塔戈.png)
      Defend/     (普兰塔戈.png)
      Hit/        (普兰塔戈.png + config.json)
      Idle/       (普兰塔戈_Idle1-24.png + config.json)
      Skill/      (普兰塔戈.png)
    Icon/Choice/  (普兰塔戈.png)
    Images/
      Avatar/     (普兰塔戈.png)
      CareerImage/(普兰塔戈.png)
      Character/  (普兰塔戈.png)
      Dialogue/Character/ (普兰塔戈.png)
```

**Note**: Chinese-character filenames for animation/asset files. Author mentions "vibe coding" (纯Vibe Coding) and incomplete pixel attack/hit artwork.

## Entry Point Analysis (`Entry.lua`)

```lua
function ModConfig:Setup()
end
```

Completely empty. No hooks, no data modifications. **All logic is in CSV-embedded Lua scripts.** This is the simplest possible entry point — a no-op that still satisfies the mod loader's requirement for a `ModConfig:Setup()` function.

## Data Config Format

### Card Data (`Data/Card/cardsample.csv`)

Both cards are `*`-prefixed (non-collectible), no `PackBelong` column:

**`*mouse_guard`** (Rarity 3, Skill):
- Uses `CS.ScriptExecutor.PlayerInfo.SkillTime` for cooldown tracking (CD: 2)
- Rolls `self.CheckDice:Roll()` — returns a dice result, checked against thresholds 50 and 100
- Base effect: +2 Poised
- Check 50 success: +1 Poised, draw 1
- Check 100 success: triggers 50-effect again (double proc)

**`*mouse_fang`** (Rarity 3, Attack):
- CD: 3
- Deals 3×1 self-damage, then deals `6 + counterattack_stacks` damage to target
- Check 60 success: +6 shield, +1 Impregnable
- Check 100 success: triggers 60-effect again

### Career Data (`Data/Career/careersample.csv`)

**mouse_keeper**: Max SAN 90.

**SkillScript inline Lua**:
1. Initializes `SkillTime` keys for both skills
2. `StartRound` handler:
   - Decrements both cooldowns
   - Converts Poised → Counterattack + Shield (level match)
3. Initial equipment references to vanilla assets (DollAni/卡洛琳/...)

**Skill 1**: `Plantago_cardsample_mouse_guard` (小心！！！)
**Skill 2**: `Plantago_cardsample_mouse_fang` (反击！！！)

## Text System

Standard multi-language format. Career text includes rich `<name>` and `<des>` tooltip formatting.

**Note**: The text CSV uses `{buff_poised}` and `{buff_counterattack}` and `{buff_impregnable}` references to display buff names dynamically.

## Resource Management

### AnimationLib
- Idle: 24 frames (普兰塔戈_Idle1-24.png), looping
- Attack: 1 frame (普兰塔戈.png), non-looping
- Defend: 1 frame (普兰塔戈.png), non-looping
- Hit: 1 frame (普兰塔戈.png), non-looping (has config.json)
- Skill: 1 frame (普兰塔戈.png), non-looping

### Image Structure
- Chinese-character named directories and files for all assets
- Dialogue/Character image exists (though for a different character — 卡洛琳 from the vanilla game)

## Key Patterns & Techniques

### 1. **Dice Check System**
```lua
local check = self.CheckDice:Roll().Value
if check >= 50 then apply_success() end
if check > 100 then apply_success() end
```
Uses the game's built-in `CheckDice` for luck-based resolution. The threshold system allows double-proc on critical success (>100).

### 2. **Buff Conversion Mechanic**
```lua
local poised = self.Self:GetBuff("buff_poised").buffConfig.Level
self:AddBuff("buff_counterattack", tostring(poised))
self:ChangeDefence(tostring(poised))
```
Poised stacks are converted to Counterattack and Shield at the start of each round. This creates a cycle: build Poised → get defense and counter damage.

### 3. **Self-Damage as Cost**
```lua
self:ChangeHp("-1");
self:ChangeHp("-1");
self:ChangeHp("-1");
```
Three separate calls for 3 self-damage ticks. This triggers on-hurt effects 3 times.

### 4. **No-Entry Mod**
Demonstrates that a complete mod can have an empty `Setup()` function. This is useful for:
- Character-only mods with no hooks needed
- Simple card/career additions
- Asset replacement mods

### 5. **disabled + MustSame**
`"Enabled": false` and `"MustSame": true` in ModConfig.json. This indicates:
- `MustSame`: All multiplayer players must have the same version
- `Enabled: false`: Currently disabled in mod list

## C#/Lua Interop

- **No DLL**. All logic in Lua/CSV.
- Uses `self.CheckDice:Roll()` for dice rolling.
- Uses `self.Self:GetBuff("buff_poised")` for buff access.
- Uses `CS.ScriptExecutor.PlayerInfo.SkillTime` for cooldowns.
- Equipment paths reference vanilla assets (DollAni/卡洛琳/待机/卡洛琳).

## Extractable Lessons

1. **CheckDice system**: `self.CheckDice:Roll().Value` returns a number influenced by the character's Luck stat. Useful for probability-based bonus effects.

2. **Buff conversion at round start**: Convert one buff type to another in `StartRound` to create resource cycling (Poised → Counterattack).

3. **Self-damage loops**: Use `ChangeHp("-1")` to trigger on-hurt effects multiple times intentionally.

4. **Minimal viable Entry.lua**: An empty `Setup()` is valid. Mods without runtime hooks don't need Entry.lua logic.

5. **Single-frame animations**: For characters without full pixel art, single-frame PNGs can be used for non-idle states. Only Idle needs multiple frames.

6. **Chinese character filenames**: The mod system supports Chinese-character file paths. This works but may cause encoding issues in tools.

7. **Simplified data**: No Buff data files, no Relic data files, no CardPack data — the mod uses only vanilla buffs and has no custom relics.

8. **Skill-only cards**: Both skill cards are `*`-prefixed (non-collectible) starter cards with cooldowns, not part of a card pack.
