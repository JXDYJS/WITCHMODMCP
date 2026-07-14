# Entry 文件模式

Entry.lua 和 Entry.dll 是 Mod 的入口。
游戏在加载 Mod 时会按顺序：运行 Entry.lua → 加载 Entry.dll → 注册 Hook。

---

## 模式 1：空 Entry（Plantago / Nana）

**适用场景：** 所有逻辑都在 CSV 脚本列中完成，不需要任何 Hook。

```lua
-- Scripts/Entry.lua
function ModConfig:Setup()
end
```

即使什么都不做，Entry.lua 也必须存在（否则游戏不会加载 Mod）。
这是**最简形式**，适合纯内容 Mod（只添加卡牌/Buff/圣物/事件）。

---

## 模式 2：Hook 初始化（BlackMage）

**适用场景：** 需要在游戏特定时机执行一次初始化。

```lua
-- Scripts/Entry.lua
function ModConfig:Setup()
    self:AddMethodHookAfter("Fight_PlayerTurn.Init", function(_)
        -- 战斗初始化时确保资源 Buff 存在
        EnsurePlayerResources()
    end)
end

function EnsurePlayerResources()
    -- 初始化 MP（如果不存在）
    local mp = StatusManager:GetStatus("buff_mp")
    if mp == nil or mp == 0 then
        StatusManager:AddStatus("buff_mp", 100, source, target)
    end
end
```

**常用 Hook 点：**

| Hook 方法 | 时机 | 用途 |
|-----------|------|------|
| `FightManager.StartPlayerTurn` | 玩家回合开始时 | 冷却递减、资源恢复 |
| `FightManager.EndPlayerTurn` | 玩家回合结束时 | 持续性伤害结算 |
| `FightManager.OnFightStart` | 战斗开始时 | 初始化跨战斗数据 |
| `FightManager.OnFightEnd` | 战斗结束时 | 保存跨战斗数据 |
| `SettingUI.OnEnable` | 设置界面打开时 | 调试用，验证 Mod 加载 |

---

## 模式 3：复杂 Lua Entry（EdictOfStars / NanaSkillTracker）

**适用场景：** 需要在运行时操作 Unity UI、访问私有 C# 成员、管理复杂状态。

### 3a. 运行时创建 UI

```lua
-- NanaSkillTracker 模式：在游戏运行时动态创建 Canvas UI
function ModConfig:Setup()
    self:AddMethodHookAfter("Fight_PlayerTurn.Init", function(_)
        CreatePredictionUI()
    end)
end

function CreatePredictionUI()
    if _predictionCanvas then return end  -- 防止重复创建

    -- 创建 Canvas
    local canvasGO = CS.UnityEngine.GameObject("PredictionCanvas")
    CS.UnityEngine.Object.DontDestroyOnLoad(canvasGO)
    local canvas = canvasGO:AddComponent(typeof(CS.UnityEngine.Canvas))
    canvas.renderMode = CS.UnityEngine.RenderMode.ScreenSpaceOverlay
    canvas.sortingOrder = 100

    -- 创建 Image
    local bgGO = CS.UnityEngine.GameObject("PredictionBG")
    bgGO.transform:SetParent(canvasGO.transform, false)
    local image = bgGO:AddComponent(typeof(CS.UnityEngine.UI.Image))

    -- 创建 Text（通过 FindObjectsOfTypeAll 借游戏字体）
    local texts = CS.UnityEngine.Resources.FindObjectsOfTypeAll(
        typeof(CS.TMPro.TextMeshProUGUI))
    _predictionCanvas = canvasGO
end
```

### 3b. 反射访问私有成员

```lua
-- 通过反射绕过 xLua 的限制，访问 C# 私有字段
function GetPrivateField(obj, fieldName)
    local objType = obj:GetType()
    -- 尝试当前类型
    local field = objType:GetField(fieldName, 36)  -- 36 = Instance | NonPublic
    if field == nil then
        -- 失败时尝试基类
        local baseType = objType.BaseType
        if baseType then
            field = baseType:GetField(fieldName, 36)
        end
    end
    if field then
        return field:GetValue(obj)
    end
    return nil
end
```

### 3c. 跨版本兼容 Hook

```lua
-- NanaSkillTracker 的双重 Hook 模式：
-- 某些版本的游戏方法名被混淆了，尝试两个名字
function Setup(mod)
    -- 尝试原始方法名
    local hooked = pcall(function()
        mod:AddMethodHookAfter("CheckRayToEnemy", handler)
    end)
    -- 如果失败，尝试混淆后的方法名
    if not hooked then
        pcall(function()
            mod:AddMethodHookAfter("$Rougamo_CheckRayToEnemy", handler)
        end)
    end
end
```

---

## 模式 4：C# DLL Entry（DeathRetryMod / LogExp）

**适用场景：** 需要文件 I/O、引用外部库、高性能逻辑。

### C# Entry 基本结构

```csharp
using Witch.Mod;
using UnityEngine;

namespace MyMod;

public static class Entry
{
    [ModInitialize]
    public static void Init(ModConfig modConfig)
    {
        Commands.Log("MyMod", "MyMod loaded!");

        // modConfig.DirectoryName = Mod 安装路径
        // 可以在这里写文件、创建目录等
    }
}

// Hook 类必须在同一个程序集中
public static class MyHooks
{
    [HookBefore(typeof(SettingUI), nameof(SettingUI.OnEnable))]
    public static void OnSettingOpen(SettingUI __instance)
    {
        Commands.Log("MyMod", "Settings UI opened");
    }

    [HookAfter(typeof(FightManager), nameof(FightManager.StartPlayerTurn))]
    public static void OnPlayerTurnStart(FightManager __instance)
    {
        Commands.Log("MyMod", "Player turn started");
    }
}
```

### 文件 I/O 示例（LogExp 模式）

```csharp
[ModInitialize]
public static void Init(ModConfig modConfig)
{
    var logDir = Path.Combine(modConfig.DirectoryName, "Logs");
    Directory.CreateDirectory(logDir);
    var logFile = Path.Combine(logDir,
        $"Witch-{DateTime.Now:yyyyMMdd-HHmmss}.log");

    // Hook Unity 日志系统
    Application.logMessageReceived += (condition, stackTrace, type) =>
    {
        File.AppendAllText(logFile,
            $"[{DateTime.Now:HH:mm:ss}] [{type}] {condition}\n");
        if (!string.IsNullOrEmpty(stackTrace))
            File.AppendAllText(logFile, $"{stackTrace}\n");
    };
}
```

### C# Hook 注意事项

1. Assembly Name 必须是 `ModName.ModAuthor`（不是 `Entry`）
   - 运行时 DLL 文件名必须是 `Entry.dll`，**但内部程序集名不能是 Entry**
   - 这是为了和其他 DLL Mod 避免冲突
2. `[HookBefore]` 和 `[HookAfter]` 参数：类型 + 方法名字符串
3. Hook 方法必须 public static，返回 void
4. `__instance` 参数 = 被 Hook 的 `this`

---

## 模式对比

| 模式 | 复杂度 | 编译需要？ | 适用场景 |
|------|--------|-----------|---------|
| 空 Entry | 极低 | ❌ | 纯内容 Mod，CSV 就够 |
| Hook 初始化 | 低 | ❌ | 需要在游戏事件时初始化 |
| 复杂 Lua Entry | 高 | ❌ | 运行时 UI、反射、状态管理 |
| C# DLL Entry | 中-高 | ✅ dotnet build | 文件 I/O、外部库、高性能代码 |

**建议：** 95% 的 Mod 不需要 C# DLL。先从最简单的 Entry 开始，
只有当 Lua 确实不够用时才考虑 C#。
