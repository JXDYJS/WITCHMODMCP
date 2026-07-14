# 使用模板仓库

官方模板仓库：https://github.com/meowalive/apocalyptic-journey-mod-tutorial

---

## 选择模板

| 模板 | 适用场景 | 语言 | 需要编译？ |
|------|---------|------|-----------|
| `ModTemplate/` | 添加卡牌、Buff、圣物、事件、职业等大部分内容 Mod | Lua + CSV | ❌ |
| `DllTemplate/` | 需要 C# 语言特性、复杂的 Harmony Hook、自定义 UI | C# | ✅ dotnet build |

**95% 的 Mod 应该用 `ModTemplate/`（纯 Lua/CSV），只有以下情况才用 `DllTemplate/`：**
- 需要文件 I/O（写日志、读外部配置文件）
- 需要自定义 Canvas UI 组件
- 需要反射访问游戏私有成员
- 需要引用外部 .NET 库
- 需要高性能的热路径代码

---

## 快速开始

```bash
# 1. 克隆仓库
git clone https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git

# 2. 复制模板
# Lua Mod:
copy ModTemplate /your-workspace/YourModName

# C# DLL Mod:
copy DllTemplate /your-workspace/YourModName

# 3. 重命名配置
# 编辑 YourModName/ModConfig.json：
#   "ModName": "YourModName"     ← 必须和文件夹名一致
#   "ModAuthor": "YourName"
#   "ModVersion": "1.0"
#   "Enabled": true               ← 改为 true

# 4. 开始写内容（见 code-patterns/ 和 testing/）
```

---

## 模板文件说明

### ModTemplate/ 结构

```
ModTemplate/
├── ModConfig.json                # Mod 元数据（编辑：ModName/Author/Version/Enabled）
├── Icon.png                      # 工坊图标（占位图）
├── Scripts/
│   ├── Entry.lua                 # Lua 入口（可选，大部分 Mod 不需要改）
│   ├── ScriptSample.lua          # Lua 脚本参考（包含所有 API 调用示例）
│   └── Lib/
│       ├── TypeHint_0~2.lua      # EmmyLua 类型提示（编辑器的代码补全用）
│       └── DataConfigs/          # **原版游戏的全部 CSV 参考（160+ 文件）**
│           ├── Data/             #   各系统的列名参考
│           └── Text/             #   本地化格式参考
├── Data/                         # 数据 CSV（按类型放在子目录）
│   ├── Card/cardsample.csv       #   卡牌
│   ├── Buff/buffsample.csv       #   Buff
│   ├── Career/careersample.csv   #   职业
│   ├── Relic/relicsample.csv     #   圣物
│   ├── CardPack/cardpack.csv     #   卡包
│   ├── Enemy/enemysample.csv     #   敌人
│   ├── EnemyCard/enemycardsample.csv
│   ├── EventList/eventlistsample.csv
│   ├── Item/itemsample.csv
│   ├── Map/mapsample.csv
│   ├── Partner/partnersample.csv
│   ├── PartnerCard/partnercardsample.csv
│   ├── Blessing/blessingsample.csv
│   ├── RoleData/roledatasample.csv
│   ├── Level/levelsample.csv
│   ├── Hard/hardsample.csv
│   ├── EnchTag/enchtagsample.csv
│   ├── Food/foodsample.csv
│   ├── Dialogue/dialoguesample.csv
│   ├── HouseDialogue/housedialoguesample.csv
│   ├── OutSideShop/outsideshopsample.csv
│   ├── Destiny/destinysample.csv
│   ├── Coin/coinsample.csv
│   ├── Achievement/achievementsample.csv
│   ├── Affection/affectionsample.csv
│   ├── Effect/effectsample.csv
│   ├── SlotCal/slotcalsample.csv
│   ├── SlotReward/slotrewardsample.csv
│   ├── Tutorial/tutorialsample.csv
│   └── Task/tasksample.csv
├── Text/                         # 本地化 CSV（镜像 Data/ 结构）
│   ├── Card/cardsample.csv
│   ├── Buff/buffsample.csv
│   └── ...（同上）
└── ModResource/                  # 资源文件
    ├── AnimationLib/             #   技能动画帧
    ├── Images/                   #   卡牌/圣物/Buff 图片
    └── Icon/                     #   UI 图标
```

### DllTemplate/ 结构

```
DllTemplate/
├── ModConfig.json                # Mod 元数据（编辑：ModName/Author/Enabled）
├── Icon.png                      # 工坊图标
├── Scripts/
│   └── Entry.dll                 # 预编译的 DLL（入口文件，使用 Dev/Entry.cs 重新编译）
└── Dev/
    ├── DllTemplate.sln           # Visual Studio 解决方案
    ├── DllTemplate.csproj        # 项目文件（需编辑 GamePath 指向你的游戏安装目录）
    └── Entry.cs                  # C# 源码
```

---

## 关键初始化步骤

### 1. 编辑 ModConfig.json

```json
{
  "ModName": "MyPlagueMod",
  "ModVersion": "1.0.0",
  "ModAuthor": "MyName",
  "ModDescription": "Adds plague-themed cards",
  "IconPath": "Icon.png",
  "Enabled": true,
  "Dependencies": null
}
```

**规则：**
- `ModName` 必须和文件夹名完全一致
- 运行时 `ModId` 自动生成：`ModName.ModAuthor`
- `Dependencies` 填写其他 Mod 的 ModId（如果依赖）

### 2. 用模板仓库的 Scripts/Lib/DataConfigs/ 作参考

`ModTemplate/Scripts/Lib/DataConfigs/` 下存有原版游戏所有 CSV 的完整列定义。
写 CSV 时对照这些文件，确保列名准确。

### 3. 删除不需要的目录

ModTemplate 包含 30+ 种 CSV 类型模板。你的 Mod 只用到 Card + Buff + CardPack，
就只保留 `Data/Card/` `Data/Buff/` `Data/CardPack/` 和对应的 `Text/` 子目录，
其他目录可以删除。

---

## 示例 Mod 参考

`Example/Defect/` 是一个完整的工作 Mod（故障机器人职业），包含：
- 9 张真实可用的卡牌，每张都有 Lua 脚本逻辑
- 6 个 Buff，含事件驱动效果
- 职业配置 + 圣物 + 卡包
- 完整动画资源（68 帧待机动画）
- 4 语言本地化

详见 `templates/reference-example.md`。
