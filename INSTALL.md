# WitchModMCP Installation Guide

> **Disclaimer**: The MCP configuration templates below are provided as reference only. AI tools update their config formats over time, and not every tool is listed here. If a template doesn't work or your tool isn't covered, search the official documentation of your AI tool for the correct MCP server configuration format and transport type.

## Project Structure

This repository contains four parts. Verify they are all present before installing:

| Component | Description | Location |
|---|---|---|
| **Game Mod (DLL source)** | C# Unity mod, injects HTTP API into game process | `WitchModMCP/`, `WitchModMCP.Contracts/`, `Harmony/`, `MCP/`, `Dispatcher/`, `Utils/` |
| **Pre-built DLLs** | Ready-to-copy binaries for the game Mods folder | `bin/Release/` or release package |
| **MCP Gateway (Python)** | MCP stdio ↔ HTTP proxy, connects AI tools to the game | `mcp_gateway/`, `run_gateway.py` |
| **Skill (AI docs)** | Knowledge base for game mechanics, tool usage, combat strategy | `.agents/skills/witchSkill/` |

### Missing files?

If any of the above directories or files are missing, re-clone:

```bash
git clone https://github.com/JXDYJS/WITCHMODMCP.git
```

---

## Step 1: Prerequisites

Check that the following are installed:

- **Python** (any 3.x) — run `python --version` to verify; install if missing
- **.NET SDK** (optional, only needed for compilation) — run `dotnet --version` to verify

### Project path

The root of the cloned repo is `<project_root>`. All paths below should use absolute paths to avoid issues across different AI tools.

---

## Step 2: Deploy the Mod DLL

### Option A: Use pre-built DLLs (recommended)

Pre-built DLLs are in `bin/Release/` or the release package:
- `WitchModMCP.dll`
- `WitchModMCP.Contracts.dll`

### Option B: Build from source

```bash
cd <project_root>
dotnet build
```

Output goes to `WitchModMCP/bin/Debug/net472/` (or similar).

### Find the game installation directory

First, ask the user for the path — the easiest way is to right-click the game in Steam → Manage → Browse local files, then paste the path.

If the user doesn't know or can't provide it, try these in order:

1. **Read `.game_path`** in the repo root (if it exists)
2. **Check Steam registry** (Windows):
   - `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 000000`
   - Or scan `HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam`
3. **Scan common paths**:
   - `C:\Program Files\Steam\steamapps\common\Witch's Apocalyptic Journey`
   - `C:\Program Files (x86)\Steam\steamapps\common\Witch's Apocalyptic Journey`
   - `D:\Steam\steamapps\common\Witch's Apocalyptic Journey`

Verify the path by checking for a `*_Data/Managed/` subdirectory.

### Deploy the mod

Game mods directory: `<game_root>\*_Data\Mods\`

Copy the entire `WitchModMCP/` folder:

```bash
# Windows
xcopy /E /I "<project_root>\WitchModMCP" "<game_root>\*_Data\Mods\WitchModMCP"

# macOS / Linux
cp -r "<project_root>/WitchModMCP" "<game_root>/*_Data/Mods/"
```

Expected structure after deployment:
```
Mods/WitchModMCP/
├── Scripts/
│   ├── Entry.dll                 ← Main mod entry
│   └── WitchModMCP.Contracts.dll ← Contracts assembly
├── mcp_plugins/
│   └── decompile/publish/
│       ├── Decompile.dll
│       ├── ICSharpCode.Decompiler.dll
│       └── ...
├── ModConfig.json                ← MCP port config
├── Data/
├── Text/
├── ModResource/
└── mcp_skills/
```

---

## Step 3: Deploy Skills & Agent Instructions

Skills and agent instructions need to be in the **user's actual project directory** (where they develop their mod), so the AI tool can find them regardless of whether it supports skill loading natively.

Determine the user's project directory (`<user_project>`). This is the root of the mod they are working on, not the WitchModMCP clone.

### Copy skills

```bash
# Create the skills directory in the user's project
mkdir -p "<user_project>/.agents/skills/"

# Copy skills from the WitchModMCP clone
cp -r "<project_root>/.agents/skills/witchSkill" "<user_project>/.agents/skills/"
```

### Copy agent instructions

```bash
# AGENTS.md — supported by most coding agents (opencode, Cursor, Codex, Claude Code, VS Code, etc.)
cp "<project_root>/AGENTS.md" "<user_project>/AGENTS.md"

# INSTRUCTIONS.md — used by Codex CLI
cp "<project_root>/INSTRUCTIONS.md" "<user_project>/INSTRUCTIONS.md"
```

If the user's project already has an `AGENTS.md`, append the WitchModMCP instructions to it instead of overwriting.

> **Note for global install**: If the AI tool supports global skill directories, also copy skills there:
> - opencode: `~/.config/opencode/agents/skills/`
> - Claude Code: `~/.claude/skills/`

---

## Step 4: Configure the MCP Server

The MCP gateway is auto-launched by the AI tool. You need to determine which AI tool the user is running and write the correct config.

The gateway script resolves its own paths via `__file__`, so **use an absolute path** to `<project_root>/run_gateway.py` in all configs.

### Ask the user

Before writing config, **ask the user: do you want a project-level or global install?**

- Project-level: Config file inside the project directory
- Global: Config file in the user's home or app data directory

### Config templates by tool

**opencode**
File: `<project_root>/opencode.json` (project) or `~/.config/opencode/opencode.json` (global)
```json
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "witchmod": {
      "type": "local",
      "command": ["python", "<abs_path>/run_gateway.py"],
      "cwd": "<project_root>",
      "timeout": 30000,
      "enabled": true
    }
  }
}
```

**Claude Desktop**
File: `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) or `%APPDATA%\Claude\claude_desktop_config.json` (Windows)
```json
{
  "mcpServers": {
    "witchmod": {
      "command": "python",
      "args": ["<abs_path>/run_gateway.py"]
    }
  }
}
```

**Claude Code**

Three scope options:
- Local (default, `~/.claude.json`): `claude mcp add --transport stdio witchmod -- python <abs_path>/run_gateway.py`
- Project (`.mcp.json`): `claude mcp add --transport stdio --scope project witchmod -- python <abs_path>/run_gateway.py`
- User (`~/.claude.json`, cross-project): `claude mcp add --transport stdio --scope user witchmod -- python <abs_path>/run_gateway.py`

Or manually write `.mcp.json`:
```json
{
  "mcpServers": {
    "witchmod": {
      "command": "python",
      "args": ["<abs_path>/run_gateway.py"],
      "env": {}
    }
  }
}
```

**VS Code (GitHub Copilot)**
File: `.vscode/mcp.json` (project) or global settings
```json
{
  "servers": {
    "witchmod": {
      "type": "stdio",
      "command": "python",
      "args": ["<abs_path>/run_gateway.py"]
    }
  }
}
```

**Cursor**
File: `.cursor/mcp.json` (project) or Cursor Settings → MCP
```json
{
  "mcpServers": {
    "witchmod": {
      "command": "python",
      "args": ["<abs_path>/run_gateway.py"]
    }
  }
}
```

**Windsurf**
File: `~/.codeium/windsurf/mcp_config.json`
```json
{
  "mcpServers": {
    "witchmod": {
      "command": "python",
      "args": ["<abs_path>/run_gateway.py"]
    }
  }
}
```

**Codex CLI**
File: `~/.codex/config.toml`
```toml
[mcp_servers.witchmod]
command = "python"
args = ["<abs_path>/run_gateway.py"]
```

---

## Step 5: Verify

1. **Launch the game** — ask the user to start the game with the WitchModMCP mod loaded
2. **Check connectivity** — call `get_scene_state` or `ping` to test
3. **If gateway isn't running** — the AI tool will auto-launch the gateway on first MCP tool call; wait a few seconds and retry
4. **Check logs** — if it fails, verify the game is running, the mod is enabled, and port `3100` isn't in use

---

## Step 6: Clean up the project folder (optional)

The cloned project folder does not need to be kept. Before deleting, make sure:

- No one is actively using the Python gateway or DLLs
- You've copied anything you want to keep

If you keep the project folder, the MCP config's absolute paths already point to it.

**If you want to delete the project folder**, first copy the MCP gateway to a permanent location:

```bash
cp -r "<project_root>/mcp_gateway" "<target_path>/"
cp "<project_root>/run_gateway.py" "<target_path>/"
```

Then update all MCP config paths to point to the new location.

---

## Troubleshooting

| Problem | Solution |
|---|---|
| `Game mod is not reachable` | Game not running or mod not loaded. Start the game first. |
| Gateway fails to start | Check Python is installed (`python --version`). |
| Port conflict | Default: 3100. Override via `MCP_MOD_PORT` env var. |
| Mod not showing up | Check the Mods directory path and that the DLLs are in the right place. |
