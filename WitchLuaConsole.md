# WitchLuaConsole — 游戏内 Lua 开发环境

## 一、目标

在《魔女:终末旅途》Mod 侧提供一个**完整的 Lua 开发环境**，包含：

1. **浏览器终端**（xterm.js + WebSocket）—— 实时 Lua REPL，浏览器打开即用
2. **代码补全 + 签名提示**（sumneko Lua LSP）—— VS Code 级别的编辑体验
3. **C# 类型自动生成**（运行时反射 → EmmyLua 注解）—— 零手写类型定义，自动覆盖官方 + 所有 Mod
4. **MCP 工具封装** —— 所有 MCP tool 在 Lua 中作为原生函数可用，并带参数签名
5. **存量 MCP tool 不受影响** —— 网关和现有 tool 全量保留，LuaConsole 是独立功能模块

---

## 二、为什么选这个方案

### 核心思路

不做 LSP、不做 Lua 解析器。利用**现有生态**拼出完整开发体验：

| 组件 | 来源 | 工作量 |
|------|------|--------|
| Lua 执行引擎 | 游戏自带的 xlua (`ScriptExecutor.luaEnv`) | 0（已有 `doLua` tool） |
| 补全 + 签名 + hover + 诊断 | **sumneko lua-language-server**（开源成熟 LSP） | 0（开箱即用） |
| C# 类型信息 | 运行时反射所有 loaded assembly + DataConfigCache | ~400 行 C# 生成器 |
| 终端 UI | **xterm.js** + 浏览器 WebSocket | ~80 行 HTML + JS |
| LSP 协议通信 | VS Code 插件调 sumneko 标准 LSP | ~100 行 JS |

不需要自己写 LSP、不需要 Lua 解析器、不需要 AST——sumneko LSP 原生理解 EmmyLua `---@` 注解，我们只需要把 C# 类型信息翻译成这个格式。

### 方案对比

| | WebSocket 自建补全 | VS Code + sumneko LSP |
|---|---|---|
| 补全体验 | 基础前缀匹配 | AST 感知 + 类型推导 + 签名 + hover |
| 实现工作量 | ~500 行 | ~600 行（含 VS Code 插件）|
| 维护成本 | 自己维护 | 社区维护 sumneko |
| 使用场景 | 浏览器终端 | VS Code 编辑器 |
| **结论** | 适合快速预览 | **适合日常开发** |

---

## 三、架构总览

```
┌──────────────────────────────────────────────────────────────┐
│                    游戏进程 (Unity)                          │
│                                                              │
│  McpServer (HttpListener :3100)                              │
│    ├─ POST /           → MCP JSON-RPC (已有)                │
│    ├─ GET  /ping       → ping (已有)                        │
│    ├─ POST /heartbeat  → heartbeat (已有)                   │
│    ├─ GET  /console    → index.html (xterm.js 前端)         │
│    └─ WebSocket /ws    → Lua REPL 通道                      │
│         │                                                    │
│         └─ {cmd:"exec", code:"..."}                         │
│             → GameDispatcher.RunOnMainThread                 │
│             → ScriptExecutor.luaEnv.DoString()               │
│             → 结果格式化 → WebSocket 回传                    │
│                                                              │
│  Harmony Postfix on GameConfigManager.Init()                 │
│    → HintGenerator.Generate()                                │
│      ├─ AppDomain.GetAssemblies() → 所有 loaded 类型         │
│      ├─ DataConfigCache.Keys → CSV 配表 ID（官方+Mod）      │
│      ├─ McpRouter.GetToolNames() → MCP 工具签名             │
│      └─ 写出到工作区 .lua 文件                                │
│                                                              │
├──────────────────────────────────────────────────────────────┤
│                         工作区目录                            │
│                                                              │
│  .vscode/                                                    │
│    ├─ settings.json         ← Lua LSP 配置                   │
│    └─ WitchLuaConsole.code-workspace                         │
│                                                              │
│  witch_types/                                                │
│    ├─ _witch_types.lua       ← C# → EmmyLua 注解            │
│    ├─ _witch_mcp_tools.lua   ← MCP 工具函数签名              │
│    ├─ _witch_config.lua      ← Lua 辅助常量                  │
│    └─ _witch_ids.lua         ← DataConfigCache ID + 类型     │
│                                                              │
├──────────────────────────────────────────────────────────────┤
│                     VS Code (用户操作端)                      │
│                                                              │
│  WitchLuaConsole.vsix                                       │
│    ├─ 自动激活 sumneko Lua LSP                               │
│    ├─ 侧边栏面板（WebView → xterm.js REPL）                  │
│    ├─ 状态栏显示游戏连接状态                                  │
│    └─ 命令面板：重新生成类型提示、刷新连接等                   │
│                                                              │
│  sumneko lua-language-server (LSP 后端)                      │
│    ├─ 索引 witch_types/*.lua                                 │
│    ├─ completion（全局 + CS. 路径补全）                       │
│    ├─ signatureHelp（参数提示）                               │
│    ├─ hover（类型预览）                                      │
│    └─ diagnostics（语法/类型错误）                            │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

---

## 四、模块划分

### 模块 A：HintGenerator — C# 类型反射生成器

**文件：** `WitchModMCP/Terminal/HintGenerator.cs`

**触发时机：** `GameConfigManager.Init()` 执行完毕后，通过 Harmony Postfix 自动触发。

```csharp
[HarmonyPatch(typeof(GameConfigManager), "Init")]
public static class Patch_GameConfigManagerInit
{
    static void Postfix()
    {
        HintGenerator.Generate();
    }
}
```

**数据源与覆盖范围：**

| 数据源 | 覆盖内容 | 示例输出 |
|--------|----------|----------|
| `AppDomain.GetAssemblies().SelectMany(a => a.GetExportedTypes())` | 所有程序集的 public 类型 | `---@class Witch.RoleTable` |
| 每个类型的 public 字段/属性 | 成员类型映射 | `---@field public CurHp number` |
| 每个类型的 public 方法 | 方法签名 | `---@param targetIndex integer?` |
| `DataConfigCache.Keys` | 所有 CSV 表 ID | `Nightwatcher_career_shadow_strike` |
| `McpRouter.GetToolNames()` + 各 tool 的 `InputSchema` | MCP 工具函数 | `function get_fight_state() end` |

**类型映射表：**

| C# 类型 | EmmyLua 注解 |
|---------|-------------|
| `int`, `long`, `float`, `double` | `number` |
| `string` | `string` |
| `bool` | `boolean` |
| `List<T>`, `T[]` | `T[]` |
| `Dictionary<K,V>` | `table<K,V>` |
| `void` | `nil` |
| 自定义引用类型 | `ClassName`（自引用做 `---@field`）|
| `T?` 可空值类型 | `T?` |

**CS 命名空间处理：** 使用 `---@meta CS` 声明将每个 C# 类型挂到 `CS.` 路径下，使 sumneko 能理解 `CS.RoleTable.Instance.CurHp` 的链式补全。

**写出文件：**

```
{workspace}/witch_types/
├── _witch_types.lua      ← 类型定义（~数千行，自动生成）
├── _witch_mcp_tools.lua  ← MCP 工具函数签名（~几十行）
├── _witch_ids.lua        ← DataConfigCache ID 常量（~几千行）
└── _witch_aliases.lua    ← 常用别名简化
```

### 模块 B：ConsoleWebSocket — 浏览器终端服务

**文件：** `WitchModMCP/Terminal/ConsoleWebSocket.cs`

在现有 `McpServer`（HttpListener :3100）上新增两个路由：

```
GET  /console    → 返回 index.html（xterm.js 终端页面）
WebSocket /ws    → REPL + 补全通信通道
```

**WebSocket 消息协议：**

```
客户端 → 服务端:
  {cmd:"exec", code:"return CS.RoleTable.Instance.CurHp"}
  {cmd:"eval", expr:"CS.RoleTable"}

服务端 → 客户端:
  {type:"result", data:["100"]}
  {type:"error", message:"attempt to index a nil value"}
  {type:"echo", text:">> return 1+2"}        // 输入回显
```

**多行支持：** 检测输入是否为完整 Lua 块。若不完整（缺 `end` 等），提示用户继续输入，不立即执行。

**格式器 ConsoleFormatter.cs：** 将 `object[]` 返回值格式化为人眼可读文本：
- `nil` → 灰色 `nil`
- `number` / `string` / `boolean` → 原样
- `LuaTable` → 递归展开键值对
- Error → 红色高亮 + 堆栈截断

### 模块 C：VS Code 插件（可选，重要度最高）

**目录：** `WitchLuaConsole.vsix/`

**功能：**

| 功能 | 实现 |
|------|------|
| 自动启动 sumneko LSP | 插件激活时 spawn `lua-language-server` 进程，指向工作区 |
| 类型文件自动索引 | LSP 自动读取 `witch_types/` 目录 |
| 侧边栏 WebView 终端 | VS Code Webview API 内嵌 xterm.js，WebSocket 直连游戏 |
| 命令面板 | "Witch: Generate Type Hints" → 调用 `generate_lua_hints` MCP tool |
| 状态栏 | 显示游戏连接状态、Mod 版本 |

**为什么选 VS Code 插件：**
- VS Code 是 Lua mod 开发者的首选编辑器
- sumneko LSP 本身就是 VS Code 插件生态的一部分
- 集成后开发者不需要离开编辑器就能执行和调试 Lua

### 模块 D：MCP 工具封装

所有现有 MCP tool 自动被封装为 Lua 全局函数，无需手动注册。

**自动生成的 _witch_mcp_tools.lua 示例：**

```lua
---获取当前游戏状态快照
---@return table
function get_game_data() end

---打出一张手牌
---@param cardId string
---@param targetIndex integer?
---@param choices table?
function play_card(cardId, targetIndex, choices) end

---给予玩家物品
---@param type string
---@param value string
function give_item(type, value) end
```

每个函数的签名从 `IMcpTool.InputSchema` 的 `properties` 自动推导：
- `required` 数组中的参数 → 必选参数
- 非 required → 可选参数（带 `?`）
- `description` → `---@param` 的描述文本

---

## 五、文件清单与工作量估算

### C# 端（WitchModMCP 项目）

| 文件 | 预计行数 | 说明 |
|------|----------|------|
| `Terminal/HintGenerator.cs` | ~400 | 反射扫描 + EmmyLua 生成 + 文件写出 |
| `Terminal/ConsoleWebSocket.cs` | ~250 | WebSocket 握手 + REPL 循环 + 消息分发 |
| `Terminal/ConsoleFormatter.cs` | ~80 | `object[]` → 可读文本 |
| `Terminal/LuaExecService.cs` | ~100 | Lua 执行调度（主线程 marshal + 超时保护）|
| `McpServer.cs`（修改） | +30 | 追加 `/console` 和 WebSocket 路由 |
| `Entry.cs`（修改） | +20 | 注册 Harmony patch + 初始化 |
| **小计** | **~880** | |

### 前端

| 文件 | 预计行数 | 说明 |
|------|----------|------|
| `Terminal/console.html`（嵌入资源）| ~80 | xterm.js CDN + WebSocket 连接 |
| **小计** | **~80** | |

### VS Code 插件

| 文件 | 预计行数 | 说明 |
|------|----------|------|
| `extension.ts` | ~200 | 激活、LSP、Webview 面板 |
| `package.json` | ~40 | 扩展配置、命令注册 |
| **小计** | **~240** | |

### 总计

| 部分 | 行数 |
|------|------|
| C# 端 | ~880 |
| 前端 HTML | ~80 |
| VS Code 插件 | ~240 |
| **总计** | **~1200** |

---

## 六、实施路线

### Phase 1 — 类型生成器 MVP（~2天）

**目标：** 能反射 C# 类型 → 写出 `_witch_types.lua`

- [ ] 实现 `HintGenerator`：扫 `GetExportedTypes()` → 类型、字段、属性、方法映射
- [ ] 输出 EmmyLua `---@class` / `---@field` / `---@param` / `---@return`
- [ ] CS 命名空间 `---@meta` 块生成
- [ ] DataConfigCache key 列表输出
- [ ] MCP 工具函数签名输出
- [ ] Harmony Postfix on `GameConfigManager.Init()` 触发
- [ ] 落地测试：打开生成文件，验证 sumneko 正确索引

### Phase 2 — WebSocket REPL（~1天）

**目标：** 浏览器打开 `localhost:3100/console` 即可交互

- [ ] `ConsoleWebSocket.cs`：WebSocket 握手 + 消息循环
- [ ] `McpServer.cs`：追加 `/console` 和 `/ws` 路由
- [ ] `ConsoleFormatter.cs`：结果格式化
- [ ] `console.html`：xterm.js 嵌入 + WebSocket 连接
- [ ] 多行 Lua 块检测
- [ ] 落地测试：输入 `return 1+2` → 输出 `3`

### Phase 3 — VS Code 插件（~2天）

**目标：** 开发者工作流闭环

- [ ] VS Code 插件脚手架
- [ ] sumneko LSP 集成与启动
- [ ] 侧边栏 Webview 终端面板
- [ ] 命令面板：生成类型提示、重连

### Phase 4 — 完善（持续）

- [ ] 补全引擎增强：CS. 链式成员补全
- [ ] 方法签名提示（`(` 触发）
- [ ] 输出高亮（ANSI 颜色）
- [ ] 会话管理（多人 WebSocket 连接）
- [ ] 超时保护（卡死 Lua 自动中断）

---

## 七、已知难点与解决方案

### 难点 1：GameConfigManager.Init() 能否准确 patch？

**分析：** 游戏使用 Harmony，该方法是 public static，Rougamo 对其织入了包装器。Patch 时需要确认方法名不因混淆/织入而变。

**方案：** 若 `GameConfigManager.Init` 被 Rougamo 重命名为 `$Rougamo_Init`，用 Harmony 的 `MethodType` 或直接 patch 原始方法名。备选节点：`UIManager.Show<MainMenu>` 作为降级。

### 难点 2：运行时反射在 IL2CPP 下的限制

**分析：** IL2CPP 默认裁剪未使用的类型和成员，`GetExportedTypes()` 可能返回不全。

**方案：** 项目已通过 `[UnityEngine.Scripting.Preserve]` 或 link.xml 保留必要类型。若仍有问题，可改用 `DataConfigCache` 中的 `IDataConfig.scriptExecutor` 的运行时类型作为锚点，反向扫描类型图。

### 难点 3：生成文件体积

**分析：** 2213 条 DataConfigCache key + 数千个 C# public 类型 → 生成文件可能很大。

**方案：**
- 分文件输出（类型/ID/工具分开）
- 按需生成：仅在用户调用 `generate_lua_hints` 或 Mod 数量变化时重新生成
- hash 缓存：DLL 内容未变则不重写

### 难点 4：Mod 热加载后的类型变更

**分析：** 某些 Mod 支持运行时热加载，新类型出现时需要重新生成。

**方案：** 
- `reload_tools` 调用时顺带重新生成（`McpRouter.ReloadAllTools()` 末尾加一行）
- 提供独立的 `generate_lua_hints` MCP tool 手动触发

### 难点 5：sumneko LSP 进程管理

**分析：** VS Code 插件需要启动外部进程 `lua-language-server`。

**方案：** 
- 插件通过 `vscode.extensions.getExtension('sumneko.lua')` 检查是否已安装
- 若未安装，引导用户从市场安装（无需自己打包 sumneko）
- 工作区配置 `.vscode/settings.json` 设定 `Lua.workspace.library` 指向 `witch_types/` 目录

### 难点 6：Threading — WebSocket 线程与 Unity 主线程

**分析：** `HttpListener` 运行在后台线程，Lua `DoString()` 必须在主线程。

**方案：** 已有 `GameDispatcher.RunOnMainThread()` 可直接使用。WebSocket 线程收到消息 → `RunOnMainThread` 执行 Lua → `TaskCompletionSource` 等结果 → WebSocket 线程发回。

---

## 八、依赖清单

### 运行时依赖（游戏侧）

| 依赖 | 来源 | 用途 |
|------|------|------|
| `System.Net.HttpListener` | .NET Framework 内置 | HTTP + WebSocket 服务 |
| `System.Net.WebSockets` | .NET Framework 4.5+ 内置 | WebSocket 协议 |
| `Harmony` | 项目已有 | Patch `GameConfigManager.Init()` |
| `xlua` | 游戏内置 | Lua 执行引擎 |
| `Newtonsoft.Json` | 项目已有 | 序列化/反序列化 |
| `UnityEngine` | 项目已有 | GameDispatcher |

### 开发依赖（用户侧）

| 依赖 | 来源 | 用途 |
|------|------|------|
| **sumneko.lua** (LSP) | VS Code 市场 | 补全 + 签名 + hover + 诊断 |
| **浏览器**（Chrome/Edge）| 系统自带 | xterm.js 终端页面 |

### 零新增 NuGet 包

所有功能使用 .NET Framework 4.72 内置 API + 项目已有依赖实现。

---

## 九、非目标（明确不做）

| 功能 | 原因 |
|------|------|
| 自研 Lua LSP | sumneko 已成熟，不应重复造轮子 |
| AST / 类型推导引擎 | sumneko 提供，我们只提供类型注解 |
| 调试器（断点/单步）| 需要完整 Lua 调试协议，远超当前范围 |
| 内嵌游戏 UI 的终端 | 浏览器终端更方便，不占游戏画面 |
| 语法高亮 | sumneko 在 VS Code 中已提供 |
