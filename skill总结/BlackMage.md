# BlackMage Mod Analysis

## Mod Overview

**Author**: S0RA
**Version**: 1.1
**Workshop ID**: 3744506022
**Description**: Adds a "Black Mage" card pack to the game, bringing Final Fantasy XIV-inspired black magic (Astral Fire / Umbral Ice elemental rotation, MP management, Thunder DoT) as a neutral card pack (not a new career).

The mod implements a full MP system with a maximum of 100, an elemental dichotomy (Fire → Astral Fire, Ice → Umbral Ice), a Thunder DoT mechanic, and a culminating Elemental Star system that tracks which element you have 3-stacked to unlock Soul Resonance → Flare Star or Frost Star transformations.

This is a **card-pack-only mod** — no new career/character, no DLL. All logic is in CSV-embedded Lua scripts and the Entry.lua hook.

## Directory Structure

```
BlackMage/
  ModConfig.json               # Mod metadata, workshop config
  Icon.png                     # Workshop icon
  Audio.mp3                    # Mod audio resource
  .workshop-id                 # Steam workshop ID file
  .workshop-sync.json          # Workshop sync metadata
  Scripts/
    Entry.lua                  # Mod entry point (hook + resource management)
    Entry.dll                  # (unused? Present but all logic is in Lua)
  Data/
    Card/blackmage.csv         # 18 card definitions (blizzard, fire, thunder, etc.)
    Buff/blackmage.csv         # 12 buff definitions (astral_fire, umbral_ice, mp, etc.)
    Relic/blackmage.csv        # 2 relic definitions (ether, super_ether)
    CardPack/blackmage.csv     # 1 card pack definition
  Text/
    Card/blackmage.csv         # Card localization (zh-Hans, zh-Hant, en, ja)
    Buff/blackmage.csv         # Buff localization
    Relic/blackmage.csv        # Relic localization
    CardPack/blackmage.csv     # Card pack localization
    KeyWordsDic/BlackMage_keywords.csv  # Keyword definitions
  ModResource/
    Images/
      Card/BlackMage/          # 17 card icon PNGs
      Buff/BlackMage/          # 9 buff icon PNGs
      Cardpack/                # Card pack icon
      Relic/BlackMage/         # 2 relic icon PNGs
```

**Notable**: No `AnimationLib` directory (this mod doesn't add a playable character with battle animations).

## Entry Point Analysis (`Entry.lua`)

The Entry.lua is concise (49 lines) and focuses on a single responsibility: **ensuring the Black Mage's UI-independent resource system (MP, Elemental Star) is initialized at the right moment**.

### Mod Registration Pattern

```lua
function ModConfig:Setup()
    self:AddMethodHookAfter("Fight_PlayerTurn.Init", function(_)
        EnsurePlayerResources()
    end)
end
```

- Uses `self:AddMethodHookAfter` to hook into `Fight_PlayerTurn.Init` — this fires after the player's turn begins in combat.
- No `self:ModifyDataConfig` calls (data is pre-configured in CSVs).
- No `self:RedirectSourcePath` calls (images referenced by absolute paths in CSVs).

### EnsurePlayerResources Logic

1. **MP Buff**: Checks if the player has `BlackMage_blackmage_mp`. If not, adds it with 40 initial MP.
2. **Start MP Bonus**: If a `start_mp_bonus` buff exists (from relics Ether/Super Ether), consumes it to boost MP, capped at 100.
3. **Elemental Star**: Checks for `grants_elemental_star`. If absent, creates it with the correct level based on current Astral Fire/Umbral Ice stacks (via `GetElementalStarLevel`).

### GetElementalStarLevel Logic
- Returns 2 if Umbral Ice ≥ 3
- Returns 3 if Astral Fire ≥ 3
- Returns 1 otherwise

## Data Config Format

### Card Data (`Data/Card/blackmage.csv`)

**Columns**: `Id`, `Rarity`, `Expend`, `Tag`, `PackBelong`, `InitScript`, `DrawScript`, `UseScript`, `DropScript`, `Icon`, `Effects`, `Action`

- **Id**: Unique card identifier (e.g., `blizzard`, `fire`, `thunder`). Prefixed with `*` for non-collectible cards (e.g., `*flare_star`, `*frost_star`).
- **Rarity**: 1-3 (basic-rare).
- **Expend**: Energy cost.
- **Tag**: Comma-separated tags (e.g., `冰魔法`, `火魔法`, `Burnout`, `Recycle,Retain`).
- **PackBelong**: Full ID of the card pack (`BlackMage_blackmage_cardpack_blackmage`).
- **InitScript**: Lua code that runs to set up the card's display (Vars, dynamic description). Sets `BaseScript` to `"AttackCardItem"` or `"CommonCardItem"`.
- **UseScript**: Lua code that runs on card use. Handles targeting (`self:SetStatus("Target")`, `self:SetStatus("Self")`, `self:SetStatus("AllTarget")`), damage, buff application, MP consumption.
- **Icon**: Path to icon (`Mods/BlackMage/ModResource/Images/Card/BlackMage/...`).

**Key pattern**: Lua code is inlined in CSV cells, double-quoted and with `""` for escaped quotes inside. This is the standard Witch modding approach.

### Buff Data (`Data/Buff/blackmage.csv`)

**Columns**: `Id`, `InitScript`, `ApplyScript`, `ClearScript`, `ReducePerTurn`, `ReducePerAttacked`, `ReducePerUse`, `UpperBound`, `Icon`, `Type`, `Rarity`, `Effects`, `SoundEffects`, `Action`

- **ApplyScript**: Lua code that runs when the buff is applied. Used to register event handlers via `self:AddEvent()`.
  - `"StartRound"` — at start of each round
  - `"ActionAfter"` — after the player acts
  - Custom event names like `"BlackMage_blackmage_astral_fireOnLevelChange"`
- **ClearScript**: Runs on buff removal.
- **ReducePerTurn/Attacked/Use**: Stack decay mechanics.
- **UpperBound**: Max stacks (e.g., 3 for astral_fire, 100 for mp).
- **Type**: `能力` (ability) — standard buff type.
- **CanZero** (optional column, absent here): Whether the buff can exist at 0 stacks.

**Notable buff: `mp`**
- On `StartRound`: Regenerates MP to at least 40, caps at 100, processes `start_mp_bonus`.
- On `ActionAfter`: Removes the buff if MP drops to 0.

**Notable buff: `grants_elemental_star`**
- A "passive tracker" buff. On every relevant event (element level change, start round, action after), it refreshes the elemental star level by checking astral_fire and umbral_ice stacks.
- Uses `self:RunImmediately("BlackMage_blackmage_grants_elemental_star", "BlackMage_refresh_elemental_star")` to trigger a re-evaluation of a different buff.

**Notable buff: `buff_thunder`**
- On `ActionAfter`: Deals damage = level² to all enemies as true damage (`"True"` damage type), then decrements. This is the Thunder DoT.

### Relic Data (`Data/Relic/blackmage.csv`)

**Columns**: `Id`, `Rarity`, `OwnScript`, `FightScript`, `Icon`, `PackBelong`

- **OwnScript**: Runs on acquisition.
- **FightScript**: Runs at fight start, uses `self:AddEvent("FightStart", ...)` to register effects.

Both relics (Ether, Super Ether) use `FightStart` to add `start_mp_bonus` buff.

### CardPack Data (`Data/CardPack/blackmage.csv`)

**Columns**: `Id`, `Type`, `Icon`

- **Type**: `Normal` (standard card pack).
- **Icon**: Path without extension.

## Text System

### Localization Format

All text files share a similar structure with multi-language columns:

**Card Text**: `Id`, `是否完成`, `Type`, `Note`, `Name`, `Name_en`, `Name_zh-Hant`, `Name_ja`, `Description`, `Description_zh-Hant`, `Description_en`, `Description_ja`

- `{0}`, `{1}`, etc. are format placeholders replaced by values from `InitScript`'s `self:AddDescription()` calls.
- `{buff_burn}`-style references are replaced with the actual buff name in the UI.

**Buff Text**: `Id`, `Note`, `Name`, `Name_zh-Hant`, `Name_en`, `Name_ja`, `Description`, `Description_zh-Hant`, `Description_ja`, `Description_en`

**Relic Text**: `Id`, `Note`, `Series`, `Tag`, `Name`, `Name_zh-Hant`, `Name_en`, `Name_ja`, `Tips`, `Tips_zh-Hant`, `Tips_en`, `Tips_ja`, `Description`, `Description_zh-Hant`, `Description_en`, `Description_ja`

- `Tips` is the lore/flavor text (separate from mechanical `Description`).

**CardPack Text**: Similar multi-language format with `Name` and `Description`.

**KeyWordsDic**: `Id`, `Note`, `Description`, `Keywords`, `Keywords_zh-Hant`, `Keywords_en`, `Description_zh-Hant`, `Description_en`, `Keywords_ja`, `Description_ja`, `ShouldShow`

- References like `{keyword_BlackMage_fire_magic}` in card descriptions link to keyword popups.

### Localization Pattern

- Row 1: Column headers.
- Row 2: Chinese field descriptions/comments.
- Row 3+: Data.

## Resource Management

### Image Path Convention

All image paths in CSV data use the format: `Mods/BlackMage/ModResource/Images/Category/Subfolder/filename`

No `.png` extension is included in the CSV paths (the game's mod system appends it automatically).

### Categories
- `Images/Card/BlackMage/` — Card icons (17 PNGs)
- `Images/Buff/BlackMage/` — Buff icons (9 PNGs)
- `Images/Cardpack/` — Card pack selection icon
- `Images/Relic/BlackMage/` — Relic icons (2 PNGs)

## Key Patterns & Techniques

### 1. **UI-Independent Resource System (MP)**
The Black Mage has no career with a special UI. Instead, it implements MP as a buff with a level that represents current MP. The `mp` buff:
- Acts as both the resource storage (level = current MP, max 100)
- Includes auto-regeneration logic in its `ApplyScript`
- Is checked by all fire-school cards before execution
- Is removed at 0 and re-added at the start of next round with 40

### 2. **Conditional Card Execution**
Fire-school cards (fire, fire_plus, flare, despair, high_fire) check MP before executing:
```lua
local mp = self.Self:GetBuff("BlackMage_blackmage_mp");
if mp == nil or mp.buffConfig.Level < mpCost then return; end
```
This causes the card to "fizzle" (return early, no effect, card consumed) if insufficient MP.

### 3. **Dual-Phase Card Execution via InitScript**
Cards use `InitScript` to dynamically compute and display descriptions:
```lua
self.Vars:set_Item("BaseScript", "AttackCardItem");
local damage = 3;
local astral = ...;
if astral ~= nil then damage = math.floor(damage * (1 + astralLevel * 0.5)); end;
self:AddDescription("1", "Damage", tostring(damage));
```
Then `UseScript` recalculates for actual execution. The `InitScript` runs at display time, `UseScript` at play time.

### 4. **Elemental Stance System via Buff Tracking**
- `astral_fire` and `umbral_ice` are mutually exclusive stance buffs
- Fire magic grants astral_fire, ice magic grants umbral_ice
- Using the opposite element consumes the current stance
- `grants_elemental_star` automatically tracks which element is stacked to 3 and exposes a "star level" (1=neutral, 2=umbral, 3=astral)
- Events like `"BlackMage_blackmage_astral_fireOnLevelChange"` are custom events used for cross-buff communication

### 5. **Umbral Hearts Resource Reduction**
`umbral_hearts` is a "resource token" that reduces MP cost by 50% for fire magic when in Astral Fire stance. It's consumed on use.

### 6. **Astral Soul / Elemental Star Ultimate**
`soul_resonance` reads the Elemental Star level and behaves differently:
- Level 2 (3 Umbral Ice): Damage = shield * current MP
- Level 3 (3 Astral Fire): Damage = astral_soul_level * (100 - current_MP)

This is a setup → payoff design where the player builds resources then unleashes with a finisher card.

### 7. **Thunder DoT System**
`thunderhead` is a passive enabler buff (1 max stack). Thunder cards check for it to apply `buff_thunder` DoT stacks. The DoT deals quadratic damage (level²) each action and self-decrements.

### 8. **Card Retrieval (Tutor) Pattern**
Ice cards use `self:AddCardByCardList("1", "火魔法")` to search the deck for a fire magic card. This is a "tutor" mechanic that provides deck consistency.

### 9. **Hook Strategy (Entry.lua)**
Single hook on `Fight_PlayerTurn.Init` with `AddMethodHookAfter`. This ensures resources are set up every turn without interfering with the game's normal flow. The `_` parameter pattern indicates the hook receives but ignores the method's context/arguments.

### 10. **Buff-Level Caps**
All MP-related buffs cap at 100 via `math.min(100, ...)`. Astral Fire and Umbral Ice cap at 3. This is enforced both in cards and in the buff's event handlers.

## C#/Lua Interop

- **No custom DLL logic**. `Entry.dll` exists in the Scripts folder but all functional code is in Lua/CSV.
- Uses `CS.FightPlayer.Instance` and `CS.ScriptExecutor.PlayerInfo` for game state access.
- Uses `self:Damage()`, `self:AddBuff()`, `self:RemoveBuff()`, `self:AddDescription()`, `self:SetStatus()`, `self:ChangeDefence()`, `self:AddCardByCardList()`, `self:DrawCount()` from the card execution context.
- `self.Self` = the player status, `self.Target` = selected target.
- Uses `self.dataConfig.Vars` (a key-value dictionary) for per-instance card state.
- Uses `self:RunImmediately(buffId, eventName)` to trigger a buff's event handler directly.
- The `xlua` framework manages Lua-to-C# binding automatically.

## Extractable Lessons

1. **Buff-as-resource**: You can implement custom resources (MP, combo points, etc.) using buffs with `UpperBound` caps, attaching regeneration/decay logic to `StartRound`/`ActionAfter` events.

2. **Stance system**: Mutually exclusive stances are implemented as two buffs where each card type checks for and removes the opposing stance. A third "tracker" buff (elemental_star) cross-references both.

3. **Conditional card fizzle**: Cards can "fizzle" by returning early from `UseScript` when a resource check fails. The card is consumed but has no effect.

4. **Rich localization**: The game supports 4 languages (Simplified Chinese, Traditional Chinese, English, Japanese). Text CSVs use `{0}` placeholders for dynamic values. Buff names can be referenced in card/text descriptions via `{buff_buffname}` syntax.

5. **Dynamic descriptions**: `InitScript` recalculates display values each time the card is shown (hand, reward screen, etc.) using `AddDescription(key, label, value)`.

6. **No animation requirement**: Card-pack-only mods don't need an `AnimationLib` directory. Only career/character mods need that.

7. **CSV-as-database**: The game's mod system uses CSVs as the primary data format. Column 1 is headers, column 2 is comments, data starts at column 3.

8. **Naming convention**: All IDs follow `ModName_FileNameInFolder_CardId` pattern (e.g., `BlackMage_blackmage_cardpack_blackmage`). This ensures uniqueness across mods.

9. **Workshop configuration**: `ModConfig.json` supports `PublishedFileId` and `WorkshopTags` for Steam Workshop integration. Tags include "角色" (character), "卡包" (card pack), language tags.

10. **Entry.lua minimalism**: The entry point needs only the hooks that can't be expressed in CSV data. The actual game logic lives in the CSV cells.
