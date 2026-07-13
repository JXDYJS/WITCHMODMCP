# MoonRite (月相秘仪) — Card Pack Mod Analysis

## Mod Overview

**Author**: AG.jewel | **Version**: 1.0 | **Workshop ID**: 3740527324

A card pack mod built around **Moonlight**, **Moon Phase**, and **Eclipse Mark** mechanics. Adds:
- 18 general-purpose cards (mix into official reward pool)
- 8 Moon-themed relics
- 2 playable characters: **Luneya (观月魔女)** / **Selena (蚀潮魔女)**
- 1 new card pack: MoonRite (月相秘仪)
- Lightweight UI-only DLL hook to fix relic icon sizing in shop

Notable: Developed with **Codex** AI assistance. Includes tutorial docs and reusable Codex Skill.

## Directory Structure

```
MoonRite/
  ModConfig.json              — Mod metadata
  .workshop-id / .workshop-sync.json
  README.md                   — English/Chinese documentation
  WorkshopDescription_steam_bbcode.txt
  WorkshopDescription_zh-en.md
  WorkshopFeedbackThread_steam_bbcode.txt
  WorkshopPreview.jpg / Icon.png
  
  Data/
    Card/moonrite.csv         — 22 card definitions
    Buff/moonrite.csv         — 13 buff definitions
    Career/moonrite.csv       — 2 career definitions
    CardPack/moonrite.csv     — Card pack definition
    Relic/moonrite.csv        — 8 relic definitions
    RoleData/moonrite.csv     — Role avatar/character paths

  Text/                       — Multi-language text (mirrors Data/)
    Card/moonrite.csv
    Buff/moonrite.csv
    Career/moonrite.csv
    CardPack/moonrite.csv
    Relic/moonrite.csv
    RoleData/moonrite.csv

  ModResource/
    AnimationLib/             — Luneya & Selena animations (Attack/Defend/Hit/Idle/Skill)
    Images/                   — Avatar, Card, CardPack, CareerImage, Character, Icon, Relic

  Scripts/
    Entry.dll                 — Tiny UI-only DLL (fixes shop relic icon size)

  Docs/
    CharacterExpansionPlan.md
    CharacterSkillCooldownGuide.md
    Mod制作教程.md             — Modding tutorial (Chinese)
    MoonRite_Content_Inventory.md
    CodexSkill/SKILL.md       — Reusable Codex skill for mod development
    DeveloperNotes/
      MoonRiteRelicUiFix_README.md
      MoonRiteRelicUiFix.cs   — Source for the UI fix DLL

  .workshop-id
  .workshop-sync.json
```

## Data Config Format

### Card CSV — Inline Lua Script Approach
Header: `Id,Rarity,Expend,Tag,PackBelong,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action`
- Unlike SunExp (which delegates to C# DLL), MoonRite cards contain **inline Lua code** directly in `UseScript` and `InitScript` columns
- `InitScript`: Sets `self.Vars:set_Item("BaseScript", "...")` to define card type (`AttackCardItem` or `CommonCardItem`)
- `UseScript`: Contains full card effect logic as embedded Lua (self-contained helper function block pattern)
- Helper functions are defined **inside each UseScript cell** (duplicated across all cards):
  - `getLight()` — Read moonlight stacks
  - `getPhase()` — Read moon phase
  - `spendLight(maxSpend, minKeep)` — Consume moonlight with Silver Tide Bottle refund support
  - `advancePhase()` — Cycle moon phase 1→2→3→4→1 with Four-Phase Astrolabe bonus
  - `forEachEnemy(fn)` — Iterate enemies
  - `markDamageEnemy(t, pct)` — Deal true damage from Eclipse Mark
- This creates a **boilerplate-heavy** but self-contained pattern — every card file has the same helper functions repeated

### Buff CSV
Header: `Id,InitScript,ApplyScript,ClearScript,ReducePerTurn,ReducePerAttacked,ReducePerUse,UpperBound,Icon,Type,Rarity,Effects,SoundEffects,Action`
- Core buffs (moonlight, moon_phase, eclipse_mark) are simple stat trackers
- Power buffs (lunar_cycle_power, moon_crowned_witch_power, etc.) contain **inline Lua** in ApplyScript
- All power buffs share the same helper function block (duplicated)

### Career CSV
Header: `Id,SanMax,SkillScript,Animation,Vocal,Skill1,Skill2,ChoiceIcon,DollIcon,Character,Avatar,CareerImage,ActionImage1,ActionImage2,Dialogue,EmojiPath,FightWidget,Note`
- `SkillScript`: Inline Lua — initializes passive abilities and registers event hooks
- Luneya: 105 SAN, passive "Moon Reading" (每回合开始月辉+3, 格挡+6, 满月时抽1)
- Selena: 100 SAN, passive "White Tide Wound" (每回合开始全体蚀刻+3, 真伤+3)

### Relic CSV
Header: `Id,Rarity,OwnScript,FightScript,Icon,Note,Series,Tag,PackBelong`
- `FightScript`: Inline Lua with same helper function block pattern
- 8 relics at rarities 1-3:
  - moon_pool_shard (1): Combat start +12 moonlight, each turn +2
  - new_moon_lantern (1): Combat start gain New Moon +8 moonlight +10 block
  - eclipse_mirror (2): Combat start apply 12 Eclipse Mark to all enemies
  - silver_tide_bottle (2): 25% moonlight refund on spend
  - quiet_tide_bookmark (2): Gain Quiet Tide Bookmark power
  - crescent_pin (2): Gain Lunar Cycle +6 moonlight
  - four_phase_astrolabe (3): Gain Ritual of Four + New Moon +10 moonlight
  - full_moon_crown (3): Gain Moon-Crowned Witch +10 moonlight

### Card Pack CSV
Header: `Id,Type,Icon` — Simple 3-field format

### RoleData CSV
Header: `Id,Avatar,CharacterImage` — Simple 3-field format

## Text System

Standard multi-language CSV format:
- Headers: `Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja`
- Uses `{MoonRite_moonrite_moonlight}` style template references
- Career text includes `<name>` and `<des>` XML-style tags for skill descriptions
- Relic text has `Tips` (flavor text) and `Description` (mechanical text) columns

## Key Mechanics

1. **Moonlight (月辉)**: Resource state, capped at 400. Cards spend or check moonlight for scaling effects.
2. **Moon Phase (月相)**: Cycles 1→2→3→4 (New→Crescent→Full→Waning). Cards have bonus effects at specific phases.
3. **Eclipse Mark (蚀刻)**: At end of turn, take true damage = stacks, then -1. Some effects deal % damage based on stacks without removing them.
4. **Lunar Cycle (月轮)**: Each turn start, advance phase +3 moonlight + trigger Rite of Four.
5. **Rite of Four (四相仪式)**: Per-phase bonuses on phase advance (New=block, Crescent=energy, Full=damage, Waning=eclipse mark damage).
6. **Moon-Crowned Witch (月冠魔女)**: Each turn start gain 4 moonlight. During Full Moon: +1 energy, draw 1.
7. **Silver Tide Bottle refund**: Spend moonlight → refund 25%.
8. **Phases & bonuses**: New=block/defense, Crescent=draw/energy, Full=burst damage, Waning=eclipse mark synergy.

## Key Patterns & Techniques

1. **Inline Lua approach**: All card/buff/relic logic written directly in CSV cells as Lua code — no dependency on external scripts
2. **Helper function duplication**: Same Lua helper block repeated in every UseScript cell — high redundancy but self-contained per file
3. **Card type via BaseScript**: `self.Vars:set_Item("BaseScript", "AttackCardItem")` pattern for card targeting behavior
4. **Burnout mechanic**: Several cards (lunar_cycle, ritual_of_four, moon_crowned_witch) use `Burnout` tag for discard-after-use
5. **Phase-conditional effects**: `if phase == 3 then ... end` pattern for full moon bonuses
6. **Dynamic description**: Description strings updated in `InitScript` via `self:AddDescription()` with calculated values
7. **UI-only DLL fix**: Entry.dll only clamps shop relic icon size — demonstrates minimal DLL approach
8. **Documentation focus**: Extensive tutorial docs and Codex Skill included for other modders

## Extractable Lessons

1. **CSV-only mod viability**: Complete gameplay content (18 cards, 8 relics, 2 careers, 13 buffs) without writing a single line of compiled code
2. **Lua inline pattern**: Trade-off between code duplication and self-containment — each CSV file is independently readable
3. **Helper function library**: Common functions (getLight, spendLight, advancePhase, forEachEnemy) should ideally be centralized but are inlined for simplicity
4. **Phase cycle design**: Simple 1→2→3→4 cycle with unique bonuses per phase creates strategic depth without complex code
5. **Graduated scaling**: Moonlight provides % scaling (15%, 35%, 60%) for incremental power growth
6. **Relic-pack synergy**: Relics reference specific card mechanics (moonlight spend, phase advance, eclipse mark application) for natural deckbuilding synergy
