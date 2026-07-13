# Console Log Terminal — Developer Tool Mod Analysis

## Mod Overview

An in-game console/log terminal viewer that allows reading game logs directly within the game. Includes supporting scripts for log viewing outside the game.

## Directory Structure

```
Console Log Terminal/
  ModConfig.json
  Configuration.json           — Mod-specific configuration
  .workshop-id / .workshop-sync.json
  icon.png
  Scripts/
    Entry.dll                  — C# DLL mod entry
  查看mod配置/                  — "View mod config" support scripts
    查看日志.bat                — Batch script for log viewing
    查看日志.ps1                — PowerShell script for log viewing
    查看日志.py                 — Python script for log viewing
    ASCII 字符编码查看器.py     — ASCII encoding viewer utility
```

## Data Config Format

**ModConfig.json**: Standard mod metadata  
**Configuration.json**: Mod-specific settings for the terminal window

## Key Patterns & Techniques

1. **Dual approach**: In-game DLL + external viewing scripts (batch, PowerShell, Python)
2. **External utility scripts**: Provides `.bat`, `.ps1`, and `.py` scripts for log reading outside the game
3. **Multiple platform support**: Windows native (bat/ps1) + cross-platform (Python)
4. **Configuration file**: Separate `Configuration.json` for user-modifiable settings

## Extractable Lessons

1. **Companion scripts pattern**: Include external utility scripts alongside the in-game mod
2. **Multi-language support scripts**: Provide the same utility in bat, ps1, and py for different user preferences
3. **Developer tooling focus**: Tools that help debug and diagnose issues are valuable additions to the modding ecosystem
