# Entry 文件模式

Entry.lua 和 Entry.dll 是 Mod 的入口。
游戏加载 Mod 时按顺序（`ModConfig.Setup()`，源码 `Witch.Mod.ModConfig.cs`）：**运行 Entry.lua → 加载 Entry.dll → 注册 Hook**。

> **⚠️ Hook 回调签名（决定代码能不能取到数据）：**
> `self:AddMethodHookBefore/After("类型.方法", function(...) ... end)` 回调的**第一个参数是被 Hook 方法的 `this` 实例**（不是 ModHookContext），后续参数是该方法的形参。
> 例（来自真实 Mod）：`"GameEntryUI.ShowCareer"` → `function(ui)`（ui 即 GameEntryUI 实例）；`"ScriptExecutor.ChangeHp"` → `function(exe, amount)`；`"SafeBoxItem.Init"` → `function(item, dataConfig)`。
> 不需要实例时写 `function(_)` 即可。

---

## 模式 1：空 Entry（Plantago / Nana）

**适用场景：** 所有逻辑都在 CSV 脚本列中完成，不需要任何 Hook。

```lua
-- Scripts/Entry.lua
function ModConfig:Setup()
end
```

`ModConfig:Setup()` 是标准入口：游戏加载 Entry.lua 后调用 `ModConfig:Setup()`（`self` 即 ModConfig）。纯内容 Mod（只添加卡牌/Buff/圣物/事件）用空 Entry 即可。
注意：缺失 Entry.lua **不会阻止 Mod 加载**，只会在日志告警（`不存在Entry.lua`）；但需要 Lua Hook 时必须有此文件。

---

## 模式 2：Hook 初始化（BlackMage）

**适用场景：** 需要在游戏特定时机执行一次初始化。

```lua
-- Scripts/Entry.lua
local MP_BUFF_ID = "YourMod_YourCsv_mp"   -- Buff 运行时 ID，格式 {ModFolder}_{CsvFile}_{RawId}
local INITIAL_MP = 40

local function EnsurePlayerResources()
    local player = CS.FightPlayer.Instance
    if player == nil or player.Status == nil then return end
    local status = player.Status
    if status:GetBuff(MP_BUFF_ID) == nil then
        status:AddBuff(MP_BUFF_ID, INITIAL_MP)
    end
end

function ModConfig:Setup()
    self:AddMethodHookAfter("Fight_PlayerTurn.Init", function(_)
        EnsurePlayerResources()
    end)
end
```

**Buff 读写 API（真实，`StatusManager.cs`）：**
- 读：`status:GetBuff(id)` → 返回 `IBuffItem` 或 nil；层数用 `buff.buffConfig.Level`（如 `local mp = self.Self:GetBuff("buff_mp")`）
- 写：`AddBuff(id, level)`，层数**累加**（`Level += level`，正数叠层、负数扣层）。注意 level 类型：**StatusManager 上必须传数字**（`player.Status:AddBuff`、`self.Self:AddBuff`，xLua wrap 只认 number）；**ScriptExecutor 上数字/字符串均可**（卡牌 UseScript 里的 `self:AddBuff`）
- 删：`status:RemoveBuff(id)`
- 精确设置/扣减层数：直接改 `buff.buffConfig.Level = X`（setter 自动钳到 `UpperBound`、`<0→0`；0 层且 `CanZero=false` 时清除 Buff）
- 不存在 `StatusManager:GetStatus/AddStatus/RemoveStatus` 这类方法，别用。

**常用 Hook 点（真实方法名，来自各 Mod 实装）：**

| Hook 方法 | 时机 | 用途 |
|-----------|------|------|
| `Fight_PlayerTurn.Init` | 玩家回合开始时（BlackMage/FixERROR 用） | 冷却递减、资源恢复 |
| `Fight_EnemyTurn.Init` | 敌方回合开始（≈玩家回合结束） | 持续性伤害结算 |
| `Fight_Start.Init` / `FightInit.Init` | 战斗开始时 | 初始化跨战斗数据 |
| `Fight_Win.ResetStates` / `Fight_Escape.ResetStates` / `Fight_Loss.Init` | 战斗结束时 | 保存跨战斗数据 |
| `SettingUI.OnEnable` | 设置界面打开时 | 调试用，验证 Mod 加载 |

> ⚠️ **不要用** `FightManager.StartPlayerTurn` / `EndPlayerTurn` / `OnFightStart` / `OnFightEnd`——这些方法不存在，hook 注册会失败。真实 Mod 对每个 hook 都用 `pcall` 包裹防御（见 3c），并用 `get_recent_logs` 排查 hook 失败。

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
    local canvas = canvasGO:AddComponent(typeof(CS.UnityEngine.Canvas))
    canvas.renderMode = CS.UnityEngine.RenderMode.ScreenSpaceOverlay
    canvas.sortingOrder = 100
    CS.UnityEngine.Object.DontDestroyOnLoad(canvasGO)

    -- 创建 Image
    local bgGO = CS.UnityEngine.GameObject("PredictionBG")
    bgGO.transform:SetParent(canvasGO.transform, false)
    local image = bgGO:AddComponent(typeof(CS.UnityEngine.UI.Image))

    -- 创建 Text（借游戏现有 TMPro 字体：FindObjectsOfTypeAll 拿第一个样本）
    local allTexts = CS.UnityEngine.Resources.FindObjectsOfTypeAll(
        typeof(CS.TMPro.TextMeshProUGUI))
    if allTexts ~= nil and allTexts.Length > 0 then
        local textGO = CS.UnityEngine.GameObject("PredictionText")
        textGO.transform:SetParent(canvasGO.transform, false)
        local myText = textGO:AddComponent(typeof(CS.TMPro.TextMeshProUGUI))
        myText.font = allTexts[0].font
        myText.fontSize = 22
    end

    _predictionCanvas = canvasGO
end
```

### 3b. 反射访问私有成员

```lua
-- 通过反射绕过 xLua 的限制，访问 C# 私有字段（NanaSkillTracker 读 AttackCardItem.hitEnemy）
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

不同游戏版本的方法名可能被混淆（Rougamo 加 `$Rougamo_` 前缀）或需要完整命名空间，真实 Mod 用 `pcall` 逐个尝试：

```lua
-- NanaSkillTracker：混淆名优先，再试原名（均带类型前缀）
function ModConfig:Setup()
    pcall(function() self:AddMethodHookAfter("SkillItem.$Rougamo_CheckRayToEnemy", onCheckRay) end)
    pcall(function() self:AddMethodHookAfter("SkillItem.CheckRayToEnemy", onCheckRay) end)
end

-- EdictOfStars：简单类型名失败时试完整命名空间
function ModConfig:Setup()
    local names = {
        "FightUI.DoCardUseAnimation",
        "Witch.UI.Window.FightUI.DoCardUseAnimation"
    }
    for _, name in ipairs(names) do
        pcall(function()
            self:AddMethodHookBefore(name, function(ui, cardUseData) ... end)
        end)
    end
end
```

---

## 模式 4：C# DLL Entry（DeathRetryMod / LogExp / FateGambler）

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

    [HookAfter(typeof(Fight_Start), nameof(Fight_Start.Init))]
    public static void OnFightStart(Fight_Start __instance)
    {
        Commands.Log("MyMod", "Fight started");
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
   - 运行时 DLL 文件名必须是 `Entry.dll`，**但内部程序集名不能是 Entry**（如 `MyMod.MyAuthor`）。这是为了和其他 DLL Mod 避免冲突
2. `[HookBefore]` 和 `[HookAfter]` 参数：类型 + 方法名字符串
3. Hook 方法**必须 public static**（非 static 会被静默跳过）；返回 void 是惯例
4. 方法第一个参数 `__instance` = 被 Hook 方法的 `this`（`[HookAfter(typeof(Fight_Start), nameof(Fight_Start.Init))]` 里 `__instance` 就是 `Fight_Start` 实例）

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
