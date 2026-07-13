# Mokou Mod Analysis

## Mod Overview

**Author**: 知了 (Zhīle)
**Version**: 1.1
**Workshop ID**: 3740597947
**Description**: A Fujiwara no Mokou (Touhou Project) character mod. Adds a new playable career with a unique "Burn" (灼烧) mechanic. Mokou's gameplay revolves around self-immolation — applying Burn stacks to both herself and enemies, then converting Burn into regeneration (`buff_evergreen` = 自愈/regeneration) and rebirth (`buff_rebirth` = 重生).

The mod introduces a "Fuel" keyword system where certain cards have special effects when Burned (焚毁/discarded). The career has two active skills (焚身化羽/燃燒殆盡) and comes with custom animations, icons, relics, and full multi-language support.

Author's note: "第一次做mod，怕出bug所以机制很保守" (First time making a mod, afraid of bugs so mechanics are conservative).

## Directory Structure

```
Mokou/
  ModConfig.json                        # Mod metadata
  Icon.png                              # Workshop icon
  .workshop-id / .workshop-sync.json    # Workshop sync
  Scripts/
    Entry.lua                           # Hook-based mod logic (81 lines)
  Data/
    Card/
      cardsample.csv                    # 10 cards (main set)
      mokouextras.csv                   # 8 cards (extras/generated cards)
    Buff/buffsample.csv                 # 1 custom buff (immortality)
    Career/careersample.csv             # 1 career definition
    Relic/relicsample.csv               # 3 relic definitions
  Text/
    Card/
      cardsample.csv                    # Card localization
      mokouextras.csv                   # Extra card localization
    Buff/buffsample.csv                 # Buff localization
    Career/careersample.csv             # Career localization
    Relic/relicsample.csv               # Relic localization
    CardPack/cardpack.csv               # Card pack localization
    KeyWordsDic/keyword.csv             # "Fuel" keyword definition
  ModResource/
    AnimationLib/Mokou/
      Attack/   (Attack_00.png + config.json)
      Defend/   (Defend_00.png + config.json)
      Hit/      (Hit_00.png + config.json)
      Idle/     (Idle_00–07.png + config.json)
      Skill/    (Skill_00.png + config.json)
    Icon/
      Card/   (17 card icon PNGs)
      Relic/  (3 relic icon PNGs)
    Images/
      Avatar/Mokou.png
      CareerImage/Mokou.png
      Character/Mokou.png
      Icon/
        Mokou.png
        Mokou - 技能.png (skill icon)
```

## Entry Point Analysis (`Entry.lua`)

Entry.lua (81 lines) is more complex than BlackMage's, implementing gameplay logic that couldn't be expressed in CSV cells.

### Hook Setup

```lua
function ModConfig:Setup()
    self:ModifyDataConfig("buff_burn", "UpperBound", "9999")
    self:AddMethodHookBefore("CardItem.EffectOfBurnCard", function(ctx)
        -- Fuel mechanic implementation
    end)
end
```

### Key Modification: Buff Burn UpperBound

`self:ModifyDataConfig("buff_burn", "UpperBound", "9999")` — Increases the Burn buff's stack cap from its default to 9999, since Mokou's mechanics involve stacking Burn to very high values.

### Fuel Mechanic (CardItem.EffectOfBurnCard Hook)

This is the core innovation. When any card is burned (discarded via the game's Burn mechanic), the hook intercepts `EffectOfBurnCard` and checks the card's ID:

1. **MokouSkipFuel**: Cards can set `Vars["MokouSkipFuel"] = "1"` to skip fuel handling (prevents infinite loops).

2. **undying_legend**: Increments a `BurnLegendBonus` counter on the card's Vars, then creates a copy of itself in the hand via `fight_ui:CreateCardItem(ctx.dataConfig)`.

3. **little_bird → tasty/bad roast bird**: Checks success rate (40%), creates the appropriate result card.

4. **tasty_roast_bird → phoenix_wings or bad_roast_bird**: Chain check at 40%.

5. **bad_roast_bird → tasty_roast_bird or bamboo_forest_fire**: Chain check at 30%.

6. **bamboo_forest_fire**: 70% chance to do nothing (success), or creates a copy with doubled `BambooForestFireBlessCount`.

### Helper Functions

- **ModConfig:GetFightUI()**: Uses `xlua.get_generic_method` to call the generic `UIManager.GetUI<T>()` method, retrieving the `FightUI` window. This is a notable C# generics interop pattern.

- **ModConfig:GetVarNumber(vars, key, default)**: Safely reads a numeric value from a Vars dictionary.

- **ModConfig:CheckSuccess(target)**: Luck-based check: `lucky + random(1, 100) >= target`.

- **ModConfig:CreateCardData(id)**: Creates a `CS.DataConfig(id, CS.DataType.Card)`.

- **ModConfig:CreateCardByCheck(fight_ui, target, success_id, fail_id)**: Creates a card based on a luck check outcome.

## Data Config Format

### Card Data (`Data/Card/cardsample.csv`)

**Columns**: `Id`, `Rarity`, `Expend`, `Tag`, `InitScript`, `DrawScript`, `UseScript`, `DropScript`, `Icon`, `Effects`, `Action`, `PackBelong`

Same structure as BlackMage's card CSV. Notable cards:

- **`*mokou_tail`** (Skill, rarity 3): Self-destruct to gain max HP based on Burn stacks, draw 10. Uses `CS.ScriptExecutor.PlayerInfo.SkillTime` for cooldown tracking.
- **`fire_claws`**: Standard attack with Burn application.
- **`phoenix_wings`**: AoE damage that repeats for every 10 Burn stacks on self.
- **`spontaneous_combustion`**: Gains `buff_extraordinary` and Burn.
- **`phoenix_rebirth`**: Converts all Burn stacks into `buff_evergreen` (regeneration).
- **`hourai_elixir`**: Gains `buff_evergreen` and Immortality buff.
- **`*mokou_kindling`**: Burns 1 hand card, using `SkillTime` to track state.

### Extra Cards (`Data/Card/mokouextras.csv`)

Generated cards (prefixed with `*`) that are created by the Fuel mechanic:

- **`whirlwind_of_self_immolation`**: Applies Burn to all and triggers Burn ticks.
- **`kayfuu_kick`**: Damage = Burn stacks × 8, then doubles Burn.
- **`undying_legend`**: Fuel card that copies itself with escalating power.
- **`bamboo_flame_tube`**: AoE Burn + immediate tick trigger.
- **`bird_barbecue`**: Creates a `little_bird` card.
- **`*little_bird`**: Fuel card with 40% check for tasty/bad bird.
- **`*tasty_roast_bird`**: Fuel card that creates phoenix_wings on success.
- **`*bad_roast_bird`**: Unusable card (Tag: `Unusable`) with Fuel check for tasty bird or bamboo_forest_fire.
- **`*bamboo_forest_fire`**: Fuel card that gives random blessings and potentially copies itself with doubled values.

### Career Data (`Data/Career/careersample.csv`)

**Columns**: `Id`, `SanMax`, `SkillScript`, `Animation`, `Vocal`, `Skill1`, `Skill2`, `ChoiceIcon`, `DollIcon`, `Character`, `Avatar`, `CareerImage`, `ActionImage1`, `ActionImage2`, `Dialogue`, `EmojiPath`, `AttackEffect`, `SkillEffect`, `HitEffect`, `DefendEffect`

- **SanMax**: 100
- **SkillScript**: Massive inline Lua that:
  - Initializes `SkillTime` keys for cooldowns
  - `StartRound` handler: decrements cooldowns, checks Burn card state
  - `BurnCard` handler: grants 1 power and 4 Burn when `mokou_kindling` pending flag is set
  - `SelectCardEnd` handler: cleanup for kindling state
  - Initial buffs: `buff_evergreen` (5), `buff_rebirth` (10), immortality (1)
- **Animation**: `Mods/Mokou/ModResource/AnimationLib/Mokou`
- **Skill1**: `Mokou_cardsample_mokou_tail` (焚身化羽)
- **Skill2**: `Mokou_cardsample_mokou_kindling` (燃燒殆盡)

### Relic Data (`Data/Relic/relicsample.csv`)

All three relics are `*`-prefixed (unobtainable through normal gameplay?):

- **superheated_phoenix_feather** (Rarity 2): On hurt, gain `buff_extraordinary` = damage taken.
- **honest_deaths** (Rarity 3): End of turn, if no shield, gain 30 `buff_rebirth`.
- **floating_up_to_the_moon_immortal_smoke** (Rarity 4): On Burn card, draw 1.

## Text System

Same multi-language format as BlackMage (zh-Hans, zh-Hant, en, ja).

**Career text** adds columns for `Title`, `Action1`, `Action2`, `Passive1`, `Passive2` with `<name>` and `<des>` XML-like tags for rich tooltip formatting.

**Keyword System**: The `keyword.csv` defines the "Fuel" keyword:
```csv
Fuel,,这张牌被焚毁时，触发其燃料效果。,燃料,燃料,Fuel,...,FALSE
```
`ShouldShow=FALSE` means it's an internal keyword (not shown in the keyword dictionary UI).

## Resource Management

### AnimationLib Structure

Each animation state (Idle, Attack, Defend, Hit, Skill) has a folder with:
- **config.json**: Standard fields — `AnimationPerFrame` (0.1s), `isLoop` (false for one-shots, true for idle), `Direction` ("Right")
- **PNG frames**: Sequential frame images named `{State}_NN.png`

Idle has 8 frames (00-07), others have 1 frame each.

### Icon/Image Structure

- `Icon/Card/`: 17 card PNGs
- `Icon/Relic/`: 3 relic PNGs
- `Images/Avatar/`, `Images/CareerImage/`, `Images/Character/`, `Images/Icon/`: Career visual assets

## Key Patterns & Techniques

### 1. **Fuel Mechanic (Burn-Triggered Effects)**
The most innovative pattern. Cards are tagged with `Burnout` or have their own IDs checked in the `EffectOfBurnCard` hook. When burned (discarded), they generate new cards or effects. This creates a "cooking" mini-game where cards transform through a chain.

### 2. **SkillTime for Cooldowns**
Uses `CS.ScriptExecutor.PlayerInfo.SkillTime` — a dictionary that persists across battles — for tracking cooldowns. Keys are checked and decremented in `StartRound`.

### 3. **Luck-Based Card Generation**
The "bird barbecue" chain uses luck checks (`CheckSuccess`) to determine outcomes, creating branching card transformations:
```
bird_barbecue → little_bird → tasty_roast_bird (40%) / bad_roast_bird (60%)
                               ↓                     ↓
                         phoenix_wings (40%)    tasty_roast_bird (30%)
                                                bamboo_forest_fire (70%)
```

### 4. **Self-Destruct as Mechanic**
`mokou_tail` kills the player (`self:ChangeHp(tostring(death))` with death = `-CurHp - 9999`) but grants permanent max HP before dying. Combined with immortality/rebirth buffs, this becomes a sustainable cycle.

### 5. **Vars-Based Per-Instance Card State**
Uses `self.dataConfig.Vars` to store persistent state on card instances:
- `BurnLegendBonus`: Tracks how many times undying_legend has been copied
- `BambooForestFireBlessCount`: Tracks escalation of bamboo_forest_fire
- `MokouSkipFuel`: Prevents infinite fuel recursion

### 6. **Burn-Triggered Power Generation**
The `BurnCard` event in the career script grants 1 Power (魔能) when `mokou_kindling` is active. This rewards the player for burning cards.

### 7. **C# Generics Interop**
```lua
local get_ui_generic = xlua.get_generic_method(CS.Witch.UI.UIManager, "GetUI")
local get_ui_fightui = get_ui_generic(CS.Witch.UI.Window.FightUI)
return get_ui_fightui(CS.Witch.UI.UIManager.Instance, "FightUI")
```
This is a critical pattern for accessing generic C# methods from Lua.

### 8. **Tag-Based Card Filtering**
Tags like `Recycle` (回転), `Burnout` (焚毁), `Annihilation` (消亡), `Unusable` control card behavior:
- `Recycle`: Card returns to hand after use.
- `Burnout`: Card is burned after use.
- `Annihilation`: Card is removed from the game after use.
- `Unusable`: Card cannot be played.

### 9. **ModifyDataConfig for Vanilla Tuning**
```lua
self:ModifyDataConfig("buff_burn", "UpperBound", "9999")
```
This modifies a vanilla game data entry at load time — changing the Burn buff's max stack from (presumably) a default value to 9999.

### 10. **Inline Lua in CSVs**
Card scripts can be very long (multiple lines compressed into one CSV cell). The game uses `CS.DataConfig(id, CS.DataType.Card)` to load card data by ID at runtime.

## C#/Lua Interop

- **No DLL**. All logic is in Entry.lua and CSV-embedded scripts.
- Uses `CS.FightPlayer.Instance`, `CS.ScriptExecutor.PlayerInfo`, `CS.Witch.UI.UIManager`.
- `xlua.get_generic_method` is used for generic C# method access.
- `CS.DataConfig(id, CS.DataType.Card)` creates card data objects at runtime.
- `self:AddEvent("StartRound", ...)` and similar event system for lifecycle hooks.
- `self:CreateCardItem()` and `fight_ui:CreateCardItem()` instantiate cards in the UI.
- `self:BurnCardByData(self.dataConfig)` burns a specific card instance.
- `PlayerInfo.RandomAddBless(count)` adds random blessings.

## Extractable Lessons

1. **Fuel system**: Burn-triggered effects are powerful for creating cards that do something when discarded. Implement via `AddMethodHookBefore("CardItem.EffectOfBurnCard", ...)`.

2. **Self-damage as resource**: Taking damage as a cost (e.g., kayfuu_kick dealing self-damage) enables risk-reward gameplay.

3. **Chain card transformations**: Cards that transform into other cards on burn create engaging gameplay loops. Use Vars for escalation tracking.

4. **SkillTime persistence**: `CS.ScriptExecutor.PlayerInfo.SkillTime` is a cross-battle dictionary for cooldowns and persistent state.

5. **Generics pattern**: `xlua.get_generic_method` is essential for accessing C# generic methods — always useful for `UIManager.GetUI<T>()`.

6. **Card creation at runtime**: `CS.DataConfig(id, CS.DataType.Card)` followed by `fight_ui:CreateCardItem()` creates and shows cards dynamically.

7. **Luck checks**: `math.random(1, 100) + lucky >= target` is a standard dice roll pattern that interacts with the game's Luck stat.

8. **Burnout/Annihilation tags**: Cards with `Burnout` or `Annihilation` tags are consumed in specific ways. Understanding these tags is essential for card design.

9. **Career initialization**: The `SkillScript` in Career CSV handles both battle initialization and event registration. It's where career-wide passive effects and cooldown tracking start.

10. **Animation frames**: `AnimationLib/{CharacterName}/{State}/{State}_NN.png` with `config.json` controlling timing. Idle loops, other states play once.
