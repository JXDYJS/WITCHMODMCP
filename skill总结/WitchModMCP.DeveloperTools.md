# WitchModMCP.DeveloperTools — Extended Developer Tools Analysis

## Mod Overview

**WitchModMCP.DeveloperTools** extends the base WitchModMCP with advanced developer tools for mod testing and debugging. It adds capabilities for combat automation, game flow orchestration, lobby configuration, screenshot capture, raycasting, source decompilation, and RNG control.

**Key Features:**
- Extended combat tools (claim rewards, card pile manipulation)
- Game flow navigation (enter game, start run, scene jumping)
- Lobby manipulation (detailed career/partner/config)
- Screenshot capture via Unity `ScreenCapture`
- Mouse raycasting for GameObject identification
- RNG seed control for reproducible testing
- Source decompilation integration (ILSpy-based)

## Directory Structure

```
WitchModMCP.DeveloperTools/
├── ModConfig.json                       # Mod configuration
├── mcp_skills/
│   ├── SKILL.md                        # Skill documentation
│   └── skills/                         # Skill subdirectories
├── mcp_plugins/
│   └── decompile/                      # Decompilation plugin
└── Scripts/
    ├── Entry.dll                       # Main entry point
    └── WitchModMCP.DeveloperTools.dll  # Core library
```

## Entry Point Analysis

**Standard pattern:** Uses `[ModInitialize]` attribute. The mod registers its extended tool handlers with the WitchModMCP server, extending the base MCP protocol with additional endpoints.

**Plugin architecture:** Tools are organized as:
1. **In-process tools** (C# methods called directly)
2. **External plugins** (like decompile, which may shell out to external tools)

## Tool Categories

### Combat Tools
- `claim_rewards` — post-battle reward selection
- `set_card_pile` — manipulate draw/discard/hand piles
- `set_fight_entity` — modify entity stats mid-fight

### Game Flow Tools
- `enter_game` — navigate from main menu
- `start_new_game` — start with mode selection
- `start_run` — begin run from lobby
- `load_scene` — jump to specific scene/event/fight
- `check_mode_saves` / `list_game_modes`

### Diagnostics Tools
- `get_screenshot` — capture game screen
- `raycast_mouse` — identify GameObjects under cursor
- `set_rng_seed` — control RNG for reproducibility
- `decompile_source` — decompile game assemblies

### Lobby Tools
- `get_lobby_state` — full career/partner/attribute/pack readout
- `set_lobby_state` — modify any lobby parameter

## Key Techniques

1. **Game flow state machine**: Models game as a state machine (MainMenu → Lobby → Map → Fight → Rewards)
2. **Page detection**: Uses Unity scene name and active UI panels to determine current game page
3. **Screenshot pipeline**: Captures frame buffer, encodes to PNG, returns base64
4. **Raycasting**: Physics.Raycast + GraphicRaycaster for UI element detection
5. **RNG seed injection**: Sets `UnityEngine.Random.InitState()` and game-specific RNG
6. **Decompilation**: Uses ILSpy command-line to decompile game assemblies on-demand

## Extractable Lessons

### Game Flow Navigation
- Main menu scene names and how to detect them
- Loading sequence: requires tracking when async loads complete
- Lobby → Map → Fight transitions and their completion conditions

### Testing Methodology
- Seed-controlled RNG enables reproducible fight testing
- Screenshot comparison for visual regression testing
- Scene jumping bypasses normal progression for targeted testing

### Decompilation Integration
- On-demand decompilation for modders to inspect game code
- Caches decompiled output for performance
- Requires knowing the game's managed assembly paths
