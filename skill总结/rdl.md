# rdl Mod Analysis

## Mod Overview

**Author**: 夜沫 (Yè Mò)
**Version**: 1.0
**Description**: "小孩子不可以看的阿黛拉" (Adela, not for children). A simple asset replacement mod that replaces the vanilla "支配魔女" (Dominating Witch) career's visual assets — character portrait and battle animations — with custom ones for a character named "阿黛拉" (Adela).

**This is a cosmetic-only mod**. It adds no new cards, no new careers, no new mechanics. It simply redirects asset paths of an existing vanilla career.

## Directory Structure

```
rdl/
  ModConfig.json                        # Mod metadata
  Icon.png                              # Workshop icon
  .workshop-id / .workshop-sync.json    # Workshop sync
  Scripts/
    Entry.lua                           # Asset redirection logic (24 lines)
  AnimationLib/
    rdl/
      Attack/     (1 frame PNG + config.json)
      Defend/     (1 frame PNG + config.json)
      Hit/        (1 frame PNG + config.json)
      Idle/       (20 frames + config.json)
      Skill/      (1 frame PNG + config.json)
  ModResource/
    Images/
      Character/  (阿黛拉.png)
```

**No Data/, Text/, or Icon/ directories** — this is purely an asset swap mod.

## Entry Point Analysis (`Entry.lua`)

```lua
function ModConfig:Setup()
    self:RplaceCareer()
end

function ModConfig:RplaceCareer()
    local Debug = CS.UnityEngine.Debug
    Debug.Log("[Slay-Defect]资源已替换。")
    self:ModifyDataConfig("career_3", "CareerImage",  "Mods/rdl/ModResource/Images/Character/阿黛拉")
    self:RedirectSourcePath("AnimationLib/支配魔女/Idle", "Mods/rdl/AnimationLib/rdl/Idle")
    self:RedirectSourcePath("AnimationLib/支配魔女/Defend", "Mods/rdl/AnimationLib/rdl/Defend")
    self:RedirectSourcePath("AnimationLib/支配魔女/Skill", "Mods/rdl/AnimationLib/rdl/Skill")
    self:RedirectSourcePath("AnimationLib/支配魔女/Attack", "Mods/rdl/AnimationLib/rdl/Attack")
    self:RedirectSourcePath("AnimationLib/支配魔女/Hit", "Mods/rdl/AnimationLib/rdl/Hit")
end
```

### Key APIs Used

1. **`self:ModifyDataConfig("career_3", "CareerImage", path)`** — Modifies the `CareerImage` field of the vanilla career `career_3` (支配魔女/Dominating Witch) to point to the custom portrait.

2. **`self:RedirectSourcePath(originalPath, newPath)`** — Redirects asset loading from a vanilla path to a mod path. All 5 animation states (Idle, Defend, Skill, Attack, Hit) are redirected.

Also has commented-out test hooks:
```lua
--self:AddMethodHookBefore("SettingUI.OnEnable", Test)
--self:AddMethodHookBefore("GameEntryUI.OnEnable", Test)
--self:AddMethodHookBefore("EventCenter.EventTrigger", Test)
```

## Resource Management

### AnimationLib Structure

```
AnimationLib/rdl/
  Attack/   Attack_50a...png + config.json
  Defend/   Defend_50a...png + config.json
  Hit/      Hit_50a...png + config.json
  Idle/     支配魔女1-20...png + config.json (20 frames)
  Skill/    Skill_50a...png + config.json
```

**Naming Convention**: The PNGs use a hash-style naming pattern: `{State}-{Description}-CAB-{hash}-{numeric_id}.png`. This appears to be how the vanilla assets are named, suggesting these are directly modified/replaced vanilla assets rather than custom artwork.

All `config.json` files use the standard format: `AnimationPerFrame: 0.1`, `isLoop: false` (true for Idle), `Direction: "Right"`.

### Character Image

`ModResource/Images/Character/阿黛拉.png` — A single portrait image that replaces the career selection image for 支配魔女.

## Key Patterns & Techniques

### 1. **Asset Replacement via RedirectSourcePath**
```lua
self:RedirectSourcePath("AnimationLib/支配魔女/Idle", "Mods/rdl/AnimationLib/rdl/Idle")
```
This tells the game's asset loading system: "when something tries to load from `AnimationLib/支配魔女/Idle`, serve the files from `Mods/rdl/AnimationLib/rdl/Idle` instead." This is the simplest way to replace vanilla animations.

### 2. **Data Config Modification for Career Images**
```lua
self:ModifyDataConfig("career_3", "CareerImage", "Mods/rdl/ModResource/Images/Character/阿黛拉")
```
`ModifyDataConfig` changes a specific field in a specific data entry at load time. This is the standard way to patch vanilla data entries.

### 3. **Minimal Functional Mod**
This is the simplest possible functional mod pattern:
- 1 asset type (career image + animations)
- 2 API calls (ModifyDataConfig + RedirectSourcePath)
- No new content, no hooks, no data files

### 4. **Career ID Knowledge**
`"career_3"` is the internal ID for the "支配魔女" (Dominating Witch) career. This knowledge of vanilla game data IDs is essential for asset replacement mods.

### 5. **Vanilla Animation Path Knowledge**
The mod knows that the Dominating Witch's animations are stored at `AnimationLib/支配魔女/{State}`. This path convention must match the game's actual resource loading paths.

## C#/Lua Interop

- **No DLL**. Pure Lua.
- Uses `CS.UnityEngine.Debug` for logging.
- Uses `self:ModifyDataConfig()` and `self:RedirectSourcePath()` — these are `ModConfig` methods provided by the game's mod framework.

## Comparison: Asset Replacement vs Content Mods

| Aspect | rdl (Asset Swap) | Content Mods (Mokou/Muga) |
|---|---|---|
| New cards/careers | No | Yes |
| Data files | None | CSVs in Data/, Text/ |
| ModResource | Character image + animations | Full icon/image/animation sets |
| Entry.lua | Uses RedirectSourcePath + ModifyDataConfig | Uses AddMethodHook (or empty) |
| Complexity | Very low | High |
| Gameplay impact | Cosmetic only | Full gameplay changes |

## Extractable Lessons

1. **RedirectSourcePath for animations**: Use to replace any vanilla animation with custom frames. The source path must match the exact path the game uses.

2. **ModifyDataConfig for data patches**: Use to change specific fields of vanilla data entries. Field names must match the data schema (e.g., `CareerImage`, `Character`).

3. **Asset replacement mod structure**: Minimal Entry.lua + AnimationLib + Images is the complete structure. No text/data files needed.

4. **Mod logger pattern**: `CS.UnityEngine.Debug.Log("[Tag] message")` for debug output. The game's mod console may filter by tag.

5. **Commented hooks**: Developers can leave experimental hooks commented out for future use. Useful for debugging.

6. **MustSame false**: `"MustSame": false` means players don't need matching versions in multiplayer — it's cosmetic only.

7. **Character image vs career image**: `ModResource/Images/Character/` is the career selection portrait, while the `CareerImage` field in career data might point elsewhere. The mod replaces the data field to ensure consistency.

8. **AnimationLib location**: `AnimationLib/` can be at the mod root level (not inside `ModResource/`) as seen here. The mod framework supports both locations.

9. **No dependencies**: `"Dependencies": null` means the mod has no mod dependencies. This is typical for asset replacement mods.

10. **Mod size**: This is one of the smallest mods possible — just Entry.lua, config, and animation/image assets. A full content mod is ~50+ files.
