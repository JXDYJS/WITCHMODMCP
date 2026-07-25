# 完整示例 Mod 参考 — Defect（故障机器人）

路径：模板仓库下的 `Example/Defect/`

这是一个完整的工作 Mod，参考 **Slay the Spire** 的故障机器人角色。
它可以作为你写第一个 Mod 的蓝图。

---

## 文件一览

```
Defect/
├── ModConfig.json
│   ModName: "Defect", ModAuthor: "DLSINNOCENCE"
│   注意：实际 ID = Defect.DLSINNOCENCE（自动生成）
│
├── Scripts/Entry.lua              ← 极简入口，只 Hook SettingUI.OnEnable
│
├── Data/
│   ├── Card/cardsample.csv         ← 9 张卡牌，带 Lua UseScript 逻辑
│   ├── Buff/buffsample.csv         ← 6 个 Buff，带事件驱动
│   └── Career/careersample.csv     ← 职业配置
│
├── Text/
│   ├── Card/cardsample.csv         ← 多语言卡牌文本
│   ├── Buff/buffsample.csv
│   └── Career/careersample.csv
│
├── ModResource/
│   └── AnimationLib/Defect/        ← 完整动画帧
│       ├── Idle/       (68 帧)
│       ├── Attack/
│       ├── Defend/
│       ├── Hit/
│       └── Skill/
│
└── README.md / README.zh-CN.md     ← 文档
```

---

## 关键设计模式

### 1. 卡牌 Lua 脚本

`Data/Card/cardsample.csv` 的 `UseScript` 列包含真实 Lua 代码：

```csv
Id,Rarity,Expend,Tag,InitScript,UseScript
chaos_orb,2,1,,self.Vars:set_Item("BaseScript","CommonCardItem"),self:SetStatus("Self")  …  self:AddBuff(buff_id,"1")
```

**要点：**
- `InitScript` 通过 `self.Vars:set_Item("BaseScript", "CommonCardItem")` 动态设置脚本基类
- Lua 用冒号调用方法：`self:SetStatus("Self")`
- Buff ID 使用 DataId 枚举：`DataId.buff_bleeding`
- `UseScript` 里可以做条件判断、循环、随机

### 2. Buff 事件驱动

`Data/Buff/buffsample.csv` 的 Buff 通过事件钩子实现持续效果：

```
electric_orb buff → 每回合结束触发闪电
ice_orb buff → 回合开始时叠盾
```

**实现方式：** 在 Buff 的 `InitScript` 中调用 `self:AddEvent("EndRound", handler)` 监听游戏事件。

### 3. 职业配置

`Data/Career/careersample.csv` 定义：
- `SanMax` / `HpMax` — 基础属性
- `CardList` — 初始卡组（用 `*` 前缀标记不进入随机池的起始卡牌）
- `RelicList` — 初始圣物
- `PackBelong` — 所属卡包

### 4. 动画资源

`AnimationLib/Defect/` 下每个动画是一个目录，包含：
- `frame_N.png` — 逐帧序列
- `config.json` — 动画参数（`AnimationPerFrame` / `isLoop` / `Direction`）

---

## 从 Defect 到你的 Mod

1. 复制 `Data/Card/cardsample.csv` → 替换卡牌 ID 和 Lua 逻辑
2. 复制 `Data/Buff/buffsample.csv` → 替换 Buff 定义
3. 复制 `Data/Career/careersample.csv` → 修改属性
4. 复制 `Text/` 下的对应文件 → 替换文本
5. 替换 `ModResource/AnimationLib/` 下的动画（如果没有动画，删掉这个目录）
6. 编辑 `ModConfig.json`
7. 部署到游戏 Mods 目录
