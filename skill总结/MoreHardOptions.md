# MoreHardOptions (MoreHardOptions that support multiplayer) — Difficulty Mod Analysis

## Mod Overview

This mod adds additional difficulty modifier options (hard mode words/affixes) to the game, designed to work in multiplayer. Uses `.lua` scripting rather than C# DLL.

## Directory Structure

```
MoreHardOptions/
  ModConfig.json
  .workshop-id / .workshop-sync.json
  Icon.png
  Data/
    Buff/MhoBuff.csv           — Custom buff definitions
    Hard/Hard.csv              — Difficulty modifier definitions
  Text/
    Buff/MhoBuff.csv           — Localized buff text
    Hard/Hard.csv              — Localized hard mode text
  Scripts/
    Entry.lua                  — Lua script entry point
  buff图片/                    — Buff icon images (7 PNG files)
```

## Data Config Format

- Standard CSV format in `Data/Hard/` and `Data/Buff/` directories
- Uses **Lua scripting** (`Entry.lua`) instead of C# DLL
- Custom buff icons stored in `buff图片/` directory
- Multiplayer support indicated by mod name

## Key Patterns & Techniques

1. **Lua-based mod**: Uses `Entry.lua` (not Entry.dll) — demonstrates the alternative modding approach
2. **Buff + Hard mode combo**: New difficulty affixes implemented as buffs, connected via Hard data
3. **Custom icon directory**: Localized buff icon storage without ModResource/Images structure (alternative pattern)
4. **Multiplayer compatible**: Mod name explicitly advertises MP support

## Extractable Lessons

1. **Lua modding entry point**: `Scripts/Entry.lua` is the Lua equivalent of Entry.dll
2. **Alternative resource structure**: Not all mods follow the ModResource/ convention — icons can be at the mod root
3. **Hard + Buff relationship**: Difficulty modifiers can be implemented as persistent buffs applied at combat start
