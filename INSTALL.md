# WitchModMCP 安装指南

## 项目构成

本仓库包含四部分，安装前先确认它们都在：

| 组成部分 | 说明 | 位置 |
|---|---|---|
| **游戏 Mod（DLL 源码）** | C# Unity Mod，注入游戏进程提供 HTTP API | `WitchModMCP/`、`WitchModMCP.Contracts/`、`Harmony/`、`MCP/`、`Dispatcher/`、`Utils/` |
| **编译好的 DLL** | 可以直接复制到游戏 Mods 目录的二进制文件 | `bin/Release/` 或发布包中 |
| **MCP 网关（Python）** | MCP stdio ↔ HTTP 代理层，AI 工具通过它连接游戏 | `mcp_gateway/`、`run_gateway.py`、`witch_mcp.py` |
| **Skill（AI 指导文档）** | AI 理解游戏机制、工具用法、战斗策略的知识库 | `.agents/skills/witchSkill/` |

### 如果文件不完整

如果上述目录或文件缺失（比如只克隆了一部分），重新完整克隆：

```bash
git clone https://github.com/JXDYJS/WITCHMODMCP.git
```

---

## 第一步：准备工作

### 检查依赖

确认系统已安装：
- **Python**（任意版本 3.x）—— 输 `python --version` 验证，如果没有引导用户安装
- **.NET SDK**（可选，仅编译时需要）—— 输 `dotnet --version` 验证

### 确认项目路径

克隆本仓库后的根目录即为 `<项目根目录>`。后续所有路径操作以此为基础。

---

## 第二步：部署 Mod DLL

### 方案 A：使用预编译的 DLL（推荐）

预编译的 DLL 在项目根目录下的 `bin/` 或发布包中：
- `WitchModMCP.dll`
- `WitchModMCP.Contracts.dll`

### 方案 B：自行编译

```bash
cd <项目根目录>
dotnet build
```

产物在 `WitchModMCP/bin/Debug/net472/`（或类似路径）。

### 找到游戏安装目录

先问用户要游戏路径——最简单的方式是让用户在 Steam 库中右键游戏 → 管理 → 浏览本地文件，然后把路径发给你。

如果用户不知道或不方便，再依次尝试：

1. **读取仓库根目录的 `.game_path` 文件**（如果存在）
2. **查询 Steam 注册表**（Windows）：
   - `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 000000`
   - 或遍历 `HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam`
3. **扫描常见路径**：
   - `C:\Program Files\Steam\steamapps\common\Witch's Apocalyptic Journey`
   - `C:\Program Files (x86)\Steam\steamapps\common\Witch's Apocalyptic Journey`
   - `D:\Steam\steamapps\common\Witch's Apocalyptic Journey`

验证路径下存在 `*_Data/Managed/` 目录，确认是游戏根目录。

### 部署 Mod

游戏 Mod 目录位置：`<游戏根目录>\*_Data\Mods\`

把整个 `WitchModMCP/` 文件夹复制过去（里面包含编译好的脚本、反编译插件、配置文件、数据资源等）：

```bash
# Windows
xcopy /E /I "<项目根目录>\WitchModMCP" "<游戏根目录>\*_Data\Mods\WitchModMCP"

# macOS / Linux
cp -r "<项目根目录>/WitchModMCP" "<游戏根目录>/*_Data/Mods/"
```

部署后的目录结构应为：
```
Mods/WitchModMCP/
├── Scripts/
│   ├── Entry.dll                 ← 主 Mod 入口
│   └── WitchModMCP.Contracts.dll ← 契约程序集
├── mcp_plugins/
│   └── decompile/publish/
│       ├── Decompile.dll
│       ├── ICSharpCode.Decompiler.dll
│       └── ...
├── ModConfig.json                ← MCP 端口等配置
├── Data/
├── Text/
├── ModResource/
└── mcp_skills/
```

---

## 第三步：部署 Skill（AI 指导文档）

Skill 文件指导 AI 理解游戏机制、工具用法和战斗策略，需要让 AI 工具能找到它们。

Skill 位于 `<项目根目录>/.agents/skills/witchSkill/`，包含以下内容：
- `base/` — 基础工具说明（战斗、牌组、大厅、流程等）
- `devtools/` — 开发者调试工具说明
- `gameplay/` — 正常游玩指南
- `insights/` — 游戏机制与数据结构知识
- `patterns/` — 开发模式参考
- `deployment/` — 编译与部署指南

**项目级安装**：skill 已在仓库中，无需额外操作。AI 工具通过 `AGENTS.md` 中的引用找到它们。

**全局安装**：将 skill 复制到 AI 工具的全局 skill 目录，供所有项目使用。

```bash
# opencode 全局
cp -r "<项目根目录>/.agents/skills/witchSkill" "~/.config/opencode/agents/skills/"

# Claude Code 全局
cp -r "<项目根目录>/.agents/skills/witchSkill" "~/.claude/skills/"
```

---

## 第四步：配置 MCP 服务器

MCP 网关通过 AI 工具的配置文件自动启动。你需要确定当前 AI 工具的身份和配置位置。

### 识别当前 AI 工具

从上下文判断用户正在使用的 AI 工具：

| 工具 | 配置位置类型 | 说明 |
|---|---|---|
| opencode | 项目级 或 全局 | 项目级：`<项目根目录>/opencode.json`；全局：`~/.config/opencode/opencode.json` |
| Claude Desktop | 全局 | Windows: `%APPDATA%\Claude\claude_desktop_config.json`；macOS: `~/Library/Application Support/Claude/claude_desktop_config.json` |
| Claude Code | 全局 | `~/.claude/settings.json` |
| Cursor | 项目级 或 全局 | 项目级：`<项目根目录>/.cursor/mcp.json`；全局：Cursor Settings → MCP |
| Windsurf | 项目级 | `<项目根目录>/.windsurf/mcp_config.json` |
| Codex CLI | 全局 | `~/.codex/config.toml` |
| VS Code (GitHub Copilot) | 项目级 或 全局 | 项目级：`.vscode/mcp.json`；全局：settings.json 的 `github.copilot.chat.mcp.servers` |

### 询问用户偏好

在写配置前，先问用户：**想安装到项目级别（仅当前项目能用）还是全局（所有项目都能用）？**

- 项目级：配置文件写在项目目录内，随仓库分发
- 全局：配置文件写在用户目录或工具的系统级配置中

### 写入配置

不同工具的配置格式不同，遵循以下模板：

**opencode（项目级 `opencode.json`）：**
```json
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "witchmod": {
      "type": "local",
      "command": ["python", "run_gateway.py"],
      "cwd": "<项目根目录>",
      "timeout": 30000,
      "enabled": true
    }
  }
}
```

**Claude Desktop（全局 `claude_desktop_config.json`）：**
```json
{
  "mcpServers": {
    "witchmod": {
      "command": "python",
      "args": ["run_gateway.py"],
      "cwd": "<项目根目录>"
    }
  }
}
```

**Claude Code（全局 `~/.claude/settings.json`）：**
```json
{
  "mcpServers": {
    "witchmod": {
      "command": "python",
      "args": ["run_gateway.py"],
      "cwd": "<项目根目录>"
    }
  }
}
```

**Cursor（项目级 `.cursor/mcp.json`）：**
```json
{
  "mcpServers": {
    "witchmod": {
      "command": "python",
      "args": ["run_gateway.py"],
      "cwd": "<项目根目录>"
    }
  }
}
```

**Windsurf（项目级 `.windsurf/mcp_config.json`）：**
```json
{
  "mcpServers": {
    "witchmod": {
      "command": "python",
      "args": ["run_gateway.py"],
      "cwd": "<项目根目录>"
    }
  }
}
```

**Codex CLI（全局 `~/.codex/config.toml`）：**
```toml
[mcp_servers.witchmod]
command = "python"
args = ["run_gateway.py"]
cwd = "<项目根目录>"
```

> 注意：如果用户选择了项目级安装，且对应工具不支持项目级配置，则自动回退到全局配置。

---

## 第五步：验证安装

1. **启动游戏**：让用户启动游戏（确保 WitchModMCP Mod 已加载）
2. **检查连接**：执行 `get_scene_state` 或 `ping` 测试是否连通
3. **如果网关未启动**：AI 工具会在首次调用 MCP 工具时自动启动 `python run_gateway.py`，等待几秒后重试
4. **查看日志**：如果连接失败，检查游戏是否已启动、Mod 是否已启用、端口 `3100` 是否被占用

---

## 第六步：项目文件夹清理（可选）

克隆下来的项目文件夹不是必须保留的。清理前确认：

- 无人正在使用将被删除的 Python 网关或 DLL
- 已复制好需要保留的内容

一般删除源码和编译产物是安全的。如果选择保留项目文件夹，MCP 配置中的 `cwd` 已经指向正确位置，无需额外操作。

**如果要删除项目文件夹**，推荐先将 MCP 网关复制到外部稳定路径，再更新 MCP 配置：

```bash
# 把 MCP 网关复制到稳定位置（如 C:\Tools\WitchModMCP）
cp -r "<项目根目录>/mcp_gateway" "<目标路径>/"
cp "<项目根目录>/run_gateway.py" "<目标路径>/"
```

MCP 配置中的 `cwd` 需改为新路径，`command` 也须使用绝对路径指向新位置的 `run_gateway.py`。

---

## 故障排查

| 问题 | 解决 |
|---|---|
| `Game mod is not reachable` | 游戏未启动或 Mod 未加载。先启动游戏 |
| 网关启动报错 | 检查 Python 是否安装，输 `python --version` |
| 端口冲突 | 默认端口 3100，可通过环境变量 `MCP_MOD_PORT` 修改 |
| Mod 未显示在游戏中 | 检查 Mods 目录路径是否正确，DLL 是否在正确位置 |
