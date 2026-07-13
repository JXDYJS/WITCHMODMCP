# SunExp (日耀：烬冠天幕) — Comprehensive Content Mod Analysis

## Mod Overview

**Author**: Aura | **Version**: 0.4.2 | **Workshop ID**: 3741157062

A massive expansion mod adding:
- 2 new playable characters: **WuNa (曜日魔女)** and **Loneer (晨星魔女)**
- 5 card packs: 3 Radiance (日耀), 1 Morning Star (晨星), 1 Other Dimension (异次元)
- 40+ cards across multiple card packs
- 13 relics (日耀遗物 series)
- 21+ buffs/statuses
- Companion/familiar system: **Dusk (黄昏)** and **Star-Clay Doll (星泥人傀)** with intent AI
- 2 wax seals: 白曜 (White Radiance), 启明星 (Morning Star)
- 3 bosses + 1 hidden boss (白曜圣女·乌娜)
- **Solar Memory** (日耀回忆) story mode — boss rush narrative
- **Endless Abyss** (无尽之渊) endless mode with evolution traits
- Voice pack for WuNa, skin "夏日清凉" (Summer Cool)
- 9 difficulty modifiers related to Terrius/Other Dimension

## Directory Structure

```
SunExp/
  ModConfig.json              — Mod metadata
  .workshop-id                — Steam workshop ID
  .workshop-sync.json         — Workshop sync timestamp
  audio.registry.json         — Voice/audio provider registry
  companion.intent.registry.json — Companion AI intent pool
  endless_abyss.config.json   — Endless abyss mode config
  endless_abyss.evolution_traits.registry.json — Evolution trait pools
  familiar.blessing.registry.json — Familiar blessing definitions
  polymorph.role-crops.json   — Role polymorph crop coordinates
  starterdeck.registry.json   — Default starter decks for roles
  visual.registry.json        — Visual/shader/effect registrations
  Icon.png / WuNa_e_head.png  — Mod icons
  *.txt                       — Workshop descriptions (Chinese)
  
  Data/                       — Game data configs (CSV)
    Blessing/sunexp.csv       — Companion passive blessings
    Buff/sunexp.csv, wuna.csv — Buff definitions
    Card/sunexp.csv, loneer.csv, wuna.csv, cursecard.csv — Card definitions
    CardPack/sunexp.csv       — Card pack definitions
    Career/loneer.csv, wuna.csv — Career definitions
    Dialogue/sunexp.csv       — Dialogue/event sequences
    EnchTag/sunexp.csv        — Enchantment tag definitions
    Enemy/sunexp.csv          — Enemy definitions (3 bosses)
    EnemyCard/sunexp.csv      — Enemy action cards
    EventList/sunexp.csv      — Event definitions (Solar Memory)
    Hard/sunexp.csv           — Difficulty modifiers
    Level/sunexp.csv          — Level/battle configurations
    Map/sunexp.csv            — Map node definitions
    Partner/sunexp.csv        — Companion definitions
    PartnerCard/sunexp.csv    — (Empty) companion action cards
    Relic/sunexp.csv          — Relic definitions
    RoleData/loneer.csv, wuna.csv, solar_memory.csv — Role display data

  Text/                       — Localized text (mirrors Data/ structure)
    <same subdirs as Data/>   — With multi-language columns

  ModResource/                — Custom resources
    AnimationLib/             — Character animations (WuNa, Loneer, Dusk, SecondSunWeel_e, WuNa_e)
    Images/                   — Sprites: Avatar, Buff, Card, CardPack, CareerImage, CG, Character, dialog, Dialogue, Effects, Icon, MapNode, Partner, Relic, Role, Skill, UI
    VisualBundles/            — Shader/material asset bundles

  SharedResources/            — Shared resource package
    Audio/WuNa/               — Voice audio files (.wav)
    CG/                       — CG images (Loneer, WuNa, BlazingCrownCollapse sequence)
    Skins/                    — Skin: summer_cool
    package.json              — Resource package manifest
    cg.registry.json          — CG registry entries

  Scripts/
    Entry.dll                 — C# DLL mod entry point
    Aura.Shared.dll           — Shared utilities DLL
```

## Data Config Format

### Card CSV
Header: `Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action,PackBelong`
- `InitScript`: Always calls `CS.SunExp.Dll.Scripting.CardScripts.Init(self, "<id>")` — delegates behavior to C# DLL
- `UseScript`: Delegates to `CardScripts.Use(self, "<id>")` 
- `Tag`: Comma-separated keywords (白曜, Burnout, Retain, Nihility, Froze)
- `PackBelong`: References the cardpack ID
- `*` prefix = generated/derived card (not in pool)

### Buff CSV
Header: `Id,InitScript,ApplyScript,ClearScript,ReducePerTurn,ReducePerAttacked,ReducePerUse,UpperBound,Icon,Type,Rarity,Effects,SoundEffects,Action,CanZero`
- `Type`: 正面/负面/能力/特性/场地 (Positive/Negative/Power/Trait/Field)
- Scripts delegate to `CS.SunExp.Dll.Scripting.BuffScripts`

### Career CSV
Header: `Id,SanMax,SkillScript,Animation,Vocal,Skill1,Skill2,ChoiceIcon,DollIcon,Character,Avatar,CareerImage,ActionImage1,ActionImage2,Dialogue,EmojiPath,AttackEffect,SkillEffect,HitEffect,DefendEffect`
- `SkillScript`: `InitCareer(self)` — initialization via C# DLL
- `Skill1`, `Skill2`: Career skill card IDs

### Relic CSV
Header: `Id,Rarity,OwnScript,FightScript,Icon,PackBelong`
- `FightScript`: `RelicScripts.Fight(self, "<id>")` with C# DLL delegation
- Rarity 1-4

### Enemy CSV
Header: `Id,Name,Hp,Attack,Defend,ActionCount,Rarity,InitScript,CardList,AttributeText,Animation`
- `CardList`: Comma-separated enemy card IDs
- `AttributeText`: References a boss trait buff ID

### Card Pack CSV
Header: `Id,Type,Icon` — Simple 3-field format

### RoleData CSV
Header: `Id,Avatar,CharacterImage,HouseAvatar,DefaultY,DefaultScale`

## Text System

Multi-language CSV format (all Text/ files):
- Headers: `Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja`
- Uses `{buff_burn}` and `{SunExp_sunexp_solar_radiance}` style format references
- Keywords/buff IDs referenced via `{...}` template syntax for dynamic text rendering
- Each text row has an "是否完成" (completion status) flag

## Registry File Patterns

### `audio.registry.json`
- Schema v2, lines 2-137
- `ownerModId`: "SunExp"
- `providers[]`: Audio provider definitions
  - `kind`: Event type (`CareerSelected`, `LowHealth`, `BattleCompleted`, custom)
  - `policy`: `ReplaceOriginal` or `Additive`
  - `match`: Filter by `careerIds`, `roleIds`, `hpRatioCrossDown`, `battleResults`
  - `bus`: "Vocal"
  - Path format: `Shared:Audio/SunExp/WuNa/<file>.wav`
- `battleBgmProviders[]`: Empty (would contain BGM overrides)

### `companion.intent.registry.json`
- Schema v3, lines 2-113
- `intents[]`: Companion AI actions
  - `id`: Unique intent ID
  - `enemyCardId`: Links to an enemy card that represents the action
  - `type`: `Attack`, `Defense`, `Interference`, `Support`, `Recovery`
  - `handlerId`: Handler type (`damage.single`, `block.single`, `buff.apply`, `heal.single`, `damage.multi`)
  - `target`: Targeting config with `scope`, `mode`, `policy`
  - `cost`/`cooldown`: Action economy
  - `basePriority` + `priorityBonus`: AI decision weighting
  - `threat`: Threat system with `preview`/`onUse`/`decay`
  - Scaling values: `flatValue`, `attackScale`, `magicScale`
- `profiles[]`: AI behavior profiles
  - `roleId`: `"*"` (wildcard for all)
  - `attackTendency` / `defenseTendency`: Intent ordering
  - `attackWeight` / `defenseWeight`: Overall aggression ratio

### `familiar.blessing.registry.json`
- `blessings[]`: 33 blessing definitions
  - `id`, `name`, `description`, `iconPath`, `tier` (1-5), `weight`, `pool`
  - `allowedSpecies[]`: Which companion species can use it (`["*"]` = universal)
  - `exclusiveGroup`: Mutual-exclusion grouping
  - `effects[]`: Effect entries with `kind`, `amount`, `value`
  - Effect kinds: `CombatStartShield`, `CombatStartHeal`, `BattleWinGold`, `CombatStartDraw`, `CombatStartBuff`, `BurnTriggeredEmber`, etc.

### `starterdeck.registry.json`
- Schema v1
- `profiles[]`: Default deck overrides for specific modes
  - `profileId`, `displayName`, `modeIds[]`, `targetRoleIds[]`, `deckSize`, `priority`
  - `cardIds[]`: Cards in the starting deck

### `visual.registry.json`
- Schema v1, 391 lines
- `textures[]`: Custom sprite registrations
- `modeEntries[]`: Custom mode title art (solar_memory, endless_abyss)
- `frameAnimations[]`: Animated sprites (blessing icons, buff icons, enemy dictionary)
  - `targetKind`: `blessing-icon`, `buff-icon`, `enemy-dictionary-icon`
  - `frameSeconds`, `framePaths[]`
- `mapNodeArt[]`: Custom map node art per enemy/level
- `shaders[]`: Custom shader registration
- `effects[]`: Material effects (ui-material, card-visual-face-material, character-orbit-material, etc.)
  - `kind`: Material type
  - `shaderId`: References a shader
  - `textures`, `floats`, `colors`: Material property overrides

### `cg.registry.json`
- `entries[]`: CG display events
  - `cgId`, `displayName`, `kind` (`skill`, `cardUse`, `feast`)
  - `targetRoleIds[]`, `cardIds[]` — trigger conditions
  - `media`: `{type, resource, fallbackImage, bundlePath, frameSeconds, ...}`
  - `defaultPresentation`: `{mode, fit, fadeIn, hold, fadeOut, focusX, focusY}`
  - `defaultActivation`: `{enabled, consumerMode, consumerModId}`

### `endless_abyss.config.json`
- Schema v1
- `gaze`: Abyss gaze level progression config
- `shock`: Shock mechanic configs
- `rewardPools[]`: Reward pool with type/source filtering

### `endless_abyss.evolution_traits.registry.json`
- `pools[]`: Pool with weighted buff entries for endless mode evolution

### `SharedResources/package.json`
- Package manifest with `capabilities` (Audio, CG, Skin, Journey, RolePack, MultiplayerAuthority)
- Resource entries with `system`, `resourceId`, `kind`, `source`, `destination`, `targetRoleIds[]`, `metadata`

## Resource Management

- Mod resources served from `ModResource/` — path prefix: `Mods/SunExp/ModResource/...`
- AnimationLib: Per-character animation folders (Idle, Attack, Defend, Hit, Skill, Dict, Map) with config.json and PNG spritesheets
- Shared resources via `SharedResources/` package system:
  - Audio/WuNa/: Voice lines packaged as Shared: resource
  - CG/: Skill CG, Feast CG, and animated sequence CG (BlazingCrownCollapse)
  - Skins/: summer_cool skin with per-action animation folders
- Path convention: `Mods/<ModName>/ModResource/<Category>/<Subdir>/<File>`
- Texture atlas: `_sunexp_atlas.png`, `_sunexp_source_atlas.png`

## Key Patterns & Techniques

1. **C# DLL front-end + CSV data back-end**: All card/buff/relic/enemy scripts delegate to `CS.SunExp.Dll.Scripting.*Scripts.*()` methods, putting game logic in DLL while configuration stays in CSV
2. **ID Namespacing**: All IDs prefixed with `SunExp_sunexp_`, `SunExp_wuna_`, `SunExp_loneer_` to avoid collisions
3. **Modular card packs**: 5 card packs with distinct mechanical identities (Spark = basic, Ember Crown = self-burn burst, Solar Canopy = enemy burn spread, Morning Star Overture = star score/compose, More Dimensions = polymorph/projection)
4. **Custom mechanics**: Solar Radiance stacking, Scorching Canopy field, Gathered Flame conversion, Crown tier system, Star Score cadence system, Polymorph transformations
5. **Story-driven content**: Solar Memory mode with 6 narrative events + 3 boss encounters, full dialogue system with branching choices
6. **Companion system**: AI companions with intent-based action selection, distinct species (Dusk = burn synergy, Star-Clay Doll = starlight/survival), blessing system with tiered effects
7. **Shader/Visual system**: Custom shaders with extensive parameter tuning, animated frame-based sprite effects, card foil materials with 30+ exposed float parameters, orbit fire character effects
8. **Audio system**: Voice line registry with battle event triggers, LowHealth cooldown, career selection, battle completion
9. **Endless mode**: Scalable difficulty with milestone rewards, shock mechanics, evolution trait pools, abyss gaze debuff tiers

## Extractable Lessons

1. **Separation of data and logic**: CSV for config/data, C# DLL for implementation — allows asset-only mods to coexist with code mods
2. **Registry pattern**: JSON registries for extending game systems (audio, visuals, companions, CG, starter decks) without modifying the base game
3. **ID convention**: `ModName_subfolder_id` for all game objects, `*` prefix for generated/internal cards
4. **Multi-language text**: 4 languages (zh-CN, zh-TW, en, ja) in single CSV with template variable references
5. **Resource packaging**: Shared resource packages with role targeting, capability tagging, and metadata
6. **AI intent system**: Structured action definitions with targeting policies, priority bonuses, cooldown, cost, and threat/aggro management
7. **Modular mechanics**: Each card pack as a self-contained mechanical module with its own relics, buffs, and tags
