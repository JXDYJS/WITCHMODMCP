# C# DLL 编译

---

## 前提条件

- .NET Framework 4.7.2 SDK（Visual Studio 或 dotnet CLI）
- 游戏安装目录（用于引用游戏 DLL）

---

## 从模板开始

```
DllTemplate/Dev/DllTemplate.csproj  ← 编辑这个文件
DllTemplate/Dev/Entry.cs            ← 写你的代码
```

## 编辑 csproj

```xml
<PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <AssemblyName>YourMod.YourName</AssemblyName>   <!-- 必须：ModName.ModAuthor -->
    <GamePath>F:\steam\steamapps\common\Witch's Apocalyptic Journey</GamePath>  <!-- 改为你的实际路径，可用 get_game_info 工具查询 -->
    <DllPath>$(GamePath)\Witch's Apocalyptic Journey_Data\Managed</DllPath>
</PropertyGroup>
```

**关键规则：**
- `<AssemblyName>` 必须是 `ModName.ModAuthor`，**不能是 `Entry`**
- 运行时 DLL 文件名必须命名为 `Entry.dll`，但内部程序集名不能是 Entry
- `<GamePath>` 指向你的游戏安装目录

---

## 编译命令

```bash
# 进入 Dev/ 目录
cd Dev

# 编译
dotnet build DllTemplate.csproj -c Release

# 编译输出在 Dev/bin/Release/net472/YourMod.YourName.dll
```

---

## 部署 DLL

```bash
# 复制 DLL 到 Mod 的 Scripts/ 目录
copy Dev\bin\Release\net472\YourMod.YourName.dll Scripts\Entry.dll

# （可选）复制 PDB 文件用于堆栈跟踪
copy Dev\bin\Release\net472\YourMod.YourName.pdb Scripts\Entry.pdb
```

---

## 热重载

如果游戏已经在运行：

```python
# 1. 编译 DLL
# 2. 复制 Entry.dll 到 Scripts/
# 3. 在游戏中调用：
g.call("reload_tools")
# 4. 确认新工具已注册：
tools = g.call("list_tools")
```

> **注意：** `reload_tools` 只重新加载 MCP 工具 DLL。
> 如果你修改了 Entry.cs 中的 Hook，需要重启游戏。
> 只有新增/修改 `IMcpTool` 实现才支持热重载。

---

## C# 源码模板

```csharp
using Witch.Mod;
using Witch.UI.Window;
using UnityEngine;

namespace YourMod;

public static class Entry
{
    [ModInitialize]
    public static void Init(ModConfig modConfig)
    {
        Commands.Log("YourMod", "Mod loaded!");
    }
}

public static class Hooks
{
    [HookBefore(typeof(SettingUI), nameof(SettingUI.OnEnable))]
    public static void OnSettingOpen(SettingUI __instance)
    {
        Commands.Log("YourMod", "Settings opened");
    }

    [HookAfter(typeof(FightManager), nameof(FightManager.StartPlayerTurn))]
    public static void OnPlayerTurnStart(FightManager __instance)
    {
        Commands.Log("YourMod", "Player turn started");
    }
}
```

---

## 常见编译错误

| 错误 | 原因 | 修复 |
|------|------|------|
| `CS0246` 找不到类型或命名空间 | 缺少游戏 DLL 引用 | 检查 GamePath 和 DllPath 指向正确游戏目录 |
| `CS0579` 特性重复 | 自动生成的 AssemblyInfo 冲突 | 在 csproj 中添加 `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>` |
| 运行时找不到方法 | 游戏版本与引用 DLL 版本不匹配 | 确保游戏版本一致，或使用反射调用 |
