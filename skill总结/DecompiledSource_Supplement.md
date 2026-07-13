# 反编译源代码补充分析

> 来源：通过 WitchModMCP `decompile_source` 工具从游戏运行实例中获取（Witch.dll + Witch.Core.dll）
> 缓存位置：`.cache\game_src\` （661 个源文件 in Witch.dll, 113 个 in Witch.Core.dll）
> 反编译时间：2026-07-12

## 一、Mod 加载系统完整流程

### 1.1 启动流程 (`GameConfigManager.Init()`)

```
Globals.ModsPath 扫描所有子目录
  → 读取每个 ModConfig.json (JSON → ModConfig 对象)
  → 检查 LimitList 封禁名单
  → 按 ModId 去重
  → LoadModWithDependencies() 拓扑排序加载
      → 加载 Data/ 目录下的 CSV/XLSX → GameConfigData
      → 加载 Text/ 目录下的 CSV → GameConfigData
      → modConfig.Setup() 执行
          → 创建 xLua LuaTable (metatable __index = global)
          → 加载 Scripts/Entry.lua
          → 执行 Entry.lua, 调用 ModConfig.Setup(self) 或 self.Setup()
          → 加载 Scripts/Entry.dll
          → 反射扫描 [ModInitialize] 和 [ModHook] 方法
          → 调用所有 [ModInitialize] 方法
          → 注册所有 [ModHook] 方法
  → 构建关键词字典 (BuffKeyword_/CardKeyword_/EnchTag_)
  → PreCompileScripts() 预编译所有脚本列
  → 初始化 DialogueManager
```

### 1.2 ModConfig 核心字段 (`ModConfig.cs`)

```csharp
public class ModConfig {
    public string DirectoryName;      // 绝对路径
    public bool IsWorkshopMod;        // 是否为工坊mod
    public ulong WorkshopPublishedFileId;
    public bool ConfigEnabled;        // 用户配置开关
    public string ModName;            // 从 ModConfig.json 读取
    public string ModVersion;
    public string ModAuthor;
    public string ModDescription;
    public string IconPath;
    public bool Enabled;              // 是否启用
    public List<string> Dependencies; // 依赖列表
    public bool MustSame = true;      // 数据变更标记
    public string ModId => ModName + "." + ModAuthor;  // 由 ModName 和 ModAuthor 拼接
}
```

### 1.3 Lua 引导代码（C# 生成）

```lua
-- C# 通过 ScriptExecutor.luaEnv.DoString() 注入的模板代码
local __cs = self
ModConfig = setmetatable({}, {
    __index = function(_, k)
        local v = __cs[k]
        if type(v) == 'function' then
            return function(_, ...)
                return v(__cs, ...)
            end
        end
        return v
    end
})
```

Entry.lua 中的 `Setup(self)` 被调用时，`self` 就是 ModConfig C# 对象。

### 1.4 依赖解析算法

使用拓扑排序（BFS + 入度计算）：
- 构建邻接表和入度表
- 启用且无缺失依赖的 mod 入队
- 按序加载，每加载一个就减少下游的入度
- 检测循环依赖和不可解析依赖

### 1.5 数据目录加载

```csharp
// 加载 Data/ 目录
LoadResource("Mods/" + modConfig.DirectoryName + "/Data/", modConfig);
// 加载 Text/ 目录
LoadResource("Mods/" + modConfig.DirectoryName + "/Text/", modConfig);
```

`Mods/` 前缀说明 Data/ 和 Text/ 路径是相对于游戏根目录的，但 `LoadResource` 内部通过 `ResourceLoader` 处理。

## 二、Hook 系统

### 2.1 ModHookRegistry (`Witch.Core.ModHookRegistry`)

```csharp
public static class ModHookRegistry {
    // 两个静态字典: "TypeName.MethodName" → List<Action<ModHookContext>>
    private static Dictionary<string, List<Action<ModHookContext>>> Before;
    private static Dictionary<string, List<Action<ModHookContext>>> After;

    public static void AddBefore(string key, Action<ModHookContext> callback);
    public static void AddAfter(string key, Action<ModHookContext> callback);
    public static IReadOnlyList<Action<ModHookContext>> GetBefore(string key);
    public static IReadOnlyList<Action<ModHookContext>> GetAfter(string key);
    public static void Clear();  // 每次加载前清理
}
```

Key 规则：`TypeFullName.MethodName`（例如 `"FightManager.StartPlayerTurn"`）

### 2.2 ModHookContext

```csharp
public class ModHookContext {
    public object Target { get; set; }    // 被 hook 的方法的 this
    public object[] Arguments { get; set; }  // 被 hook 的方法的参数
}
```

### 2.3 C# Hook 声明方式

```csharp
[HookBefore(typeof(FightManager), "StartPlayerTurn")]
public static void MyBeforeHook(ModHookContext ctx) { ... }

[HookAfter(typeof(FightManager), "StartPlayerTurn")]
public static void MyAfterHook(ModHookContext ctx) { ... }
```

### 2.4 Lua 钩子适配

```csharp
// LuaModHookAdapter 将 LuaFunction 转为 Action<ModHookContext>
// target 存在时: array = [target, args...]
// target 不存在时: array = args
```

## 三、控制台命令系统

### 3.1 命令调度 (`ConsoleLogic.Input()`)

```csharp
// 按空格分割命令
// 反射查找 Commands.<方法名>
// 自动填充默认参数
// 返回 HTML 彩色字符串
```

### 3.2 完整命令列表

| 命令 | 功能 | 参数 |
|------|------|------|
| `help [cmd]` | 帮助文本 | 可选命令名 |
| `cls` | 清屏 | - |
| `give <id> [count]` | 给予物品 | 物品ID, 数量 |
| `copy <id> [count]` | 复制物品 | 物品ID, 数量 |
| `remove <id> [count]` | 移除物品 | 物品ID, 数量 |
| `givepack <packId>` | 发放卡包全部物品 | 卡包ID |
| `load <type> [id2]` | 加载地图/场景 | 类型, 可选ID |
| `check <id>` | 查看某ID的源数据 | ID |
| `variable set/show <key> <value>` | 调试 GameRuntimeData 变量 | action, key, value |
| `setId <arg1>` | 设置ID | - |
| `lockitem <id>` | 锁定物品 | - |
| `unlock <id>` | 解锁物品 | - |
| `unlockall` | 全部解锁 | - |
| `connect <arg1>` | 联机连接 | - |
| `dialogue <arg1> [arg2]` | 对话指令 | - |
| `eventtrigger <arg1>` | 触发事件 | - |
| `auto <on/off>` | 自动打牌 | on/off |
| `enemyall <hp/atk/all> <mult>` | 所有敌人倍率 | field, value |
| `enemytype <type> <field> <value>` | 某类型敌人倍率 | common/elite/boss |
| `enemyone <target> <field> <value>` | 单个敌人倍率 | id/name/index |
| `enemygrow <mult>` | 敌人成长倍率 | - |
| `enemybuff <target> <buff> <level>` | 给敌人加buff | - |
| `enemylayerbuff <layer> <buff> <level>` | 某层敌人加buff | - |
| `debugmoney <mult>` | 金钱倍率 | - |
| `debugmaxhp <mult>` | 最大生命倍率 | - |
| `debugheal <mult>` | 治疗倍率 | - |
| `debugdef <mult>` | 护盾倍率 | - |
| `debugcleardef <on/off>` | 回合结束清盾 | - |
| `debugreset` | 重置所有调试调整 | - |

### 3.3 实现细节

- Commands 是静态类，所有方法都是 `public static`
- 参数都是 `string` 类型，内部解析
- 用 `[HelpText("...")]` 属性提供命令描述
- `help` 命令通过反射遍历所有 `Commands` 方法

## 四、DataConfig 系统

### 4.1 核心类 (`DataConfig.cs`)

```csharp
public class DataConfig : ICloneable, IDataConfig {
    public DataType Type { get; set; }           // Card, Buff, Relic, etc.
    public IDictionary<string, string> data;      // 只读数据字典 (ReadOnlyDictionary)
    public IDictionary<string, string> Vars;      // 运行时变量字典
    public bool IsNative;                          // 是否为游戏原生 ID
    public string InstanceID;                      // 运行时实例 GUID
    public IScriptExecutor scriptExecutor;          // Lua 脚本执行器
    public bool isCompiling;                        // 正在编译脚本的标志
}
```

### 4.2 默认 Vars

构造时自动注册的默认变量：
- `DesVal1` ~ `DesVal4` = `""` (描述值，用于 `{0}` `{1}` 占位符)
- `ThisCount` = `"0"`
- `layersExperienced` = `"0"`
- `InstanceID` = GUID
- `Id` = 从 data 中获取

### 4.3 Script 编译

```csharp
// PreCompileScripts() 遍历所有 data 列
// 列名包含 "Script" 的列会被预编译
// 使用 ScriptExecutor.PreCompileScripts(columnName)
```

### 4.4 序列化

- 内存中使用 `MemoryPack` 二进制格式
- `data` 字段不序列化（靠 Id 从缓存重建）
- `Vars` 中包含 `RawData`（Base64 GZip）用于运行时创建的配置
- 持久化只存 `Type` + `Vars`

## 五、ResourceLoader 与资源重定向

### 5.1 `RedirectSourcePath` 实现

```csharp
public void RedirectSourcePath(string originalPath, string newPath)
{
    ResourceLoader.RedirectPath(originalPath, newPath);
}
```

这允许 mod 替换游戏的原生资源路径，rdl mod 就是利用这个来实现模型/动画替换。

## 六、游戏常量与关键路径

### 6.1 路径常量

- Mods 根路径：`Globals.ModsPath`
- 数据目录：`Mods/{ModDir}/Data/`（CSV 数据表）
- 文本目录：`Mods/{ModDir}/Text/`（本地化文本）
- 脚本目录：`{ModDir}/Scripts/Entry.lua` 和 `Entry.dll`
- 资源配置文件：`Configuration.json`

### 6.2 游戏版本

```csharp
Globals.VersionString  // 当前游戏版本号
```

### 6.3 DataType 枚举

反编译中可见的 DataType 值（从使用上下文中推断）：
- Card, Buff, Relic, Career, CardPack, Enemy, EnemyCard, EventList, Map, Hard, Blessing, Dialogue, Partner, PartnerCard, RoleData, EnchTag, KeyWords, Level

### 6.4 关键单例

```csharp
Singleton<GameConfigManager>.Instance  // 配置管理器（含 Mod 加载）
Singleton<DialogueManager>.Instance     // 对话管理器
FightManager.Inst                        // 战斗管理器
RoleTable.Instance / RoleTable.Inst      // 玩家角色数据
GameRuntimeData.Instance                 // 运行时游戏数据
```

## 七、xLua 集成

游戏使用 xLua 作为 Lua 虚拟机：
- `ScriptExecutor` 管理 LuaEnv
- `[LuaCallCSharp(GenFlag.No)]` 标记了 `ModConfig` 和 `ModConfigurationData`（运行时反射绑定）
- `LuaCallCSharp` 属性用于生成 C# 到 Lua 的绑定代码
- `ScriptExecutor.luaEnv.Global` 是全局 Lua 表
- `AddDynamicMethod()` 将 Lua 函数注入到全局环境

## 八、有用的钩子切入点（从反编译推断）

基于 ModHookRegistry 和常见的 mod 模式，以下类型可能有用的钩子：

| 类型 | 方法 | 用途 |
|------|------|------|
| `FightManager` | `StartPlayerTurn` | 玩家回合开始 |
| `FightManager` | `EndPlayerTurn` | 玩家回合结束 |
| `FightManager` | `StartEnemyTurn` | 敌人回合开始 |
| `FightManager` | `OnFightStart` | 战斗开始 |
| `FightManager` | `OnFightEnd` | 战斗结束 |
| `RoleTable` | `TakeDamage` | 受伤时 |
| `RoleTable` | `Heal` | 治疗时 |
| `RoleTable` | `GainMoney` | 获得金钱 |
| `CardItem` | `OnUse` | 使用卡牌时 |
| `BuffItem` | `OnApply` | Buff 施加时 |
| `BuffItem` | `OnRemove` | Buff 移除时 |
| `StatusManager` | `AddStatus` | 添加状态时 |
| `MapManager` | `OnEnterNode` | 进入地图节点 |
| `LobbyManager` | `OnCareerSelected` | 选择职业时 |

## 九、Mod 数据格式（从源码确认）

### 9.1 ModConfig.json 字段

```json
{
  "ModName": "Mod名称",
  "ModVersion": "1.0.0",
  "ModAuthor": "作者",
  "ModDescription": "描述",
  "IconPath": "Icon.png",
  "Enabled": true,
  "Dependencies": ["OtherMod.ModAuthor"],
  "WorkshopVisibility": "Private",
  "PublishedFileId": "",
  "MustSame": true
}
```

### 9.2 Configuration.json 格式

支持用户可配置的 mod 选项，使用 `ModConfigurationData` 类：
```json
{
  "_readme": "说明文本",
  "ExampleBool": true,
  "ExampleNumber": 42,
  "ExampleString": "hello"
}
```

`JsonExtensionData` 支持自定义扩展字段。

### 9.3 CSV 加载细节

- 支持 `.csv` 和 `.xlsx` 两种格式
- `ContainsTableFiles()`: 递归扫描目录下所有 .csv 和 .xlsx 文件
- CSV 编码：`UTF-8`
- 第二行是注释行（自动忽略）
- 每行解析为 `Dictionary<string, string>`

### 9.4 关键词系统

- 自动为 Buff/Card/EnchTag 生成关键词条目
- Buff: `BuffKeyword_{Id}`
- Card: `CardKeyword_{Id}`
- EnchTag: `EnchTag_{Id}`
- 描述中的 `{0}` `{1}` `{2}` `{3}` 被解析为 `DesVal1`/`DesVal2`/`DesVal3`/`DesVal4`
- 描述中的 `({{text}})` 语法被移除（正则 `\(\\{.*?\\}\)`）
- 在生成关键词描述时，会实际运行 `InitScript` 来获取正确的 DesVal

## 十、自动化 API（Unity UI Automation）

> 此为 MCP 相关重点

Witch.dll 中有一整套自动化 API (`Witch.UI.Automation.*`)，包含：
- `RuntimeBattleAutomationFacade` / `RuntimeBattleAutomationService` — 战斗自动化
- `RuntimeGameplayAutomationFacade` / `RuntimeGameplayAutomationService` — 游戏流程自动化
- `RuntimeSceneAutomationFacade` / `RuntimeSceneAutomationService` — 场景自动化
- `RuntimeUiAutomationFacade` / `RuntimeUiAutomationService` — UI 自动化
- `RuntimeUiSnapshot`, `RuntimeUiNode`, `RuntimeUiCanvasInfo`, `RuntimeUiWindowInfo` — UI 结构
- `RuntimeSceneSnapshot`, `RuntimeSceneObjectInfo`, `RuntimeSceneObjectSelector` — 场景结构
- `RuntimePlayCardRequest`, `RuntimePlayCardResult` — 出牌请求
- `RuntimeSceneRaycastRequest`, `RuntimeSceneRaycastHitInfo` — 射线检测

这些表明游戏本身内置了一套自动化框架，MCP 可以直接利用这些现成的服务，而不需要自己通过反射实现。这是非常重要的发现！
