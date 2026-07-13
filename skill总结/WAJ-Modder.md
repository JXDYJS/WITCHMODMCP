# WAJ-Modder Repository — Comprehensive Analysis

**Repository**: [Mr-Pardon/WAJ-Modder](https://github.com/Mr-Pardon/WAJ-Modder) (MIT License, copyright 2026 Mr_Pardon)
**Purpose**: Unofficial Codex skill for creating and validating Lua-template mods for *Witch's Apocalyptic Journey* (女巫：终末旅途) from natural-language ideas.
**Status**: v0 — outputs are "testable drafts", not guaranteed final releases.

---

## Overall Structure

```
WAJ-Modder/
├── SKILL.md                  # Main skill definition (Codex/Agent instructions)
├── README.md                 # Bilingual (EN/CN) project description
├── NOTICE.md                 # Legal disclaimers
├── LICENSE                   # MIT License
├── agents/
│   └── openai.yaml           # Agent display config (3 lines)
├── references/               # 9 reference documents
│   ├── boundaries.md
│   ├── mod-structure.md
│   ├── csv-schemas.md
│   ├── lua-patterns.md
│   ├── mechanic-patterns.md
│   ├── asset-style-guide.md
│   ├── assets-and-publishing.md
│   ├── cardpack-cover-layout.md
│   └── cardpack-cover-prompt-examples.md
├── scripts/                  # 6 Python helper scripts
│   ├── create_mod.py
│   ├── ensure_tutorial.py
│   ├── validate_mod.py
│   ├── finalize_cardpack_cover.py
│   ├── compose_cardpack_cover.py
│   └── locate_game.py
├── assets/                   # 4 image assets
│   ├── buff-border-atlas.png
│   ├── cardpack-cover-base-300x440.png
│   ├── cardpack-cover-frame-300x440.png
│   └── cardpack-cover-silhouette-300x440.png
└── docs/images/              # 5 demo screenshots
    ├── ashen-ledger-cardpack-success.png
    ├── cards-sheet-generated.png
    ├── buffs-sheet-generated.png
    ├── relics-sheet-generated.png
    └── generate-result.png
```

**Key design principles**:
- Lua-template mods by default (not C# DLL hooks)
- Data-driven via CSV files (`Data/**/*.csv`, `Text/**/*.csv`)
- ModTemplate from the official tutorial repo as source of truth
- Image generation with deterministic post-processing scripts
- IDs follow `<ModFolder>_<CsvFileName>_<RawId>` pattern for mod content

---

## SKILL.md Analysis

### Structure
- **YAML frontmatter**: name (`waj-modder`), description for Codex
- **Purpose section**: Turn mod ideas into Lua-template testable mod projects
- **First Response Workflow**: 7-step orchestration flow for the AI
- **Core Build Rules**: 12 concrete rules for generating mod content correctly
- **Deliverables**: What files to produce (ModConfig.json, CSVs, Entry.lua, ModResource, test checklist)
- **Asset Policy**: Visual style guardrails, card-pack cover workflow, relic icon (128x128), Buff icon (31x31)
- **Publishing Policy**: End-to-end Steam Workshop publishing assistance
- **Script Usage**: CLI examples for 5 bundled scripts

### Key Tools/Commands Exposed

| Script | CLI Pattern | Purpose |
|--------|-------------|---------|
| `ensure_tutorial.py` | `--root <workspace-root>` | Find or clone official tutorial repo |
| `create_mod.py` | `--out <dir> --name <N> --author <A> --description <D>` | Copy ModTemplate and configure |
| `validate_mod.py` | `<path-to-mod>` | Run static checks on a mod |
| `finalize_cardpack_cover.py` | `<input> <output>` | Finalize 300x440 cover with mask/cleanup |
| `compose_cardpack_cover.py` | `--center-art --output --title-zh --title-en` | Fallback cover composer |
| `locate_game.py` | `--game-dir --steam-dir` | Find game install + WorkshopUploader |

### Interaction with Game
- Reads/writes CSV files in `Data/` and `Text/` directories
- Writes `ModConfig.json` and `Scripts/Entry.lua`
- Places images under `ModResource/Images/`
- Locates game directory via Steam library VDF parsing or user-provided path
- Uses `ModTemplate/Scripts/Lib/DataConfigs` for original-game CSV references
- Game mods go in `<GameDir>/Witch's Apocalyptic Journey_Data/Mods/<ModName>`

---

## Reference Documents Analysis

### 1. `references/boundaries.md` — V0 Scope

**Stable v0**:
- Create Lua-template mods from ModTemplate
- Generate/update Data/Text CSV rows (cards, Buffs, keywords, relics, blessings, items, card packs, enemy cards, enemies, events, dialogue)
- Lua `ScriptExecutor` battle effect logic
- Small keyword/tag mechanics via `Tag`, `UseScript`, Buffs
- Asset generation with path wiring and size/style assumptions marked
- Game directory location + Workshop publishing assistance
- Static validation + in-game test checklists

**Experimental v0**:
- Global hooks/listeners for keywords
- GUI automation of Workshop uploader
- Auto cropping/resizing without official dimensions
- New career skeletons
- Complex event chains/enemy behavior beyond tutorial examples

**Out of Scope**: C# DLL hooks, engine patches, battle-flow rewrites, custom UI, save changes, Steam backend, multiplayer, guaranteed official-art matching, guaranteed correctness.

**Critical wording rule**: Call output "testable drafts" or "first playable versions", never "final".

---

### 2. `references/mod-structure.md` — Directory Layout & ID Rules

**Source of truth**: `https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git`

**Local names**: `mod-tutorial` or `apocalyptic-journey-mod-tutorial`. Uses `ModTemplate` for Lua mods, `Example/Defect` as a worked example. Uses `ModTemplate/Scripts/Lib/DataConfigs` for original game CSV references.

**Standard directory layout**:
```
<ModName>/
  ModConfig.json
  Icon.png
  README.md (optional)
  Data/{Card, Buff, CardPack, Relic, Blessing, Item, EnemyCard, Enemy, EventList, Dialogue}/
  Text/{Card, Buff, CardPack, Relic, KeyWordsDic, Blessing, Item, EnemyCard, Enemy, EventList, Dialogue}/
  ModResource/Images/{Card, CardPack, Relic, Buff}/<PackName>/
  Scripts/Entry.lua (optional)
  Docs/ (optional)
```

**ModConfig required fields**: `ModName`, `ModVersion`, `ModAuthor`, `ModDescription`, `IconPath`, `Enabled`, `Dependencies` (usually null). Publishing: `WorkshopVisibility`, `PublishedFileId`.

**ID Rules — THE MOST CRITICAL CONVENTION**:
- Original game: `<CsvFileName>_<RawId>` (e.g., `buff_revelation`)
- Mod content: `<ModFolder>_<CsvFileName>_<RawId>` (e.g., `ExamplePack_examplepack_memory_echo`)
- `PackBelong` uses runtime card pack ID (e.g., `ExamplePack_cardpack_cardpack_example`)
- Rich text references MUST use composed runtime IDs inside `{...}` braces
- Bare `{id}` or `{Association}` references fail at runtime
- `*` prefix on IDs excludes content from random pools

**Entry.lua**: Uses `function ModConfig:Setup() ... end` for resource redirects, helpers, hooks, logging. Do NOT stuff card effects here.

---

### 3. `references/csv-schemas.md` — CSV Column Definitions

**Card Data** (`Data/Card/*.csv`):
```
Id,Rarity,Expend,Tag,PackBelong,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action
```

**Card Text** (`Text/Card/*.csv`):
```
Id,是否完成,Type,Note,Name,Name_en,Name_zh-Hant,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja
```

**Buff Data** (`Data/Buff/*.csv`):
```
Id,InitScript,ApplyScript,ClearScript,ReducePerTurn,ReducePerAttacked,ReducePerUse,UpperBound,Icon,Type,Rarity,Effects,SoundEffects,Action,CanZero
```

**Buff Text** (`Text/Buff/*.csv`):
```
Id,Note,Name,Name_zh-Hant,Name_en,Name_ja,Description,Description_zh-Hant,Description_ja,Description_en
```

**Keyword Text** (`Text/KeyWordsDic/*.csv`):
```
Id,Note,Description,Keywords,Keywords_zh-Hant,Keywords_en,Description_zh-Hant,Description_en,Keywords_ja,Description_ja,ShouldShow
```

**Critical rules**:
- Preserve first-row headers exactly; preserve second comment row
- Locate columns by header name, not position (columns may be reordered)
- `InitScript` must set `BaseScript` to `AttackCardItem` (targeted) or `CommonCardItem` (self/global)
- `AttackCardItem` when card selects a target; `CommonCardItem` when self/all/deck/global
- Text descriptions use `{0}`, `{1}` placeholders for dynamic values
- Use `AddDescription` in `InitScript` to register placeholder values
- Tag values for mod keywords: use composed runtime ID (e.g., `MyMod_mycsv_Contract`)
- Tags separated by comma+space (e.g., `Combo, Burnout`)
- `PackBelong` must match runtime card pack ID
- `ShouldShow` = `TRUE` for player-facing keywords
- Relic: add `PackBelong` for pack-owned relics
- CardPack Icon must be portrait cover art (300x440), not square art
- Buff Icon: 31x31 final PNG
- Relic Icon: 128x128 final PNG, square framed

**Other tables**: Blessing, Item, CardPack, EnemyCard, Enemy, EventList, Dialogue — load samples from ModTemplate first.

---

### 4. `references/lua-patterns.md` — ScriptExecutor API Patterns

**Always use Lua syntax** (even if DataConfigs examples show C#-style scripts).

**Card BaseScript patterns**:
```lua
self.Vars:set_Item("BaseScript", "AttackCardItem");  -- targeted
self.Vars:set_Item("BaseScript", "CommonCardItem");   -- self/global
```

**Common API** (all use semicolons, method-call style):

| Method | Purpose | Example |
|--------|---------|---------|
| `self:SetStatus("Target")` | Set target mode | Before damage/heal |
| `self:SetStatus("AllTarget")` | All enemies mode | AoE attacks |
| `self:SetStatus("Self")` | Self-target mode | Shield/heal/power |
| `self:Damage("7")` | Deal damage | `"7"` as string |
| `self:ChangeDefence("8")` | Gain shield | String number |
| `self:ChangeHp("3")` | Heal | String number |
| `self:ChangePower("1")` | Gain energy | String number |
| `self:DrawCount("2")` | Draw cards | String number |
| `self:AddBuff("buff_id", "1")` | Apply buff | ID string, stack string |
| `self:AddEvent("EndRound", function() ... end)` | Register event | Buff ApplyScript |

**Dynamic Description API**:
```lua
-- InitScript
self:AddDescription("1", "Damage", "7");  -- feeds {0}
self:AddDescription("2", "Buff", "2");     -- feeds {1}
-- UseScript must stay numerically aligned
```

Description types: `Damage`, `TrueDamage`, `Defence`, `Hp`, `Buff`, `Power`, `Draw`, `Money`, `Percent`, `Value`/`Special`.

**Get own Buff level**:
```lua
local buff = self.Self:GetBuff("buff_id_here");
local level = 0;
if buff ~= nil then
  level = buff.buffConfig.Level;
end
```

**Random card by rarity**:
```lua
local cards = self:GetcardsByRarity("2", "3");
if cards ~= nil and cards.Count > 0 then
  local index = math.random(0, cards.Count - 1);
  local card = cards:get_Item(index);
  if card ~= nil then
    local id = card:get_Item("Id");
    self:RandomAddCard(id);
  end
end
```

**Defensive style rules**:
- Guard nils before reading Buffs/managers/lists/dictionaries
- Convert numbers with `tostring()` for ScriptExecutor methods
- Use `dict:get_Item` / `dict:set_Item` for C# dictionaries (not `dict[key]`)
- Keep scripts short in CSV; use `Entry.lua` helpers only when shared

---

### 5. `references/mechanic-patterns.md` — Keyword & Mechanic Templates

**Simple Keyword Trigger**: Add shared Lua at end of each card's `UseScript`. Use composed runtime IDs in `Tag` and rich text. Optionally add keyword row in `Text/KeyWordsDic`.

**Buff-Gated Keyword Trigger**: Check for a Buff in `UseScript`, execute bonus effect if present, then add/change the Buff.

**Persistent Engine**: Use a Buff with `ApplyScript` event listeners (`EndRound`, `StartRound`, `Damage`). Set `UpperBound` for stacking. Make stack behavior explicit.

**Themed Card Pack** (5-10 cards):
1. Define the keyword
2. Define required Buffs
3. 3-4 common/simple cards
4. 2-3 uncommon synergy cards
5. 1-2 rare payoff cards
6. Card pack text
7. Matching Data/Text for every card

**Balance Heuristics**:
- No zero-cost cards that generate energy/draw/random cards without limits
- Cap stacking Buffs unless endless scaling is the fantasy
- Make random generation costs visible (cost, exhaust, rarity)
- Use `*id` for special cards not in random pools

---

### 6. `references/asset-style-guide.md` — Visual Style Rules

**Global visual language**:
- Pixel-art or pixel-adjacent, crisp silhouettes
- Low-to-mid color count, posterized shading
- Deep navy/near-black/dark purple backgrounds
- Clear central subject, readable at small sizes
- Strong outline separation
- Stylized shapes over painterly realism
- Motifs: runes, stamps, metal edges, cracks, ribbons, sparks, petals, clock marks, ritual geometry

**Avoid**: Photorealism, stock-art lighting, glossy 3D, cinematic gradients, airbrush, noisy over-detail, text inside art (unless asked), Full UI mockups.

**Card art**: 512x512 square, deep navy background, one centered subject, limited palette, hard-edged highlights.

**Buff icons**: 31x31, extremely simple symbol, high contrast, dark background, framed border. Red frame for debuffs; non-red for positive/neutral.

**Relic icons**: 128x128, square framed icon, centered collectible artifact, dark background, visible border.

**Card-pack covers**: 300x440 portrait, template-guided full-cover generation preferred. Integrated title art. Top/bottom wrapper bands. Inner safe area with padding from transparent edges.

**Prompt pattern template**:
```
pixel-art / pixel-adjacent <asset> illustration, <background>, <subject>,
limited palette of <colors>, crisp silhouette, <constraints>, no text/no UI frame/no photorealism
```

---

### 7. `references/assets-and-publishing.md` — Asset Sizes & Publishing Pipeline

**Hard-constrained assets**:
- Card-pack cover: 300x440, use `finalize_cardpack_cover.py`. Generate as complete cover with integrated titles.
- Buff icon: 31x31, use Buff border atlas frames. Red frame (frame 1, `81,28-111,58`) for debuffs.
- Relic icon: 128x128, square framed, centered object.

**Practical defaults**: Card art 512x512 square, Mod/workshop icon square (derived from cover).

**Buff border atlas** (`assets/buff-border-atlas.png`): 768x256, six 31x31 frames:
| Frame | Bounds | Current Use |
|-------|--------|-------------|
| 1 | (81,28)-(111,58) | Negative/debuff |
| 2-6 | Various | Positive/neutral |

**Unconstrained**: Blessing icons, keyword icons, character portraits, animation frames, enemy intent icons, dialogue/event illustrations.

**Image path conventions** (omit extensions):
```
ModResource/Images/Card/<PackName>/<card_id>
ModResource/Images/CardPack/<pack_id>
ModResource/Images/Relic/<PackName>/<relic_id>
ModResource/Images/Buff/<PackName>/<buff_id>
```

**Game directory search**:
1. User-provided path
2. `C:\Program Files (x86)\Steam\steamapps\common\Witch's Apocalyptic Journey`
3. Parse `libraryfolders.vdf` from Steam
4. Ask user

**Validation markers**: `Witch's Apocalyptic Journey.exe`, `Witch's Apocalyptic Journey_Data`, `WorkshopUploader.exe`

**Publishing checks**:
- ModConfig fields (ModName, ModVersion, ModAuthor, ModDescription, IconPath, Enabled)
- WorkshopVisibility (Private/FriendsOnly/Unlisted/Public)
- PublishedFileId: empty for first upload, preserved for updates
- Uploader at `<GameDir>/Witch's Apocalyptic Journey_Data\StreamingAssets\Mod Upload Tool\WorkshopUploader.exe`
- Select actual mod folder, not outer staging
- Success = uploader reports success OR PublishedFileId written back

---

### 8. `references/cardpack-cover-layout.md` — Cover Composition Guide

**Goal**: AI-generated 300x440 complete cover with integrated titles.

**Base template role**: `assets/cardpack-cover-base-300x440.png` as structural reference for image model. Do NOT feed annotated guides/debug masks.

**Layout zones**:
- Top: brush strip + upper title region (English title)
- Center: main illustration (replaces placeholder `?`)
- Bottom/side: dark side body, lower-left Chinese title region
- Jagged wrapper texture at top and bottom

**Title safe areas**:
- English: upper-right/upper-center, below top jagged edge, padding from transparent edge
- Chinese: lower-left/lower area, above bottom jagged edge, padding from transparent edge

**Preferred workflow**: Ask for pack name (CN/EN), theme color/palette, 2-4 signature motifs → Generate full cover with `finalize_cardpack_cover.py` → Review.

**Fallback** (`compose_cardpack_cover.py`): Only when model repeatedly fails. Script-rendered titles look less natural.

---

### 9. `references/cardpack-cover-prompt-examples.md` — Proven Prompt Patterns

**Ashen Ledger** (proven working example):
- `docs/images/ashen-ledger-cardpack-success.png`
- Complete cover, not separate center art
- Official-style layout vocabulary
- Integrated title art, not script-rendered
- Narrow palette: deep navy/purple, ember orange, parchment beige, dark red, pale lavender-white text
- Concrete motifs: ledger, feather quill, wax seals, burned paper, ritual/debt marks

**Prompt structure**:
```text
Use case: stylized-concept
Asset type: Witch's Apocalyptic Journey card-pack cover
Primary request: Generate a complete 300x440 pixel-art card-pack cover for "EN" / "CN".
Style/medium: refined pixel-art / pixel-adjacent, low-to-mid color count, crisp silhouettes...
Composition/framing: 15:22 portrait, jagged brush top strip, dark side body, large center area...
Scene/backdrop: <dark setting>, small accent lights...
Subject: <motifs>
Text: English title upper-right/upper-center; Chinese title lower-left...
Color palette: <dark bg>, <accent>, <secondary>, <highlight>, pale lavender-white title text
Constraints: safe area padding, no guide lines, no placeholder, no watermark...
```

---

## Scripts Analysis

### 1. `scripts/create_mod.py` — Mod Creator

**Purpose**: Copy official `ModTemplate` and configure `ModConfig.json` for a new mod.

**Key logic**:
- `find_template()`: searches up the directory tree for `mod-tutorial`/`apocalyptic-journey-mod-tutorial`, or does `rglob("ModTemplate")` looking for `ModConfig.json`
- Copies entire template tree via `shutil.copytree`
- Updates `ModConfig.json` with: `ModName`, `ModVersion`, `ModAuthor`, `ModDescription`, `IconPath`, `Enabled`, `Dependencies`, `WorkshopVisibility`, `PublishedFileId`
- Uses `--force` to overwrite existing output directory
- Prints target path on success

**Args**: `--template`, `--out` (required), `--name` (required), `--author`, `--version`, `--description`, `--visibility`, `--force`

### 2. `scripts/ensure_tutorial.py` — Tutorial Repo Manager

**Purpose**: Find or clone the official mod tutorial repo.

**Key logic**:
- Searches up parent directories for accepted folder names (`mod-tutorial`, `apocalyptic-journey-mod-tutorial`)
- Falls back to `rglob("ModTemplate")` and checks parent
- If not found and `--no-clone` is not set, runs `git clone --depth 1 <repo> <dest>`
- Validates clone by checking for `ModTemplate/ModConfig.json`

**Args**: `--root`, `--repo` (defaults to official), `--dest`, `--no-clone`

### 3. `scripts/validate_mod.py` — Static Mod Validator (442 lines, most complex)

**Purpose**: Run comprehensive static checks on a mod directory.

**Checks performed** (8 validation functions):
1. **`check_config`**: Validates `ModConfig.json` exists, has required fields, folder name matches `ModName`, `IconPath` exists
2. **`check_csvs`**: Validates all CSVs have `Id` header, have comment row (row 2), consistent column width, no duplicate IDs, no C# lambda syntax (`(() =>` / `=> {`)
3. **`check_cards`**: Validates all card CSVs have `InitScript` column, cards have `BaseScript`, `AddDescription` matches text placeholders (bidirectional check)
4. **`check_data_text_pairs`**: Ensures every Data ID has a matching Text ID (and vice versa) for Card/Buff/Relic/Blessing/Item/EnemyCard/Enemy
5. **`check_text_runtime_refs`**: Scans all Text Description columns for `{...}` tokens, warns if any lack `_` (bare IDs that will fail at runtime), suggests local candidates
6. **`check_pack_belong`**: Verifies `PackBelong` on cards/relics matches local card pack runtime IDs; warns on empty relic `PackBelong`
7. **`check_images`**: Resolves all image paths from Icon/BackIcon/Image/Avatar/etc. fields and warns on missing files
8. **`check_card_pack_covers`**: Checks card pack Icon is portrait (height > width, aspect ratio 0.60-0.72) and is not square/landscape
9. **`check_buff_icons`**: Checks Buff Icons are 31×31
10. **`check_relic_icons`**: Checks Relic Icons are 128×128

**Image resolution function** (`resolve_mod_image_path`): Handles `Mods/<ModName>/...` and `ModResource/...` prefixed paths; tries `.png`, `.jpg`, `.jpeg` extensions.

**Image size detection**: Reads PNG header bytes (16-24) for dimensions; parses JPEG SOF markers for dimensions.

**Runtime ID assembly**: `f"{mod_name}_{csv_path.stem}_{raw_id.lstrip('*')}"`

### 4. `scripts/finalize_cardpack_cover.py` — Cover Finalizer

**Purpose**: Post-process AI-generated card-pack covers to final 300×440 format.

**Key logic**:
- Supports 3 resize modes: `stretch` (default — preserves titles), `crop` (center crop), `contain` (fit with padding)
- Applies silhouette alpha mask from `assets/cardpack-cover-silhouette-300x440.png`
- `strip_green_edges()`: Removes green/chroma-key fringe (pixels with `g > 90`, `g > r*1.35`, `g > b*1.25`, and `a < 220`)
- Optional `--overlay-frame`: composits `cardpack-cover-frame-300x440.png` as overlay (off by default — avoids unwanted extra border)
- Clears transparent pixel RGB to `(0,0,0,0)`

**Args**: `<input>` `<output>`, `--overlay-frame`, `--resize-mode` (stretch/crop/contain)

### 5. `scripts/compose_cardpack_cover.py` — Fallback Cover Composer

**Purpose**: Fallback when AI cannot generate a complete cover with usable title placement.

**Key logic**:
- Loads template from `assets/cardpack-cover-base-300x440.png`
- `recolor_template()`: Shifts template colors toward theme color via HSL transformation (default strength 0.72)
- `cover_crop()`: Center-crops and resizes center art to fit the cover's center box `(34,82)-(266,332)`
- `template_overlay()`: Clears alpha in center area, then composites center art underneath
- `draw_titles()`: Renders script-based titles using system fonts:
  - CJK font search: `msyh.ttc`, `msyhbd.ttc`, `simhei.ttf`, `simsun.ttc` (Windows); fallback to default
  - English title: upper-right, white text with dark shadow, rounded background rectangle
  - Chinese title: lower-left, large text (wraps at 5 characters max), white with dark shadow
  - `fit_font()`: Shrinks font to fit within max width bounds
- Applies silhouette mask + green edge stripping (same as `finalize_cardpack_cover.py`)

**Args**: `<center_art>` `<output>`, `--title-zh`, `--title-en`, `--theme-color` (default `#6db6d6`), `--font`, `--template`, `--template-recolor-strength` (default 0.72)

### 6. `scripts/locate_game.py` — Game Locator

**Purpose**: Find the game install directory and WorkshopUploader.

**Key logic**:
- Parses `libraryfolders.vdf` with regex for Steam library paths
- Checks for game directory markers (exe, data dir, uploader)
- Outputs JSON with `found` (valid installations) and `checked` (all candidates) arrays
- Each result has: `path`, `exists`, `exe`, `exe_exists`, `data_dir_exists`, `uploader`, `uploader_exists`

**Args**: `--game-dir`, `--steam-dir` (default `C:\Program Files (x86)\Steam`)

---

## Asset Pipeline

### Card-Pack Cover Generation (preferred path)
1. User provides: theme, Chinese name, English name, palette, 2-4 signature motifs
2. AI uses `assets/cardpack-cover-base-300x440.png` as reference image + detailed prompt
3. Image model generates complete 300×440 (or 15:22) cover with integrated titles
4. `scripts/finalize_cardpack_cover.py` processes: resize (default stretch), silhouette mask, transparent cleanup, green-edge stripping
5. Review: correct text, no green/debug edges, safe area padding, center art fills space

### Card-Pack Cover Generation (fallback path)
1. Generate center art (text-free, square or portrait, main motif)
2. `scripts/compose_cardpack_cover.py` composits center art into template, applies HSL recoloring, renders script-based titles

### Buff Icon Pipeline
1. Generate larger concept art (intermediate only)
2. Crop/simplify/downscale to 31×31
3. Composite into chosen Buff frame from `buff-border-atlas.png`

### Relic Icon Pipeline
1. Generate larger concept art (intermediate only)
2. Downscale/composite to 128×128 square framed icon

### Image Path Wiring
- CSV paths omit extensions (game tries `.png`, `.jpg`, `.jpeg`)
- Saved under role-specific paths under `ModResource/Images/`

### Asset Assets in Repo
| File | Size | Purpose |
|------|------|---------|
| `buff-border-atlas.png` | 768×256 | 6 Buff frames (31×31 each) |
| `cardpack-cover-base-300x440.png` | 300×440 | Cover template/structural reference |
| `cardpack-cover-frame-300x440.png` | 300×440 | Clean outer outline (optional overlay) |
| `cardpack-cover-silhouette-300x440.png` | 300×440 | Precise outer transparency mask |

---

## Key Takeaways for AI Modding

### Documented Patterns
1. **CSV schemas** for all major table types (Card, Buff, Keyword, Relic, Blessing, Item, CardPack, Enemy, Event)
2. **Lua ScriptExecutor API** with 15+ documented API methods and patterns
3. **Dynamic description** system via `AddDescription` + placeholder mapping
4. **Keyword mechanics**: simple trigger, Buff-gated trigger, persistent engine via Buff events
5. **Themed card pack** composition: 3-4 common, 2-3 uncommon, 1-2 rare
6. **Balance heuristics**: cost limits, Buff caps, random generation visibility
7. **ID conventions**: strict `<ModFolder>_<CsvFileName>_<RawId>` pattern for mod content

### Templates
- **Mod directory structure** template (from ModTemplate + SKILL.md)
- **ModConfig.json** with all required + optional fields
- **Prompt templates** for card art, Buff icons, relic icons, card-pack covers
- **Card-pack cover prompt** with proven Ashen Ledger pattern
- **Lua snippets** for: targeted attack, AoE, shield, heal, energy, draw, Buff apply, Buff gate, random card, events, dynamic description

### Validation Checks (in `validate_mod.py`)
| Check | What It Validates |
|-------|------------------|
| Config | ModConfig.json exists, required fields, folder-name match |
| CSV | Id header, comment row, column width, duplicate IDs, no C# lambdas |
| Cards | BaseScript presence, AddDescription ↔ placeholders bidirectional |
| Data/Text pairs | Every Data row has matching Text row and vice versa |
| Runtime refs | Text `{...}` references use composed IDs (not bare) |
| PackBelong | Cards/relics reference valid card pack IDs |
| Images | All image paths resolve to existing files |
| CardPack covers | Portrait aspect ratio (height > width, ratio 0.60-0.72) |
| Buff icons | Exactly 31×31 |
| Relic icons | Exactly 128×128 |

### Best Practices
1. **Always use composed runtime IDs** for mod content; never bare IDs
2. **Use `AddDescription`** for dynamic numbers; keep `InitScript` and `UseScript` aligned
3. **`AttackCardItem`** for targeted cards, **`CommonCardItem`** for self/all/deck/global
4. **Buff for persistent effects**, not card scripts storing long-lived state
5. **Prefab per-card scripts** over global hooks in v0
6. **Keep text in card descriptions** as placeholders, not hard-coded numbers
7. **Always run `validate_mod.py`** before testing
8. **Card-pack covers**: generate complete covers with integrated titles, finalize with script, never use square card art
9. **Buff icons**: 31×31 with correct frame (red for debuff)
10. **Relic icons**: 128×128 square framed, never raw 256×256 generated art
11. **Preserve CSV headers**, keep comment rows, append instead of rewriting
12. **Release hygiene**: no source drafts, temp atlases, unneeded DLLs in release folder
13. **Call output "testable drafts"**, never "final"
14. **Look up original game IDs** from `ModTemplate/Scripts/Lib/DataConfigs` before referencing
15. **`*id` prefix** for content excluded from random pools
