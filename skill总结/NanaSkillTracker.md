# NanaSkillTracker Mod Analysis

## Mod Overview

**Author**: hcxmmx
**Version**: 1.0
**Workshop Visibility**: Private
**PublishedFileId**: (empty — not published)
**Description**: A UI mod that adds a visual prediction overlay for the Nana character's "可食用厄运" (Edible Doom) skill. When hovering over an enemy, it shows how much `buff_DoomPower` (厄运/Doom Power) would be gained if the skill is used on that target, plus the estimated health gain.

**Technical Innovation**: This is the most technically sophisticated mod in the set. It uses:
- `AddMethodHookAfter` on `SkillItem.$Rougamo_CheckRayToEnemy` / `SkillItem.CheckRayToEnemy` to intercept the targeting raycast
- Runtime Unity UI creation (`Canvas`, `TextMeshProUGUI`, `Image`)
- Reflection to access private fields (`hitEnemy` via `Type:GetField`)
- `RaycastAll` validation to filter out "ghost targets"
- Blacklist management for stale/despawned targets

## Directory Structure

```
NanaSkillTracker/
  ModConfig.json                        # Mod metadata
  Icon.png                              # Workshop icon
  .workshop-id / .workshop-sync.json    # Workshop sync
  Scripts/
    Entry.lua                           # All logic (190 lines)
```

**No Data/, Text/, or ModResource/ directories** — this is a pure UI/lifecycle mod with no content definitions.

## Entry Point Analysis (`Entry.lua`)

This is a complex 190-line Entry.lua that implements a complete UI overlay system.

### Architecture

```
Entry.lua
├── initUI()           — Creates Canvas + background + TextMeshPro
├── forceHide()        — Hides the overlay
├── showUI(gainCount)  — Updates and shows the overlay with prediction data
├── clearStaleTarget() — Blacklist management
└── onCheckRay(cardItem) — Core targeting interceptor
    ├── Identity check (careercard_2)
    ├── Reflection: hitEnemy field
    ├── RaycastAll validation
    ├── Blacklist check
    ├── DoomPower calculation
    └── showUI()
```

### UI Creation (Runtime)

```lua
local canvasObj = CS.UnityEngine.GameObject("NanaOverlayCanvas")
local canvas = canvasObj:AddComponent(typeof(CS.UnityEngine.Canvas))
canvas.renderMode = CS.UnityEngine.RenderMode.ScreenSpaceOverlay
canvas.sortingOrder = 32767
```
- Creates a `Canvas` at runtime with `ScreenSpaceOverlay` and maximum sorting order
- Creates a background `Image` (dark semi-transparent: RGBA 0.05, 0.05, 0.05, 0.92)
- Creates a `TextMeshProUGUI` for text, borrowing font from any existing TMP in the scene
- `DontDestroyOnLoad` ensures the canvas persists across scene loads

### Display Content

```
[胃囊转化预测]
吞噬厄运: +X 层
最大生命: +Y
(估算)当前生命: +Z
```

Where:
- X = calculated Doom Power gain from target's debuffs
- Y = currentDoomPower + gainCount (total after application)
- Z = Y × 2 (estimated heal, hardcoded as "底层双倍回血机制")

### Targeting Interceptor

```lua
pcall(function() self:AddMethodHookAfter("SkillItem.$Rougamo_CheckRayToEnemy", onCheckRay) end)
pcall(function() self:AddMethodHookAfter("SkillItem.CheckRayToEnemy", onCheckRay) end)
```

Hooks both the obfuscated (`$Rougamo_CheckRayToEnemy`) and non-obfuscated (`CheckRayToEnemy`) method names for compatibility.

### Ghost Target Prevention

The mod implements a **three-layer defense** against stale/ghost targets:

1. **Blacklist**: When a target is unselected (`StatusManager.OnUnSelect`), it's stored as `staleTarget` and UI is hidden.
2. **RaycastAll Validation**: After getting `hitEnemy` from reflection, performs an additional `Physics.RaycastAll` against "Enemy" and "Player" layers to confirm the mouse is actually over a valid target.
3. **Blacklist Re-check**: If the reflected target matches `staleTarget`, it's treated as null regardless.

### Doom Power Calculation

```lua
for i = 0, buffs.Length - 1 do
    local buff = buffs[i]
    if buff ~= nil then
        local buffData = buff.buffConfig.dataConfig.data
        if buffData:get_Item("Type") == "负面" then  -- "Negative" type
            local rarity = tonumber(buffData:get_Item("Rarity")) or 1
            local level = buff.buffConfig.Level or 1
            local gain = math.floor((rarity * level) / 5)
            if gain < 1 then gain = 1 end
            if gain > 10 then gain = 10 end
            totalDoomPower = totalDoomPower + gain
        end
    end
end
```

- Iterates all buffs on the hovered target
- Sums contribution of all "负面" (negative) type buffs
- Formula: `floor((rarity × level) / 5)`, clamped 1–10 per buff
- Result cached in `lastDoomPower` per target

## Key Patterns & Techniques

### 1. **Runtime Unity UI Creation**
Full creation of Canvas, Image, and TextMeshPro from Lua. This is the only mod in the set that creates Unity GameObjects at runtime.

### 2. **Reflection for Private Fields**
```lua
local field = t:GetField("hitEnemy", 36)  -- 36 = private instance field
```
Uses `System.Type:GetField(name, bindingFlags)` to access private fields. Also checks `BaseType` as fallback.

### 3. **Dual Hook Targets**
```lua
pcall(function() self:AddMethodHookAfter("SkillItem.$Rougamo_CheckRayToEnemy", onCheckRay) end)
pcall(function() self:AddMethodHookAfter("SkillItem.CheckRayToEnemy", onCheckRay) end)
```
Hooks both the obfuscated (Rougamo-weaved) and original method names, ensuring compatibility regardless of build configuration.

### 4. **Mouse Position Tracking**
```lua
local mousePos = CS.UnityEngine.InputSystem.Mouse.current.position:ReadValue()
myBgObj.transform.position = CS.UnityEngine.Vector3(mousePos.x + 140, mousePos.y - 80, 0)
```
Uses the new Input System for mouse position, then positions the overlay offset from the cursor.

### 5. **Three-Layer Ghost Target Defense**
A sophisticated solution to a known game bug where targeting data persists after the target is gone:
- Layer 1: Hook `OnUnSelect` to store stale target
- Layer 2: `Physics.RaycastAll` to verify actual mouse-over
- Layer 3: Blacklist comparison

### 6. **PCall Wrapping**
Nearly all operations are wrapped in `pcall()` for safety. This is critical for a mod that touches many internal game systems that could change between versions.

### 7. **Mod as a "Plugin"**
This mod has no content Data files, no Text files, no ModResource. It's purely a behavioral/UI plugin that enhances an existing character's skill. This is a distinct mod category from the content mods.

### 8. **Font Borrowing**
```lua
local allTexts = CS.UnityEngine.Resources.FindObjectsOfTypeAll(typeof(CS.TMPro.TextMeshProUGUI))
if allTexts ~= nil and allTexts.Length > 0 then
    sampleFont = allTexts[0].font
    sampleMat = allTexts[0].fontSharedMaterial
end
```
Finds any existing TMP text in the scene to borrow font assets, ensuring the overlay matches the game's visual style.

## C#/Lua Interop

- **No DLL**. Pure Lua.
- Extensive use of `CS.UnityEngine` namespace: `GameObject`, `Canvas`, `Image`, `RectTransform`, `Resources`, `Camera`, `Physics`, `LayerMask`, `Vector2`, `Vector3`, `Object.DontDestroyOnLoad`.
- Uses `CS.TMPro.TextMeshProUGUI` and `CS.TMPro.TextAlignmentOptions`.
- Uses `CS.UnityEngine.InputSystem.Mouse` for input.
- Uses `CS.FightPlayer.Instance` for player state access.
- Uses `CS.Witch.UI.UIManager` indirectly through reflection pattern (not in this mod but referenced technique).
- Uses `System.Type:GetField(name, bindingFlags)` for reflection.

## Comparison with Other Mods

| Aspect | NanaSkillTracker | Content Mods (Mokou/Muga/BlackMage) |
|---|---|---|
| Purpose | UI enhancement | New content (cards/careers) |
| Data files | None | CSVs in Data/, Text/ |
| Resources | None (runtime UI) | PNGs in ModResource/ |
| Entry.lua complexity | 190 lines | 2–81 lines |
| Hooks used | Yes (heavy) | Minimal or none |
| Risk level | High (touches internals) | Low (standard content) |
| C# reflection | Yes | No |

## Extractable Lessons

1. **Runtime UI Toolkit**: Lua can create full Unity UI overlays. Use `CS.UnityEngine.GameObject`, add components, set transforms.

2. **Private field access**: Use `type:GetField(name, bindingFlags)` with binding flag 36 (Instance + NonPublic) for private fields. Check `BaseType` as fallback.

3. **Raycast validation**: Use `CS.UnityEngine.Physics.RaycastAll` with `LayerMask.GetMask` to verify game state when you suspect stale data.

4. **Safe hooking with pcall**: Wrap `AddMethodHookAfter` in `pcall` to gracefully handle methods that don't exist in different game versions.

5. **Ghost target defense**: Key pattern for any targeting UI mod — hook selection/deselection, validate with physics, maintain a blacklist.

6. **Font borrowing**: Use `Resources.FindObjectsOfTypeAll` to find in-scene assets for matching visual style.

7. **Input System**: Use `CS.UnityEngine.InputSystem.Mouse.current.position:ReadValue()` for mouse position in the new Input System.

8. **DontDestroyOnLoad**: Essential for UI elements that must persist across scene transitions.

9. **Mod categories**: Not all mods need Data files. UI plugins, quality-of-life improvements, and bugfix mods can be pure Lua.

10. **Hardcoded game knowledge**: The mod hardcodes `"careercard_2"` as the card ID and `"buff_DoomPower"` as the buff ID. This ties it specifically to the Nana character.
