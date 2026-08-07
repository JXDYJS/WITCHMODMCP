# references — 参考材料

本目录存放编写 Mod 时的**权威参考资料**，供 AI 与开发者核对真实格式，避免臆造。

| 文件 | 内容 | 何时用 |
|------|------|--------|
| `csv-schemas.md` | 游戏官方全部 CSV 表头（表头行 + 中文注释行，Data + Text 共 85 表） | 写任何 Data/Text CSV 之前，先查对应表的真实列名 |
| `extract_csv_schemas.ps1` | 重新生成 `csv-schemas.md` 的脚本 | 游戏版本更新后刷新表头 |
| `mods/` | （可选）真实参考 Mod | 看真实文件结构 |

## 来源与许可

- `csv-schemas.md` 提取自官方 Mod 模板仓库 **`meowalive/apocalyptic-journey-mod-tutorial`**（**MIT License, © 2026 MeowAlive**），路径 `ModTemplate/Scripts/Lib/DataConfigs/`。
- 需要完整模板（含示例数据、DllTemplate、Example）时：
  `git clone https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git`
- **为什么没有随附社区 Mod 文件**：社区 Mod（BlackMage/Mokou/Plantago/NanaSkillTracker 等）版权归各自作者，未获授权不随 skill 分发；且官方示例 `Example/Defect` 的 CSV 表头已是**旧游戏版本**（缺 `ChoiceIcon/AttackEffect…` 列、多 `FightWidget` 列），照抄会出错。**列名一律以 `csv-schemas.md` 为准。**

## 使用规范

1. **写 CSV 前**：到 `csv-schemas.md` 查对应表（如 `Data/Card/card.csv`、`Data/Buff/buff.csv`、`Data/Career/career.csv`）的真实表头，列名逐字对齐。
2. **列名 vs 列序**：游戏按列名读取，列顺序无关紧要，但列名不能错。
3. **不要臆造列**：`Cost`(实为 `Expend`)、`MaxLayer`(实为 `UpperBound`)、`CardType/Damage/Defend/Heal/Buff/Exhaust/Ethereal` 等都不存在。
4. **刷新**：游戏更新后重新 clone 官方仓库，跑 `extract_csv_schemas.ps1 -TemplateRoot <clone>/ModTemplate/Scripts/Lib/DataConfigs` 覆盖本文件。
