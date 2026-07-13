# EdictOfStars (群星律令) — Mod Analysis

**Author:** 雪樱 + 天耀 + codexgpt5.5 (AI-assisted)  
**Version:** V2.3.0  
**Workshop ID:** 3742129366  
**Tags:** 角色, 事件, 卡包, 中文, 英文, 日语  
**Mod Type:** Lua-only content mod (no DLL)

---

## Mod Overview

A constellation-themed card pack mod that adds high-frequency/multi-hit damage mechanics, synergizing with the game's existing bleeding system. Contains:

- **1 New Character:** Ishtar (伊丝塔尔/Stellar Oracle) — cat-eared oracle girl
- **16 Constellation Cards** (8 Astral Companion cards, 2 Buried cards)
- **1 Character Exclusive Skill Card:** Stellar Oracle (星予神谕)
- **4 Relics:** Northern/Southern Star Codex, Star Bloom Sea, Star Stone
- **4 Buffs:** Bloodstain (辉刻, negative), Blooming (星象, ability), Duel (律令死斗, ability), Ishtar Oracle (ability)
- **3 Blessings:** Star Envoy, Star Rune Absorption, Lyra Tuning
- **1 Partner:** Star Familiar (观星猫, observation cat)
- **1 Map Event:** Falling Star Embers (坠星余烬)
- **2 Custom Mechanics:** Astral Companion (伴星), Buried (埋藏)

---

## Directory Structure

```
EdictOfStars/
  Data/
    Blessing/starblessings.csv
    Buff/starbuffs.csv
    Card/starcards.csv, starcareercards.csv
    CardPack/starpack.csv
    Career/starcharacters.csv
    EventList/starevents.csv
    Map/starmaps.csv
    Partner/starpartners.csv
    Relic/starrelics.csv
    RoleData/starcharacters.csv
  ModResource/
    AnimationLib/Ishtar/ (Attack, Defend, Hit, Idle, Skill — sprite sheets)
    AnimationLib/StarFamiliar/ (Idle only)
    Icon/Blessing/, Buff/, Card/
    Images/Avatar/, CardPack/, CareerImage/, Character/, Icon/, Partner/, Relic/
  Scripts/Entry.lua  (main logic, 2500+ lines)
  Text/ (mirrors Data/ structure with localization)
  .workshop-id, .workshop-sync.json
  Icon.png, ModConfig.json
```

---

## Entry Point Analysis — Entry.lua

**Engine:** Lua (xLua/XLua) — the game embeds a Lua scripting engine via xLua.  
**Mod API:** `ModConfig:Setup()` is the Lua entry point (provided by the game's mod framework), but EdictOfStars does NOT define `ModConfig:Setup()`.

### Architecture

The mod uses pure Lua globals (`SB` table, `SB_*` functions) called from CSV card/buff/relic scripts via the game's xLua bridge. Card `UseScript` fields reference functions like `SB_Xingshuo(self)`, `SB_Weiguang(self)`, etc.

### Key Patterns

1. **Global State Module (`SB` table):**  
   All IDs and state are stored in the `SB` global table. Constants use a `SB.` prefix pattern.

2. **Safe Interop Utilities:**  
   - `SB_Warn()`, `SB_SafeCall()` for error handling  
   - `safe_prop(obj, prop)` — safe property access via `pcall`  
   - `dict_get/dict_set` — dictionary operations with fallback mechanisms  
   - `list_count/list_item` — C# list/array interop with fallback to Lua tables  
   - `for_each_status/for_each_card` — C# enumerator iteration with fallback to indexed access

3. **Runtime ID Pattern:**  
   All IDs follow the convention: `<ModFolder>_<CsvFileName>_<RawId>`  
   Example: `EdictOfStars_starcards_032100007`, `EdictOfStars_starbuffs_bloodstain`

4. **Bloodstain (辉刻) System:**  
   A custom debuff that amplifies multi-hit damage. Tracked through:
   - `SB.BLOOD_REGISTERED` table for per-fight status tracking
   - `SB.BLOOD_PULSE_LOCK` to prevent recursive triggers
   - Blood level management (`set_blood_level`, `blood_level`)
   - Integration with relics like Star Bloom Sea (RelicSea) for HP-on-blood

5. **Astral Companion (伴星) System:**  
   - 8 specific cards designated as companions (`SB.COMPANION_CARDS`)
   - `create_card_id_to_hand` creates cards to the player's hand via FightUI
   - Card drawing from draw pile, with draw/remove/hide/refresh cycle
   - Extracting state management (`COMPANION_EXTRACTING`, `COMPANION_GENERATED_SKIP`)

6. **Star Stone Map Integration:**  
   - Procedurally inserts a special event node into the map
   - 20% chance (`SB.STARSTONE_RUN_CHANCE_PER_10000 = 2000`)
   - Route logging and placement tracking

7. **Duel (律令死斗) System:**  
   - Adds "Burnout" tag to all hand cards
   - Self-damage mechanic (`SB.DUEL_SELF_DAMAGE = 10`)
   - No-heal debuff (`SB.DUEL_BUFF`)

8. **Shop UI Fix:**  
   - `SB_ResizeShopRelicIcon` resizes relic icons in shop to `80x80`  
   - Checks `shop_item_is_star_relic` to only target mod relics

---

## Data Config Format

CSV convention (Row 1 = English header, Row 2 = Chinese description, Row 3+ = data):

### Card CSV
```
Id,Rarity,Expend,Tag,PackBelong,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action
```
- `Id` with `*` prefix = hidden from normal reward pool (character skill cards)
- Tags: `Retain`, `Burnout`, `Ascension`, `Combo` etc.
- Script references use Lua global function names

### Buff CSV
```
Id,InitScript,ApplyScript,ClearScript,ReducePerTurn,ReducePerAttacked,ReducePerUse,UpperBound,Icon,Type,Rarity
```
- Types: `负面` (negative), `能力` (ability), `正面` (positive)
- Buff scripts called on apply/clear

### Relic CSV
```
Id,Rarity,OwnScript,FightScript,Icon,Note,Series,Tag,PackBelong
```
- `FightScript` runs at combat start
- `PackBelong` ties relics to the card pack

### Career CSV
```
Id,SanMax,SkillScript,Animation,Vocal,Skill1,Skill2,ChoiceIcon,DollIcon,Character,Avatar,CareerImage,...
```

### Card Pack CSV
```
Id,Type,Icon
```
- `Type`: `Normal`

---

## Text System

All text CSVs mirror their Data/ counterparts under `Text/`. Each card/buff/career has:
- `Id`, `Note`, `Type`, `Name`, `Name_en`, `Name_zh-Hant`, `Name_ja`
- `Description`, `Description_zh-Hant`, `Description_en`, `Description_ja`

**Localization:** Supports Chinese (Simplified), English, Chinese (Traditional), and Japanese.

---

## Resource Management

### Animation System
Frame-by-frame sprite sheets under `ModResource/AnimationLib/<CharacterName>/<Action>/`:
- Actions: `Attack`, `Defend`, `Hit`, `Idle`, `Skill`
- Each folder has `config.json`:
  ```json
  {"AnimationPerFrame": 0.09, "isLoop": false, "Direction": "Right"}
  ```
- Idle is looped (`isLoop: true`), others are single-play

### Icon/Image Paths
- Full paths in CSV: `Mods/EdictOfStars/ModResource/Icon/Card/xingshuo`
- The game appends extensions automatically (`.png`, `.jpg`, `.jpeg`)

---

## Key Patterns & Techniques

1. **No `ModConfig:Setup()`** — pure CSV-driven mod. All hooks are in CSV script fields.
2. **Extensive Safe Interop:** Every C# interaction is wrapped in `pcall()` with fallback strategies
3. **Global State:** The entire mod state is in the Lua `SB` table
4. **Runtime ID Construction:** `ModFolder_CsvFile_RawId`
5. **Custom Card Mechanics:** Companion cards, Buried cards, Ascension, Bloodstain
6. **Lua-defined Helper Libraries:** Status iteration, card manipulation, buff management, fight UI interaction

---

## C#/Lua Interop

- C# objects accessed via `CS.<Namespace>.<Class>` path
- Common interop objects: `CS.ScriptExecutor.PlayerInfo`, `CS.FightManager.Instance`, `CS.GameConfigManager.Instance`, `CS.FightCardManager.Instance`, `CS.UnityEngine.Debug`, `CS.Witch.UI.UIManager.Instance`
- Lua-to-C# method calls via `pcall(function() obj:Method(args) end)`
- Lua functions called from CSV scripts are stored in globals (e.g., `SB_Xingshuo`)

---

## Extractable Lessons

1. **Lua-only mods are viable** for content-heavy mods (cards, buffs, relics, events)
2. **CSV is the primary data format** — it drives all content registration
3. **Runtime IDs** must be globally unique and follow the `<ModFolder>_<CsvFile>_<RawId>` pattern
4. **Lua safety wrappers** are essential — the xLua bridge can fail silently on nil objects
5. **Animation config** is simple JSON with FPS, loop, direction settings
6. **Card tags** drive behavior: `Retain` (stays in hand), `Burnout` (consumed on use), `Ascension` (custom mod mechanic)
7. **PackBelong** is critical — without it, cards/relics default to the basic pack
