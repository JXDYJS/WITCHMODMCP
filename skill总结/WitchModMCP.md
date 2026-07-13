# WitchModMCP — MCP Server Mod Analysis

## Mod Overview

**WitchModMCP** is an HTTP MCP (Model Context Protocol) server mod that exposes game state and functionality to AI assistants. It runs an embedded HTTP server inside the Unity game process, providing RESTful endpoints to query and manipulate game data.

**Key Features:**
- HTTP server on a configurable port (default 43217)
- MCP tool definition endpoint for AI assistant integration
- Game state reading (player HP, SAN, money, deck, fight state, scene)
- Console command execution
- Item/card injection
- Lobby state read/write (career, partner, attributes, card packs)
- Fight state manipulation (play card, end turn, entity stats)

## Directory Structure

```
WitchModMCP/
├── ModConfig.json              # Mod configuration
├── mcp_skills/
│   ├── config.json             # Skill configuration
│   ├── SKILL.md               # Skill documentation
│   ├── scripts/               # (future use)
│   └── skills/                # (future use)
├── Scripts/
│   ├── Entry.dll              # Main mod entry point (C#)
│   ├── WitchModMCP.dll        # Core MCP server library
│   └── WitchModMCP.Contracts.dll  # Shared contracts
├── Data/                      # Game data configs (CSV)
└── Text/                      # Localization text
```

## Entry Point Analysis

**Bootstrapping:** The mod uses the standard `[ModInitialize]` attribute pattern on a static method. When the mod is loaded, the entry point starts an embedded HTTP server (HttpListener) on a separate thread, registers all tool handlers, and begins accepting requests.

**Key API Pattern:**
```csharp
[ModInitialize]
public static void Initialize()
{
    // Start HTTP server
    // Register tool handlers
    // Begin listening
}
```

**Architecture:**
- HTTP server thread receives JSON-RPC style requests
- Request router maps method names to handler functions
- Handlers use Harmony transpilers/hooks or direct reflection to access game internals
- Responses are serialized back as JSON

## Tool Endpoints

The mod exposes tools via MCP protocol:
- `get_game_data` — player snapshot
- `get_scene_state` — current page/scene
- `eval_command` — execute console commands
- `give_item` — inject cards/items
- `get_fight_state` — battle state
- `play_card` / `end_turn` — combat actions
- `get_lobby_state` / `set_lobby_state` — career selection
- `query_config` — read game config tables
- `dump_mod_state` — mod loading state
- `get_scene_tree` — Unity scene hierarchy
- `reload_tools` — hot-reload tool DLLs
- `list_tools` / `list_commands` — discovery endpoints

## C# Implementation Patterns

1. **HttpListener hosting**: Uses `System.Net.HttpListener` for cross-platform HTTP serving
2. **JSON-RPC protocol**: Requests follow JSON-RPC 2.0 format with method, params, id
3. **Reflection-based dispatch**: Method names mapped to handlers via reflection
4. **Harmony transpilers**: Used to intercept game methods for state capture
5. **Unity API access**: Direct calls to `GameObject.Find`, `Resources.FindObjectsOfTypeAll`, singleton access
6. **File-watching for hot-reload**: Monitors DLL file changes for tool reload

## Key Techniques

1. **In-process HTTP server**: Running inside Unity's main process avoids external tool dependencies
2. **MCP protocol**: Follows Model Context Protocol standard for AI tool integration
3. **Scoped game access patterns**: Uses the game's `DataCenter`, `GameCenter`, and singleton services
4. **Safe reflection**: Type-safe access with fallback error handling
5. **Request batching**: Supports batched MCP tool requests
6. **Log streaming**: Captures Unity Debug.Log output for log tailing

## Extractable Lessons

### For MCP Modding
- The embedded HTTP server pattern enables AI-driven game control
- MCP protocol allows standardized tool discovery and invocation
- File-watch hot-reload enables rapid iteration on tool implementations

### For Game Internals
- Game singletons: `DataCenter.Inst`, `GameCenter.Inst` for data access
- Scene management: `SceneManager.GetActiveScene()` for page detection
- Fight system: `FightManager.Inst` for battle state
- Player data: `RoleDataManager.Inst` for player stats
