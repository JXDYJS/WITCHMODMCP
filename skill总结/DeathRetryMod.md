# DeathRetryMod — Utility Mod Analysis

## Mod Overview

**Author**: 榐茉 | **Version**: 1.1 | **Workshop ID**: 3759074785

A simple quality-of-life mod that intercepts player death and offers a retry dialog. When the player dies, a popup asks whether to restart the current fight or give up. On retry, the player restarts at full HP (a rare UI display bug may show 0 HP, but actual HP is correct). No configuration exposed.

## Directory Structure

```
DeathRetryMod/
  ModConfig.json              — Mod metadata
  .workshop-id                — Workshop ID: 3759074785
  .workshop-sync.json         — Sync timestamp
  Icon.png                    — Mod icon
  Scripts/
    Entry.dll                 — C# DLL mod entry (implements retry logic)
```

## Data Config Format

**ModConfig.json** (minimal):
```json
{
  "ModName": "DeathRetryMod",
  "ModVersion": "1.1",
  "ModAuthor": "榐茉",
  "ModDescription": "本模组可以在当玩家因不知原因暴毙时，弹窗询问是否重开此局战斗或放弃...",
  "IconPath": "Icon.png",
  "Enabled": true,
  "Dependencies": null,
  "MustSame": true
}
```

## Key Patterns & Techniques

- **Minimal DLL mod**: Single Entry.dll, no CSV data or resources
- **Death interception**: Hooks into the game's death flow to present a Yes/No dialog
- **Full reset on retry**: Restores full HP and restarts the battle state
- **No dependencies**: Standalone mod, `Dependencies: null`
- **No text/config files**: All UI strings are hardcoded in the DLL

## Extractable Lessons

1. **Single-purpose mod**: Does one thing and does it well — clear scope
2. **No data files**: When no configurable data is needed, a DLL-only mod is sufficient
3. **Entry point pattern**: `Scripts/Entry.dll` is the standardized entry point for C# DLL mods
4. **MustSame flag**: `MustSame: true` ensures all players in multiplayer have the same version
