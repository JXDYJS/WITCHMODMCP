---
name: witch-mod-mcp-core
description: "WitchModMCP core tools: tool discovery, console command execution, and hot-reload. Use when the user wants to list available tools/commands, run a console command, or hot-reload the tool DLL. Triggers: list_tools, list_commands, reload_tools, eval_command, discovery, console command, 控制台命令, 命令列表."
---

# Core Module

Tool discovery, console command execution, and hot-reload for the WitchModMCP server.

## Tools

| Tool | Params | Returns | Notes |
|------|--------|---------|-------|
| `list_tools` | — | `{tools: [{name, description, inputSchema}]}` | Registry. Run this first in any session. |
| `list_commands` | — | `{commands: [{name, parameters, description?, subCommands?}], hint}` | All in-game debug console commands. Feed names into `eval_command`. |
| `reload_tools` | — | `{status, hint}` | Hot-reload tool DLL after recompiling. Follow with `list_tools`. |
| `eval_command` | `{command}` | `{command, result}` | Executes any in-game console command string. |

---

### list_tools

Returns the complete registry of all currently loaded MCP tools with their name, description, and JSON input schema. Always the first call to make.

**Python:**
```python
tools = g.list_tools()
for t in tools["tools"]:
    print(f"{t['name']}: {t['description']}")
```

### list_commands

Lists all in-game debug console commands discovered via reflection on the `Commands` class. Use this to discover valid commands before calling `eval_command`.

**Python:**
```python
cmds = g.list_commands()
for c in cmds["commands"]:
    print(f"{c['name']}: {c.get('description', '')}")
    if 'subCommands' in c:
        print(f"  sub: {', '.join(c['subCommands'])}")
```

### reload_tools

After recompiling `WitchModMCP.Contracts.dll` with modified or new tool implementations, call this to hot-reload all tools without restarting the game or pressing F5. New tools appear in subsequent `list_tools` calls.

**Python:**
```python
g.call("reload_tools")
tools = g.list_tools()
```

### eval_command

Executes an arbitrary in-game console command. The command string is fed directly to `ConsoleLogic.Input()`. Discover available commands via `list_commands`.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `command` | string | Yes | Console command string |

**Examples:**
```python
g.eval_command("help give")        # show give sub-command help
g.eval_command("give money 100")   # grant 100 gold
g.eval_command("cls")              # clear console
```

## Best practices

1. When the user mentions a console command, first run `list_commands` to verify the command exists and check its parameters.
2. After recompiling a tool DLL, always run `reload_tools` then `list_tools` to confirm the new tools are registered.
3. `eval_command` bypasses the MCP tool system entirely — it runs arbitrary game console logic. Prefer dedicated MCP tools (e.g. `give_item`, `load_scene`) when available, as they have better error handling and structured returns.
