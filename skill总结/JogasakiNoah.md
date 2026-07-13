# JogasakiNoah (城崎诺亚) — Mod Analysis

**Author:** 哈基米  
**Version:** 0.1.28  
**Tags:** Content mod with custom BGM, CG, skill animations  
**Mod Type:** Lua + C# (hybrid, with per-assembly DLL bridge)

---

## Mod Overview

A character/content mod based on the game "魔法少女的魔女审判" (Magical Girl's Witch Trial). Adds the "来画画吧" (Let's Draw!) card pack, 4 relics, and the character JogasakiNoah (城崎诺亚). Gameplay revolves around bleeding, Inspiration, and Witch Form transformation. Witch Form enhances skills and unlocks custom BGM + CG cutscenes.

### Key Features

- **1 Character:** JogasakiNoah (normal + witch transformation)
- **1 Witch Form** with enhanced skills (hidden career `*jogasakinoah_witch`)
- **2 Active Skills:** Career Sketch (职业速写) and Liquid Control (控墨术) — skill-based cooldown system
- **Inspiration System:** Stacking resource buff (max 999) consumed by cards for effects
- **8 Paper Stack Wish Cards:** Story-based card set (Amelia, Nanai, Adela, Caroline, Coco, Vivian, Shell, Ermia)
- **4 Relics:** Friend Portrait, Found Pen, Paper Stack, Purple/Red Butterfly
- **Custom BGM:** `witch_bgm.mp3` plays during Witch Form combat
- **Skill CG System:** Custom overlay images during skill animations (fade-in/hold/fade-out)
- **Transform Animation:** Before/after crossfade CG when entering Witch Form
- **Cross-mod Integration:** Shared Witch Form detection across mods (`_G.WAJ_IsSharedWitchForm`)

---

## Directory Structure

```
JogasakiNoah/
  Data/ (Card, Buff, CardPack, Career, Partner, PartnerCard, Relic, RoleData, Blessing)
  ModResource/
    AnimationLib/
      JogasakiNoah/ (normal form — Idle, Attack, Defend, Hit, Skill)
      JogasakiNoah_Witch/ (witch form — Idle, Attack, Defend, Hit, Skill)
      PaperStack/ (paper stack partner — Idle only)
    Icon/Buff/, Icon/Relic/
    Images/Avatar/, Blessing/, Card/Art/, CardPack/, CareerImage/, CG/,
             Character/, Dialogue/, Icon/, Partner/, Relic/, Skill/
  Scripts/
    Entry.lua (1156 lines — main game logic)
    Entry.dll (compiled C# bridge — BGM/CG/Transform animation)
    Entry.pdb (debug symbols)
  Text/ (mirrors Data/ structure)
  tools/ (Python asset pipeline)
  WitchBGM/ (C# source code for JogasakiNoahBGM.dll)
  JogasakiNoahBGM.dll (deployed plugin)
  SkillCGConfig.json (CG animation rules)
  witch_bgm.mp3 (custom BGM)
  jogasakibridge_log.txt (runtime log)
  .workshop-id, .workshop-sync.json, Icon.png, ModConfig.json
```

---

## Entry Point Analysis

### Lua Entry (`Entry.lua`)

**ModConfig:Setup()** registers hooks via the mod framework:
```lua
function ModConfig:Setup()
    self:AddMethodHookBefore("GameEntryUI.ShowCareer", ...)
    self:AddMethodHookAfter("FightInit.Init", ...)
    self:AddMethodHookAfter("Fight_Win.ResetStates", ...)
    -- ... many UI lifecycle hooks
end
```

**Key Global Functions (called from CSV UseScript fields):**
- `_G.JogasakiNoahCareerSketch(exe)` — skill 1: RNG-based sketch check
- `_G.JogasakiNoahLiquidControl(exe)` — skill 2: bleed → inspiration conversion
- `_G.JogasakiNoahTryWitchForm(exe)` — transforms when hand painting exists
- `_G.JogasakiNoahInitCareer(exe, isWitch)` — career initialization events
- `_G.JogasakiNoahPaperStackWish(exe)` — adds 8 paper stack cards to draw pile
- `_G.JogasakiNoahFriendCoco(exe)` — card effect: convert buffs to inspiration
- `_G.JogasakiNoahObsidianAdela(exe)` — card effect: inspiration → soul conversion
- `_G.JogasakiNoahDevourNanai(exe)` — card effect: inspiration → doom power
- `_G.JogasakiNoahShowCG(self, key)` — bridge to C# CG system
- `_G.JogasakiNoahSetBGM(self, play)` — bridge to C# BGM system
- `_G.JogasakiNoahPlayTransformAnimation(self)` — bridge to C# animation system

### C# Bridge (`WitchBGMEntry.cs` → `JogasakiNoahBGM.dll`)

**ModInitialize entry point:**
```csharp
[ModInitialize]
public static void Initialize(ModConfig config) { ... }
```

**Three subsystems:**
1. `SkillCGManager` — MonoBehaviour overlay for skill card CG images
2. `TransformAnimationManager` — Before/after crossfade CG animation
3. `BGMManager` — AudioSource-based BGM playback with fade-out/restore

**Lua Registration (hacky!):**
Uses reflection to access xLua's internal `translator.assemblies` list, then calls `luaEnv.DoString` to register the C# type:
```csharp
var translator = luaEnv.GetType().GetField("translator", ...).GetValue(luaEnv);
var assemblies = translator.GetType().GetField("assemblies", ...).GetValue(translator) as IList;
assemblies.Add(asm);
luaEnv.DoString("xlua.import_type('JogasakiNoahBGM.Scripting')");
```

---

## Data Config Format

### Card CSV (`jogasakinoah.csv`)
Same standard format as other mods:
```
Id,Rarity,Expend,Tag,PackBelong,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action
```
- Character skill cards use `*` prefix (e.g., `*career_sketch`, `*hand_painting`)
- `PackBelong`: `JogasakiNoah_jogasakinoah_cardpack_art`
- Card UseScript calls `_G.JogasakiNoah*` global Lua functions
- Card art: `Mods/JogasakiNoah/ModResource/Images/Card/Art/<name>`

### Buff CSV
Two custom buffs: `inspiration` (能力, max 999, persistent) and `witch_form` (能力, max 1, transform trigger)

`witch_form` ApplyScript is massive — triggers: Inspiration gain, Power boost, Witch Trial compatibility, transform animation, BGM start, career change.

### Career CSV
Two careers: normal (`jogasakinoah`) and witch hidden (`*jogasakinoah_witch`).
- `Skill1`: `JogasakiNoah_jogasakinoah_*career_sketch` (note: runtime ID with `*` preserved — unusual!)
- `Skill2`: `JogasakiNoah_jogasakinoah_*liquid_control`
- Animations link to separate AnimationLib folders per form

---

## Text System

Standard mirrored CSV structure with `Id, Name, Name_en, Name_zh-Hant, Name_ja, Description, ...` plus 4-language localization. Keywords dictionary at `Text/KeyWordsDic/jogasakinoah.csv`.

---

## Resource Management

### Animation System (PIL-based pipeline)
Frame-by-frame PNG sequences extracted from MP4 videos using Python (`tools/key_animation_lib.py`):
1. **Magic Wand Keying:** RGB color distance threshold (tolerance 52) to remove greenscreen
2. **Connected Background Detection:** Flood-fill from borders to identify background
3. **Green Watermark Removal:** Corner region green pixel detection
4. **Alpha Hole Repair:** Internal transparent holes filled via connected-component analysis
5. **Antialiasing:** Gaussian blur (0.45px) on alpha edge

Animation config object:
```json
{"AnimationPerFrame": 0.1, "isLoop": true, "Direction": "Right"}
```

### Asset Pipeline (tools/sync_mod_assets.py)
Professional Python asset processing pipeline:
- **Portrait assets:** Bust-up cropping, alpha detection, background removal, canvas fitting
- **Card art:** Circle mask compositing on 512x512 canvas with dark blue background
- **Full-body assets:** Character/career image compositing with bottom-anchored placement
- **Relic icons:** Square asset on 512x512 with fitting
- **Skill icons:** Circular/elliptical masks at specific sizes (190x190, 85x193)
- **Animation frames:** Each role has Attack/Defend/Hit/Idle/Skill sequences, alignment-normalized via centroid calculation

### Skill Animation Resize (`tools/resize_skill_animations_to_300.py`)
Resizes skill animations to 300px height with configurable Y-shift per role.

### Witch Skill Composite (`tools/compose_witch_skill_300.py`)
Complex frame composition: extracts witch character from full scene, masks out dragon effects, composites onto resized background with precise positioning.

### BGM System
- Audio loaded via `UnityWebRequestMultimedia.GetAudioClip` (MP3 file)
- Loop disabled, volume 0.5
- Pauses/unpauses original game BGM via AudioSource reflection
- Fade-out over 3 seconds on fight end
- Scene watchers via `SceneManager.activeSceneChanged` + `EventCenter` listeners

### Skill CG System
- Canvas overlay with `sortingOrder: 100`
- JSON config: `SkillCGConfig.json` defines fade-in/hold/fade-out timing
- Images loaded from mod folder (not Resources) via `File.ReadAllBytes` + `Texture2D.LoadImage`
- Destroyed after playback to free memory

---

## Key Patterns & Techniques

1. **Hidden Witch Career:** The witch form career `*jogasakinoah_witch` is removed from game entry UI via `hide_jogasaki_witch_from_game_entry()` hook on `GameEntryUI.ShowCareer`

2. **Custom Cooldown System:** Skill cooldowns tracked in `CS.ScriptExecutor.PlayerInfo.SkillTime` dictionary with string keys, decremented in `StartRound` events

3. **Shared Witch Form Detection:** `_G.WAJ_IsSharedWitchForm` global function allows cross-mod witch form recognition (HasumiLeia + WitchTrial compatibility)

4. **Bridge Pattern:** Lua calls `_G.JogasakiNoahShowCG` → pcall → `CS.JogasakiNoahBGM.Scripting.ShowCG()` — the C# bridge is loaded dynamically via `Assembly.LoadFrom` at config time

5. **End-of-Round State Management:** Card effects use `self:AddEvent("EndRound", ...)` to restore temporary stat changes (Soul → Inspiration, DoomPower → Inspiration, MaxHP restoration)

6. **Broad UI Hooks:** 20+ method hooks cover fight init/end, game entry open/close, scene transitions, UI manager events — all used to manage BGM lifecycle

7. **Paper Stack Wish Mechanic:** Partner (Paper Stack) adds 8 specific cards to draw pile on first wish, with witch form making them free (0 cost)

8. **Resilient Error Handling:** Every C# interaction wrapped in `pcall` with bridge logging to file

---

## C#/Lua Interop

| Mechanism | Details |
|---|---|
| **DLL Loading** | `Assembly.LoadFrom` at config time + `xlua.import_type` |
| **Lua Callable** | `CS.JogasakiNoahBGM.Scripting` static methods |
| **Lua Globals** | `_G.WAJ_IsSharedWitchForm`, `_G.JogasakiNoahPaperStackWish`, etc. |
| **Project Target** | .NET Framework 4.8 (Unity compatible) |
| **Assembly Name** | `JogasakiNoahBGM` |
| **References** | `Witch.Core.dll`, `Witch.dll`, `Assembly-CSharp.dll`, Unity modules |

---

## Asset Pipeline (Python Tools)

| Tool | Purpose |
|---|---|
| `key_animation_lib.py` | Extract + greenscreen-key MP4 → PNG sequences |
| `sync_mod_assets.py` | Master asset pipeline: portraits, cards, relics, animations, BGM |
| `compose_witch_skill_300.py` | Composite witch character onto resized skill frames |
| `resize_skill_animations_to_300.py` | Uniform resize of skill animations to 300px |
| `clean_green_residue.py` | Greenscreen cleanup |
| `fix_animation_idle_holes_speed.py` | Fix alpha holes in idle animations |
| `postprocess_animation_green.py` | Green spill post-processing |
| `reduce_animation_to_24.py` | Frame rate reduction |
| `repair_animation_internal_holes.py` | Alpha hole repair |
| `reselect_four_animation_actions.py` | Action frame selection |
| `cleanup_new_repair_green.py` | Green cleanup variant |
| `preview_key_tolerances.py` | Key tolerance preview |
| `preview_key_tolerances.py` | Visual preview of key settings |

---

## Extractable Lessons

1. **Hybrid Lua+C# modding** enables complex features (BGM, CG, custom animations) beyond CSV capabilities
2. **Assembly.LoadFrom with xLua internal patching** is fragile but workable — the mod documents failures
3. **Per-fight state** should use `SkillTime` dictionary (persists across combat)
4. **CSV card scripts** can call Lua globals via `_G.FunctionName(self)` pattern
5. **Hidden careers** are implemented by hooking `GameEntryUI.ShowCareer` and removing entries
6. **Cross-mod compatibility** via global Lua functions (`_G.WAJ_IsSharedWitchForm`)
7. **Animation pipeline** can be fully automated with Python (PIL, numpy, scipy, imageio)
8. **BGM management** requires extensive lifecycle hooks — the mod registers 20+ events to ensure proper cleanup
9. **CG overlays** use separate canvas with high sorting order (100+) and are destroyed after display
10. **Resource cleanup** is manual: sprites/textures destroyed in `finally` blocks
