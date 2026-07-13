# LogExp — Log Export Utility Mod Analysis

## Mod Overview

**Author**: Aura | **Version**: 0.1.0

Redirects console/log output to a `Logs/` folder inside the mod's own directory, preventing loss of console information when the game closes. Simple, single-purpose utility mod.

## Directory Structure

```
LogExp/
  ModConfig.json              — Mod metadata
  .workshop-id / .workshop-sync.json
  Icon.png
  Scripts/
    Entry.dll                 — C# DLL mod entry (hooks console output)
  Logs/                       — Captured log files
    Witch-YYYYMMDD-HHmmss.log — Timestamped log files (31 files present)
```

## Data Config Format

**ModConfig.json** (minimal):
```json
{
  "ModName": "LogExp",
  "ModVersion": "0.1.0",
  "ModAuthor": "Aura",
  "ModDescription": "将输出信息输出到当前mod目录下的log文件夹内，避免游戏关闭控制台信息丢失。",
  "Enabled": true,
  "Dependencies": null
}
```

## Key Patterns & Techniques

- **Console output hooking**: Intercepts Unity's `Debug.Log` or console output stream
- **Timestamped log files**: Files named `Witch-YYYYMMDD-HHmmss.log` per session
- **Persistent logging**: Logs survive game restart
- **No dependencies**: `Dependencies: null`

## Extractable Lessons

1. **Developer experience mod**: Small utility mods improve the modding DX significantly
2. **No configuration**: When behavior is straightforward, no config file is needed
3. **File naming convention**: `GameName-YYYYMMDD-HHmmss.log` for session logs
4. **Mod directory as storage**: Mods can write to their own directory for configs/logs — `Logs/` folder auto-created
5. **Single DLL approach**: No CSV/Text/Resources needed for utility mods
