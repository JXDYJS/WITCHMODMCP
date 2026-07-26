# C# DLL 编译

---

## 前提条件

- .NET Framework 4.7.2 SDK（Visual Studio 或 dotnet CLI）
- 游戏安装目录（用于引用游戏 DLL）

---

## 推荐项目结构

```
YourMod/                     ← Mod 完整文件夹（即最终要部署的东西）
├── ModConfig.json
├── Scripts/                 ← C# 编译产物放这里
│   ├── Entry.dll
│   └── YourMod.Contracts.dll
Dev/                         ← C# 源码放在外面
├── YourMod.csproj
└── Entry.cs
```

**C# 源码放 `Dev/` 外面，`YourMod/` 是完整的 Mod 文件夹，编译时自动写入 `Scripts/`。**

---

## csproj 推荐写法

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <AssemblyName>YourMod.YourAuthor</AssemblyName>   <!-- 必须：ModName.ModAuthor -->
    <OutputPath>$(ProjectDir)YourMod\Scripts\</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
    <GameManagedPath>F:\steam\steamapps\common\Witch's Apocalyptic Journey\Witch's Apocalyptic Journey_Data\Managed</GameManagedPath>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="Witch">
      <HintPath>$(GameManagedPath)\Witch.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Witch.Core">
      <HintPath>$(GameManagedPath)\Witch.Core.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(GameManagedPath)\UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <!-- 自动部署：编译后将整个 YourMod/ 复制到游戏 Mods 目录 -->
  <Target Name="DeployMod" AfterTargets="Build"
          Condition="Exists('$(GameManagedPath)')">
    <PropertyGroup>
      <GameModsDir>$([System.IO.Path]::GetDirectoryName($(GameManagedPath)))\Mods\$(AssemblyName)</GameModsDir>
    </PropertyGroup>
    <RemoveDir Directories="$(GameModsDir)" />
    <ItemGroup>
      <ModFiles Include="$(ProjectDir)YourMod\**\*.*"
                Exclude="$(ProjectDir)YourMod\mcp_plugins\**\bin\**;$(ProjectDir)YourMod\mcp_plugins\**\obj\**" />
    </ItemGroup>
    <Copy SourceFiles="@(ModFiles)" DestinationFolder="$(GameModsDir)\%(RecursiveDir)" />
    <Message Importance="high" Text="✅ 已部署到: $(GameModsDir)" />
  </Target>
</Project>
```

**关键规则：**
- `<AssemblyName>` 必须是 `ModName.ModAuthor`，**不能是 `Entry`**
- 运行时 DLL 文件名必须命名为 `Entry.dll`，但内部程序集名不能是 Entry
- `<OutputPath>` 指向你的 Mod 文件夹的 `Scripts/` 子目录
- `<GameManagedPath>` 改为你的实际路径（可用 `get_game_info` 工具查询）
- `DeployMod` 目标自动将整个 `YourMod/` 文件夹复制到游戏 Mods 目录

---

## 编译命令

```bash
# 一行命令，编译 + 自动部署到游戏
dotnet build -c Release
```

编译后 `YourMod/Scripts/` 下会得到 `YourMod.YourAuthor.dll`，
你需要将其重命名为 `Entry.dll`（或者在 csproj 中用 PostBuild 自动改名，参考上方的 `DeployMod` 目标内部逻辑）。

---

## 部署说明

| 方式 | 适用场景 |
|------|----------|
| **`dotnet build`（csproj 自动部署）** | 人类开发者日常使用，一键编译+复制 |
| **`deploy_mod` 工具** | AI 代理使用，可同时重启游戏验证加载状态 |

两个方式可以混用：先 `dotnet build` 编译部署，再用 `deploy_mod` 重启游戏验证，没有冲突。

---

## 热重载

如果游戏已经在运行，编译后只需热重载 MCP 工具 DLL，无需重启游戏：

```python
g.call("reload_tools")
tools = g.call("list_tools")
```

> **注意：** `reload_tools` 只重新加载 MCP 工具 DLL。
> 如果修改了 Entry.cs 中的 Hook，需要重启游戏。
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
| `CS0246` 找不到类型或命名空间 | 缺少游戏 DLL 引用 | 检查 GameManagedPath 指向正确游戏目录 |
| `CS0579` 特性重复 | 自动生成的 AssemblyInfo 冲突 | 添加 `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>` |
| 运行时找不到方法 | 游戏版本与引用 DLL 版本不匹配 | 确保游戏版本一致，或使用反射调用 |
