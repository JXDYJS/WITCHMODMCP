# SkillCGExp — Skill CG Expansion Mod Analysis

## Mod Overview

**Author**: Aura | **Version**: 0.1.0 | **Workshop ID**: 3746994167

Adds skill CG (cut-in) pictures for characters "Amelia" (阿米莉娅) and "Adela" (阿黛拉), showing them when skills are used. Includes a standalone CG manager system with configurable display rules. The CG system code and logic are open-sourced for reference.

## Directory Structure

```
SkillCGExp/
  ModConfig.json              — Mod metadata
  SkillCGConfig.json          — CG display rule configuration
  .workshop-id / .workshop-sync.json
  Icon.png
  CG_阿米莉娅.png              — Amelia skill CG image
  CG_阿黛拉.png               — Adela skill CG image
  Scripts/
    Entry.dll                 — C# DLL mod entry (CG manager)
```

## Data Config Format

**ModConfig.json**:
```json
{
  "ModName": "SkillCGExp",
  "ModVersion": "0.1.0",
  "ModAuthor": "Aura",
  "ModDescription": "增加角色阿米莉娅和阿黛拉的技能CG图片...",
  "Enabled": true,
  "WorkshopTags": ["美化", "工具"],
  "PublishedFileId": "3746994167"
}
```

**SkillCGConfig.json** — CG rule configuration:
```json
{
  "enabled": true,
  "syncRemote": true,
  "maxQueueLength": 8,
  "maxRequestAgeSeconds": 6.0,
  "duplicateWindowSeconds": 0.2,
  "rules": [
    {
      "enabled": true,
      "providerId": "SkillCGExp.AmeliaSkillCG",
      "cardId": "careercard_1",
      "action": "*",
      "ownerInstanceId": "",
      "image": "CG_阿米莉娅.png",
      "priority": 10,
      "fadeIn": 0.35,
      "hold": 1.0,
      "fadeOut": 0.45
    },
    {
      "enabled": true,
      "providerId": "SkillCGExp.AdelaSkillCG",
      "cardId": "careercard_4",
      "action": "*",
      "ownerInstanceId": "",
      "image": "CG_阿黛拉.png",
      "priority": 10,
      "fadeIn": 0.35,
      "hold": 1.0,
      "fadeOut": 0.45
    }
  ]
}
```

## Key Patterns & Techniques

1. **External config file**: Uses `SkillCGConfig.json` (not `ModConfig.json`) for mod-specific settings — keeps game-critical metadata separate from mod-specific configuration
2. **Rule-based CG system**: Declarative rules mapping `cardId` + `action` → image with display parameters (fadeIn, hold, fadeOut)
3. **Queue management**: `maxQueueLength: 8`, `maxRequestAgeSeconds: 6.0` — prevents CG display flooding
4. **Deduplication**: `duplicateWindowSeconds: 0.2` — ignores rapid re-triggers
5. **Multi-owner support**: `ownerInstanceId: ""` matches all instances; could be filtered to specific characters
6. **Priority system**: `priority: 10` — allows multiple CGs to compete for display

## Extractable Lessons

1. **Secondary config pattern**: Use a separate JSON config file (not ModConfig.json) for mod-specific settings
2. **Rule-based event→display mapping**: Clean separation of trigger conditions from visual presentation
3. **Queue + timeout + dedup**: Essential for UI overlay mods to prevent overlapping/flickering
4. **Open source reference**: Including source code (open-sourced) helps other modders learn
5. **Minimal resource requirement**: Only needs PNG images + config + DLL — no CSV data tables needed for this type of mod
