# WitchModMCP Installation Guide

## Project Structure

This repository contains four parts. Verify they are all present before installing:

| Component | Description | Location |
|---|---|---|
| **Game Mod (DLL source)** | C# Unity mod, injects HTTP API into game process | `WitchModMCP/`, `WitchModMCP.Contracts/`, `Harmony/`, `MCP/`, `Dispatcher/`, `Utils/` |
| **Pre-built DLLs** | Ready-to-copy binaries for the game Mods folder | `bin/Release/` or release package |
| **MCP Gateway (Python)** | MCP stdio ↔ HTTP proxy, connects AI tools to the game | `mcp_gateway/`, `run_gateway.py`, `witch_mcp.py` |
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

The root of the cloned repo is `<project_root>`. All subsequent paths are relative to this.

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

Copy the entire `WitchModMCP/` folder (includes scripts, decompile plugin, config, data, etc.):

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
│   ├── Entry.dll                 ← Main mod entry point
│   └── WitchModMCP.Contracts.dll ← Contracts assembly
├── mcp_plugins/
│   └── decompile/publish/
│       ├── Decompile.dll
│       ├── ICSharpCode.Decompiler.dll
│       └── ...
├── ModConfig.json                ← MCP port config, etc.
├── Data/
├── Text/
├── ModResource/
└── mcp_skills/
```

---

## Step 3: Deploy Skills (AI Documentation)

Skills teach the AI about game mechanics, tool usage, and combat strategy. They need to be placed where the AI tool can find them.

Skills are located at `<project_root>/.agents/skills/witchSkill/`:

- `base/` — Basic tool usage (combat, deck, lobby, gameflow, etc.)
- `devtools/` — Developer debugging tools
- `gameplay/` — Normal gameplay guide
- `insights/` — Game mechanics and data structures
- `patterns/` — Development pattern reference
- `deployment/` — Build and deploy guides

**Project-level install**: Skills stay in the repo, no extra step needed. The AI finds them via the reference in `AGENTS.md`.

**Global install**: Copy skills to the AI tool's global skill directory so they're available to all projects.

```bash
# opencode global
cp -r "<project_root>/.agents/skills/witchSkill" "~/.config/opencode/agents/skills/"

# Claude Code global
cp -r "<project_root>/.agents/skills/witchSkill" "~/.claude/skills/"
```

---

## Step 4: Configure the MCP Server

The MCP gateway is auto-launched by the AI tool via its config file. You need to determine which AI tool the user is running and where its config lives.

### Identify the AI tool

| Tool | Config scope | Details |
|---|---|---|
| opencode | Project or global | Project: `<project_root>/opencode.json`; Global: `~/.config/opencode/opencode.json` |
| Claude Desktop | Global | Windows: `%APPDATA%\Claude\claude_desktop_config.json`; macOS: `~/Library/Application Support/Claude/claude_desktop_config.json` |
| Claude Code | Global | `~/.claude/settings.json` |
| Cursor | Project or global | Project: `<project_root>/.cursor/mcp.json`; Global: Cursor Settings → MCP |
| Windsurf | Project | `<project_root>/.windsurf/mcp_config.json` |
| Codex CLI | Global | `~/.codex/config.toml` |
| VS Code (GitHub Copilot) | Project or global | Project: `.vscode/mcp.json`; Global: `settings.json` → `github.copilot.chat.mcp.servers` |

### Ask the user

Before writing config, **ask the user: do you want a project-level or global install?**

- Project-level: Config file inside the project directory, scoped to this project
- Global: Config file in the user's home or app data directory, available everywhere

### Write the config

Use the appropriate template below:

**opencode (project-level `opencode.json`):**
```json
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "witchmod": {
      "type": "local",
      "command": ["python", "run_gateway.py"],
      "cwd": "<project_root>",
      "timeout": 30000,
      "enabled": true
    }
  }
}
```

**Claude Desktop (global `claude_desktop_config.json`):**
```json
{
  "mcpServers": {
    "witchmod": {
      "command": "python",
      "args": ["run_gateway.py"],
      "cwd": "<project_root>"
    }
  }
}
```

**Claude Code (global `~/.claude/settings.json`):**
```json
{
  "mcpServers": {
    "witchmod": {
      "command": "python",
      "args": ["run_gateway.py"],
      "cwd": "<project_root>"
    }
  }
}
```

**Cursor (project-level `.cursor/mcp.json`):**
```json
{
  "mcpServers": {
    "witchmod": {
      "command": "python",
      "args": ["run_gateway.py"],
      "cwd": "<project_root>"
    }
  }
}
```

**Windsurf (project-level `.windsurf/mcp_config.json`):**
```json
{
  "mcpServers": {
    "witchmod": {
      "command": "python",
      "args": ["run_gateway.py"],
      "cwd": "<project_root>"
    }
  }
}
```

**Codex CLI (global `~/.codex/config.toml`):**
```toml
[mcp_servers.witchmod]
command = "python"
args = ["run_gateway.py"]
cwd = "<project_root>"
```

> If the user chose project-level install but the tool doesn't support it, fall back to global.

---

## Step 5: Verify

1. **Launch the game** — ask the user to start the game with the WitchModMCP mod loaded
2. **Check connectivity** — call `get_scene_state` or `ping` to test
3. **If gateway isn't running** — the AI tool will auto-launch `python run_gateway.py` on the first MCP tool call; wait a few seconds and retry
4. **Check logs** — if it fails, verify the game is running, the mod is enabled, and port `3100` isn't in use

---

## Step 6: Clean up the project folder (optional)

The cloned project folder does not need to be kept. Before deleting, make sure:

- No one is actively using the Python gateway or DLLs
- You've copied anything you want to keep

Deleting source code and build artifacts is generally safe. If you keep the project folder, the MCP config's `cwd` already points to it — no extra work needed.

**If you want to delete the project folder**, first copy the MCP gateway to a permanent location and update the MCP config:

```bash
# Copy to a stable location (e.g. C:\Tools\WitchModMCP)
cp -r "<project_root>/mcp_gateway" "<target_path>/"
cp "<project_root>/run_gateway.py" "<target_path>/"
```

Then update `cwd` and `command` in the MCP config to point to the new absolute paths.

---

## Troubleshooting

| Problem | Solution |
|---|---|
| `Game mod is not reachable` | Game not running or mod not loaded. Start the game first. |
| Gateway fails to start | Check Python is installed (`python --version`). |
| Port conflict | Default: 3100. Override via `MCP_MOD_PORT` env var. |
| Mod not showing up | Check the Mods directory path and that the DLLs are in the right place. |
