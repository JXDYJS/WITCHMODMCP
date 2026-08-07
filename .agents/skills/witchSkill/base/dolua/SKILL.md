---
name: witch-mod-mcp-dolua
description: "WitchModMCP doLua tool: execute arbitrary Lua in the game's xLua environment and access any loaded C# type natively via the CS global (reflection-based, not codegen). Use when the user wants to run ad-hoc Lua against the live game, reflect over any C# type / any loaded mod assembly, call static / instance / private members, or use the in-game Lua Console. Triggers: doLua, lua, CS., xlua, lua console, 执行lua, lua脚本, 反射调用, CS命名空间, 访问其他mod, 读内存, wm."
---

# doLua 模块 — xLua 反射逃生舱

用 `doLua` 工具在游戏的 xLua 环境（`ScriptExecutor.luaEnv`）里执行任意 Lua，并通过 xLua 内建的 `CS` 全局**原生反射**访问**任何已加载的 C# 类型**（包括其他 Mod 的公开/内部/私有成员）。这是绕过专用 MCP 工具的通用逃生舱。

> **⚠️ 优先用专用 MCP 工具**：`doLua` 返回的是原始值、无结构化、易卡主线程。读状态、出牌、导航等场景请先用各模块的专用工具（`get_fight_state`、`get_game_data`、`inspect`…）。`doLua` 只用于专用工具覆盖不到的自定义反射需求。

---

## 1. 工具签名与返回值

| Tool | Params | Returns |
|------|--------|---------|
| `doLua` | `{luaCode}` (string, 必填) | `{success, results: []}` |

- 整段 `luaCode` 作为**一个 chunk** 在全局环境执行（`env.DoString(code, "WitchModMCP", null)`）。
- 运行在**主线程**。死循环 / 重负载会直接冻结游戏。
- 返回的 `results` 是 Lua 所有返回值按顺序转换而来：

| Lua 返回值 | 转换结果 |
|-----------|----------|
| `nil` | `null` |
| string / bool / int / long / float / double | JSON 原始值 |
| Lua table、C# 对象、LuaFunction 等 | `value.ToString()`（通常无意义，如 `XLua.LuaTable` 或对象 ToString） |

**所以：要拿结构化数据就 `return` 基础类型**，或把字段提取成基础类型再返回。返回 C# 对象/表拿不到字段。

**Python 调用示例：**
```python
r = g.call("doLua", {"luaCode": "return CS.RoleTable.Instance.San"})
print(r["results"][0])          # 数值
```

---

## 2. 原生 `CS` 访问 — 核心语法

xLua 的 `CS` 全局对**任意已加载类型**做反射解析（非 codegen），链式访问、重载解析、属性 setter、构造函数全支持。**不需要任何自定义中间层（`wm` 已废弃删除）。**

```lua
-- 读静态字段/属性
return CS.WitchModMCP.WitchModMCPEntry.MOD_TAG      -- "WitchModMCP"

-- 写静态字段/属性
CS.UnityEngine.Time.timeScale = 0.5

-- 调静态方法
CS.WitchModMCP.LuaAPI.RunTool('get_game_data', '{}')
CS.WitchModMCP.LuaAPI:RunTool('get_game_data', '{}')   -- 冒号写法等价

-- 构造实例（类表直接调用 = 构造）
local t = CS.WitchModMCP.Tools.DoLuaTool()
return t.Name                                            -- "doLua"（实例属性）

-- 通过静态单例属性进入对象，再链式访问成员
local inst = CS.GameConfigManager.Instance
return inst.DataConfigCache
```

### 2.1 ⚠️ 全局命名空间（最容易踩）

游戏里**很多核心类没有 `namespace` 声明**（全局命名空间），访问时**不要**加 `Witch.` 前缀：

| 类 | 正确写法 | 错误写法 |
|----|---------|---------|
| `RoleTable` | `CS.RoleTable` | `CS.Witch.RoleTable` ❌ |
| `Commands` | `CS.Commands` | `CS.Witch.Commands` ❌ |
| `ConsoleLogic` | `CS.ConsoleLogic` | `CS.Witch.ConsoleLogic` ❌ |

不确定命名空间时，用 `xlua.import_type('类名')` 试探（返回 `true` 或 `nil`），或先 `decompile_source` 看源码确认。

### 2.2 静态 vs 实例成员

- **静态成员**：类表上直接取 —— `CS.Type.StaticProp`、`CS.Type.StaticMethod(args)`。
- **实例成员**：类表上取到的是 `nil`。必须先有对象 —— `CS.Type(args)` 构造，或通过静态属性/单例拿对象，再 `obj.Member`。
- 判断技巧：类表上直接能取到的是静态；取到 `nil` 的就是实例，需要先 `()` 构造或拿单例。

---

## 3. 访问其他 Mod（含内部/私有成员）

### 3.1 程序集必须已同步

`CS.X` 的类型查找只认 xLua translator 的 `assemblies` 列表。**Mod DLL 是启动后经 `Assembly.LoadFrom` 加载的，默认不在该列表里** —— 不注册就访问不到（表现为 `CS.某Mod命名空间` 变成空代理表、访问成员报 `No such type`）。

桥接注册时已自动调用 `SyncLuaAssemblies()` 同步全部已加载程序集；`reload_tools` 后也会自动再同步。**如果访问不到**，手动同步：

```lua
SyncLuaAssemblies()                                   -- 同步所有已加载程序集
-- 或单个注册（程序集 short name，不是文件名）
xlua.load_assembly('FateGambler.opencode')
xlua.load_assembly('WitchModMCP.Contracts')
```

程序集名可从 `dump_mod_state` 返回的 `assemblyName` 字段拿到（如 `FateGambler.opencode`、`CatConsoleLogTerminal`、`WitchModMCP.Contracts`）。

### 3.2 公开类型 / 成员

```lua
return CS.FateGambler.Entry ~= nil        -- true（另一个 mod 的公开类型）
return CS.WitchModMCP.Tools.DoLuaTool ~= nil
```

### 3.3 内部（internal）类型

`FindType` 用 `assembly.GetType(name)`，**任意可见性类型都能解析**：

```lua
return xlua.import_type('XLua.InternalGlobals')   -- true（internal class）
```

### 3.4 私有 / internal 成员

默认反射包装只暴露 **public 成员**。要访问私有/内部成员，先对**完整类型名**开启私有访问（会连同基类链一起打开）：

```lua
xlua.private_accessible('ConsoleLogic')       -- 完整类型名，先注册再开
return CS.ConsoleLogic.pos                    -- 私有静态字段可读（实测返回 -1）
```

顺序：`xlua.import_type('完整类型名')`（确保注册并建立类表）→ `xlua.private_accessible('完整类型名')` → 访问。

---

## 4. xLua 反射的坑（全部实测踩过）

### 4.1 ⚠️ C# 数组是 0 基索引

`#as` 和 `ipairs` 对 C# 数组**直接抛 `index out of range`**（没接 `__len`，越界抛异常而非返回 nil）。必须用 `.Length` + `[i]`：

```lua
local as = CS.System.AppDomain.CurrentDomain:GetAssemblies()
for i = 0, as.Length - 1 do                  -- 0 基！不是 1..as.Length
  local a = as[i]
  if a ~= nil then
    pcall(function() xlua.load_assembly(a:GetName().Name) end)
  end
end
```

### 4.2 ⚠️ 绝对不要 `rawset(CS, '命名空间', nil)` 清缓存

CS 表会把查找结果缓存成命名空间代理；**类表只在类型首次包装时注册一次**。清掉缓存会让该类型路径**永久断裂**（`No such type` / 类表找不到），直到重启 LuaEnv。发现访问不到就先 `SyncLuaAssemblies()`，不要清缓存。

### 4.3 C# `Dictionary` 没有 `[]`

C# 集合对象用 `dict:get_Item('key')` / `dict:set_Item('key', 'value')`，Lua 原生表才用 `[]`。

### 4.4 返回值拿不到表内容

`doLua` 的 `ToJToken` 对 Lua 表 / C# 对象只做 `ToString()`。需要结构就拆成基础类型返回，或用 `LuaAPI.RunTool` 调结构化工具。

---

## 5. Lua Console（游戏内 WebSocket 控制台）

Lua Console 与 `doLua` 共用同一个 Lua 环境，语法一样。但**每行 `>>` 是独立的一次执行（独立 chunk）**：

- `local x = ...` **不跨行保留**——下一行 `x` 是 nil。
- 要跨行保留用**全局变量**（不带 `local`）或**单行写完**：

```lua
-- 单行
return CS.WitchModMCP.LuaAPI.RunTool('get_game_data','{}')

-- 或用全局
ret = CS.WitchModMCP.LuaAPI.RunTool('get_game_data','{}')
return ret
```

---

## 6. 在 Lua 里调用 MCP 工具

`WitchModMCP.LuaAPI.RunTool` 是静态方法，返回工具结果的 JSON 字符串：

```lua
local json = CS.WitchModMCP.LuaAPI.RunTool('get_game_data', '{}')
print(json)
```

注意大小写：`WitchModMCP`（MCP 全大写），写错成 `WitchModMcp` 会变成命名空间代理并报 `No such type`。

---

## 7. 最佳实践 / 危险

1. **主线程执行**：严禁死循环、长阻塞、大批量操作。工具描述明确"可能造成主线程卡顿"。
2. **优先专用工具**：能用结构化 MCP 工具就尽量不用 `doLua`。`doLua` 是无界、无校验的反射面。
3. **访问私有成员前先 `xlua.private_accessible`**；访问不到先 `SyncLuaAssemblies()`，**绝不清 CS 缓存**。
4. **全局命名空间的类不加前缀**；不确定就用 `xlua.import_type` 试探或看反编译源码。
5. **返回基础类型**才拿得到结构化结果；复杂数据用 `LuaAPI.RunTool` 调结构化工具。
6. 改动工具 DLL 后 `reload_tools` 会自动再同步程序集，无需手动 `SyncLuaAssemblies`。
