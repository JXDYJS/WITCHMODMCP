# 魔女之灾 Mod 开发指南

## Mod 类型

| 类型 | 说明 | 例子 |
|------|------|------|
| **Content Mod** | 添加卡牌、职业、圣物、Buff、事件 | EdictOfStars, SunExp, PW_Mahjong |
| **Plugin Mod** | 通过 Hook/反射修改游戏行为 | NanaSkillTracker, DeathRetryMod |
| **Asset Mod** | 替换游戏资源（动画、图片） | rdl |

一个 Mod 可以同时属于多个类型。

---

## 目录结构

```
ModName/
├── ModConfig.json           # 必需：Mod 元数据
├── Icon.png                 # 可选：Steam 工坊图标
├── Configuration.json       # 可选：用户可配置选项
├── Scripts/
│   ├── Entry.lua            # Lua 入口（可选）
│   ├── Entry.dll            # C# 入口（可选）
│   └── Entry.pdb            # 调试符号（可选）
├── Data/                    # CSV 数据表（可选）
│   ├── Card/
│   ├── Buff/
│   ├── Relic/
│   ├── Career/
│   ├── CardPack/
│   ├── RoleData/
│   ├── Partner/
│   ├── PartnerCard/
│   ├── Blessing/
│   ├── EventList/
│   ├── Map/
│   ├── Hard/
│   ├── Enemy/
│   ├── EnemyCard/
│   ├── Level/
│   ├── EnchTag/
│   └── Dialogue/
├── Text/                    # 本地化文本 CSV（可选，镜像 Data/ 结构）
│   ├── Card/
│   ├── Buff/
│   ├── ...
│   └── KeyWordsDic/
├── ModResource/             # 资源文件（可选）
│   ├── AnimationLib/        # 技能动画
│   ├── Images/              # 卡牌/圣物/Buff 图片
│   └── Icon/                # UI 图标
└── SharedResources/         # 跨 Mod 共享资源
```

---

## ModConfig.json 格式

```json
{
  "ModName": "MyMod",
  "ModVersion": "1.0.0",
  "ModAuthor": "AuthorName",
  "ModDescription": "Description",
  "IconPath": "Icon.png",
  "Enabled": true,
  "Dependencies": ["OtherMod.AuthorName"],
  "MustSame": true,
  "WorkshopVisibility": "Private",
  "PublishedFileId": ""
}
```

- `ModId` 自动生成为 `ModName.ModAuthor`
- `MustSame` 跟踪数据配置变更
- `Dependencies` 使用 ModId 格式

---

## 端到端工作流

### 第 1 步：创建工作区目录

在工作区的 `【MOD文件夹】/<ModName>/` 下创建目录结构（参考上面的目录结构）。

### 第 2 步：写 ModConfig.json

同上格式。

### 第 3 步：写 Data Card CSV（Data/Card/<cards>.csv）

```csv
Id,Rarity,Cost,CardType,TargetType,DamageType,Damage,Defend,Buff,SelfBuff,Exhaust,Icon,BaseScript,PackBelong,InitScript,UseScript
# id,稀有度,费用,类型,目标,伤害类型,伤害,护盾,Buff,自身Buff,消耗,图标,脚本基类,所属卡包,初始化脚本,使用脚本
1001,common,1,Attack,enemy,physical,6,0,,,false,icon_mycard,AttackCardItem,pack_mycardpack,self.Vars.DesVal1=tostring(6),self:AddBuff(DataId.buff_bleeding,"3")
```

**关键规则**：
- 第 2 行是注释行（`#` 开头），会被跳过
- 编码必须是 **UTF-8**
- `Id` 列必须唯一
- `BaseScript` 必填：造成伤害用 `AttackCardItem`，无目标用 `CommonCardItem`
- `PackBelong` 必填，指向一个在 Data/CardPack/ 中定义的 CardPack
- 运行时 ID = `{Mod文件夹名}_{Csv文件名}_{原始Id}`，例如 `MyMod_mycards_1001`
- 脚本列名称必须包含 "Script" 才会被识别为 Lua 代码
- Lua 方法调用必须用冒号 `self:AddBuff()`，不用点 `self.AddBuff()`

### 第 4 步：写 Text Card CSV（Text/Card/<cards>.csv）

```csv
Id,Type,Name,Name_en,Name_zh-Hant,Name_ja,Description,Description_en,Description_zh-Hant,Description_ja
# id,类型,名称,英文,繁中,日文,描述,英文描述,繁中描述,日文描述
1001,Card,Plague Spread,Plague Spread,瘟疫传播,疫病拡散,Deal {0} damage. Apply {1} poison.,Deal {0} damage. Apply {1} poison.,造成 {0} 点伤害。施加 {1} 层中毒。,{0}ダメージを与える。毒を{1}付与する。
```

- `{0}`~`{3}` 由 `InitScript` 中的 `DesVal1`~`DesVal4` 替换
- 4 种语言：zh-Hans（简中）、zh-Hant（繁中）、en（英文）、ja（日文）

### 第 5 步：写 CardPack CSV

Data/CardPack/<pack>.csv：
```csv
Id,Name,Description,CardList
pack_mycardpack,My Pack,Contains my custom card,1001
```

Text/CardPack/<pack>.csv：
```csv
Id,Name,Name_en,Name_zh-Hant,Name_ja,Description,Description_en,Description_zh-Hant,Description_ja
pack_mycardpack,My Pack,My Pack,我的卡包,マイパック,Description...,Description...,描述...,説明...
```

### 第 6 步：写 Entry.lua（Scripts/Entry.lua）

```lua
function ModConfig:Setup()
    CS.UnityEngine.Debug.Log("[MyMod] Mod loaded!")
end
```

### 第 7 步：部署到游戏目录

**如果 MCP 工具中有 `deploy_mod`**：
```
deploy_mod(source="workspace/【MOD文件夹】/MyMod", dry_run=true)
→ 确认预览结果
→ deploy_mod(source="workspace/【MOD文件夹】/MyMod", dry_run=false)
```

**否则**：手动将 `【MOD文件夹】/MyMod` 复制到 `{游戏目录}/Witch's Apocalyptic Journey_Data/Mods/`。

### 第 8 步：生成测试脚本

编写 `tests/test_MyMod.py` 作为验证脚本。参考下方的【标准化测试模板】。

### 第 9 步：重启游戏 → 跑测试

玩家重启游戏后，执行 `python tests/test_MyMod.py`。

---

## 标准化测试模板

每次写完 Mod 后，生成如下测试脚本：

```python
#!/usr/bin/env python3
"""test_<ModName>.py — 一键验证 Mod 是否加载和功能正确

使用方法：重启游戏并确保 WitchModMCP 运行后，执行：
  python tests/test_<ModName>.py

依赖：pip install mcp  （或使用项目已有的 Python 环境）
"""

import json
import subprocess
import sys
import time
from pathlib import Path


# ── 配置 ──────────────────────────────────────────────────────────
MOD_NAME = "<ModName>"
CARD_IDS = [1001]  # 本 Mod 添加的卡牌 ID
BUFF_IDS = []       # 本 Mod 添加的 Buff ID
PACK_ID = "pack_mycardpack"
EXPECTED_DAMAGE = 6
EXPECTED_BUFF = "buff_bleeding"
EXPECTED_BUFF_STACKS = 3
# ──────────────────────────────────────────────────────────────────


def log(msg):
    print(f"  {msg}")


def check(step, ok, detail=""):
    if ok:
        log(f"✅ {step}")
    else:
        log(f"❌ {step}: {detail}")
    return ok


def main():
    print(f"\n{'='*50}")
    print(f" Mod 测试: {MOD_NAME}")
    print(f"{'='*50}\n")

    # 连接 MCP 网关
    # （这里使用子进程启动网关，或连接已有网关）
    # 根据不同环境调整连接方式

    # ── Phase 1: 连接检查 ──
    log("Phase 1: 连接游戏 Mod")
    # 调用方式取决于具体工具链：
    # - 使用 witchmod MCP 工具：通过 mcp 库连接
    # - 或通过 HTTP 直连游戏 Mod
    
    # 伪代码示例：
    # state = call("get_scene_state")
    # check("连接成功", state is not None)

    # ── Phase 2: Mod 加载检查 ──
    log("\nPhase 2: Mod 加载状态")
    # mods = call("dump_mod_state")
    # check(f"{MOD_NAME} 已加载", MOD_NAME in mods)

    # ── Phase 3: 错误检查 ──
    log("\nPhase 3: 错误检查")
    # logs = call("get_recent_logs", {"count": 30})
    # errors = [l for l in logs if "Error" in str(l)]
    # check("无加载错误", len(errors) == 0, str(errors))

    # ── Phase 4: 配置验证 ──
    log("\nPhase 4: 配置表验证")
    # for cid in CARD_IDS:
    #     cfg = call("query_config", {"tableName": "CardConfig", "id": cid})
    #     check(f"Card {cid} 已注册", cfg is not None)
    # for bid in BUFF_IDS:
    #     cfg = call("query_config", {"tableName": "BuffConfig", "id": bid})
    #     check(f"Buff {bid} 已注册", cfg is not None)

    # ── Phase 5: 运行时验证 ──
    log("\nPhase 5: 战斗测试")

    # 进入主菜单 → 小屋 → 大厅（如果不在游戏中）
    # call("enter_game")
    # call("start_new_game", {"mode": "Standard"})
    # call("set_lobby_state", {"career": "Witch", "card_pack": PACK_ID, "confirm": True})
    # call("start_run")

    # 进入假战斗
    # call("load_scene", {"type": "fakefight"})
    # fight = call("get_fight_state")

    # 注入测试卡牌到抽牌堆
    # call("give_item", {"item_type": "card", "value": str(CARD_IDS[0])})

    # 抽牌并验证
    # fight = call("get_fight_state")
    # hand = fight.get("handCards", [])
    # check("卡牌在手牌中", any(c.get("id") == CARD_IDS[0] for c in hand))

    # 出牌测试
    # result = call("play_card", {"card_index": 0, "target_index": 0})
    # check("出牌成功", "error" not in str(result))

    # 验证效果
    # fight = call("get_fight_state")
    # enemy_buffs = fight.get("enemies", [{}])[0].get("buffs", [])
    # has_buff = any(b.get("id") == EXPECTED_BUFF and b.get("stack") >= EXPECTED_BUFF_STACKS for b in enemy_buffs)
    # check(f"Buff {EXPECTED_BUFF} 已添加 {EXPECTED_BUFF_STACKS} 层", has_buff)

    # 结束回合
    # call("end_turn")

    print(f"\n{'='*50}")
    print(" 测试完成")
    print(f"{'='*50}\n")


if __name__ == "__main__":
    main()
```

AI 每次应当：

1. 复制这个模板到 `tests/test_{ModName}.py`
2. 填入 `MOD_NAME`、`CARD_IDS`、`BUFF_IDS`、`PACK_ID`、期望数值
3. 根据可用 MCP 工具替换伪代码部分的调用方式
4. 玩家重启游戏后跑一次即可验证

---

## 验证检查清单

写完 Mod 文件后，按以下顺序逐项确认：

### ModConfig.json
- [ ] `ModName` 与文件夹名一致
- [ ] `ModAuthor` 已填写
- [ ] `Enabled` 为 `true`

### Data CSV
- [ ] 第 1 行是列名（表头）
- [ ] 第 2 行是 `#` 开头的注释行
- [ ] `Id` 列值在文件内唯一
- [ ] ID 不与游戏保留范围 (1-5000) 冲突
- [ ] `PackBelong` 已设置
- [ ] `BaseScript` 非空（`AttackCardItem` 或 `CommonCardItem`）

### Text CSV
- [ ] 结构与对应的 Data CSV 一致
- [ ] 至少填了中文名和描述

### Entry 文件
- [ ] `Scripts/Entry.lua` 存在（纯 CSV Mod 可以没有，但建议保留一个空 Lua）
- [ ] Lua 方法调用使用冒号 `self:xxx()` 而非点 `self.xxx()`

### 部署
- [ ] 文件已复制到游戏 Mods 目录
- [ ] 游戏 Mod 管理器中已启用本 Mod

### 说明
如果 MCP 工具中有 `validate_mod`，可以直接用：
```python
result = call("validate_mod", {"mod_path": "workspace/【MOD文件夹】/MyMod"})
```

---

## 故障排查

### Mod 未加载

| 现象 | 检查 |
|------|------|
| `dump_mod_state` 中找不到 | `ModConfig.json Enabled=false` → 改为 `true` |
| `get_recent_logs` 显示 "parse failed" | JSON 语法错误 → 用 JSON 验证工具检查 |
| ModId 冲突 | 另一个 Mod 使用了相同的 `ModName.ModAuthor` |
| 依赖错误 | `Dependencies` 指向了不存在或未启用的 Mod |

### 卡牌/Buff 未出现

| 现象 | 检查 |
|------|------|
| `query_config` 查不到 | CSV 文件不在正确的 `Data/` 子目录下 |
| 游戏内看不到卡牌 | `PackBelong` 未设置或指向的 CardPack 不存在 |
| 卡牌无名/无描述 | 缺少对应的 Text CSV |
| 卡牌无法打出 | `BaseScript` 未设置 |
| 卡牌图标显示为 "?" | `Icon` 路径错误或图片文件不存在 |

### Lua 脚本错误

| 现象 | 检查 |
|------|------|
| 日志显示 Lua 编译错误 | 脚本列 Lua 语法错误 |
| 脚本列不执行 | 列名不包含 "Script" |
| `self:AddBuff()` 报错 | 检查参数是否正确 `self:AddBuff(id, level)` |
| 字典访问失败 | xLua 不支持 `dict[key]`，用 `dict:get_Item(key)` |
| `CS.xxx` 为 nil | 该类型未导出到 Lua |

### Quick Diagnostic

```python
# 1. 检查 Mod 是否加载
state = call("dump_mod_state")
print(f"{state['modCount']} mods loaded")

# 2. 检查错误日志
logs = call("get_recent_logs", {"count": 30})
for entry in logs:
    if 'Error' in entry:
        print(entry)

# 3. 验证配置表
cfg = call("query_config", {"tableName": "CardConfig", "id": 你的卡牌ID})
print(cfg)

# 4. 检查场景状态
scene = call("get_scene_state")
print(scene['page'])
```

---

## 常见 Mod 开发模式

### Buff-as-Resource

用 Buff 模拟次级资源（MP/能量/元素）：

```lua
-- 检查 Buff 层数
local fuel = StatusManager:GetStatus("fuel_buff")
if fuel and fuel >= 3 then
    -- 触发效果
end

-- 添加资源 Buff
StatusManager:AddStatus("element_fire", 1, source, target)
```

示例 Mod：BlackMage（冰火雷元素系统）、PW_Mahjong（麻将牌）、Mokou（燃料系统）

### Cooldown Tracking

用 `SkillTime` 跟踪战斗内冷却：

```lua
if self.Vars.SkillTime == nil then
    self.Vars.SkillTime = 0
end
self.Vars.SkillTime = self.Vars.SkillTime + 1
if self.Vars.SkillTime >= cooldown then
    -- 技能就绪
end
```

### Card Transformation

战斗中替换手牌：

```lua
local cardId = self.data["Id"]
if cardId == "MyMod_Card_101" then
    StatusManager:RemoveStatus("fuel_buff", 3, source)
    FightManager.Inst:FightAddCard("MyMod_Card_102")
end
```

### Dice Check

```lua
local roll = Dice.Roll()
if roll >= threshold then
    -- 成功路径
else
    -- 失败路径
end
```

### Milestone System

跨战斗计数：

```lua
if SpecialVars["my_counter"] == nil then
    SpecialVars["my_counter"] = 0
end
SpecialVars["my_counter"] = SpecialVars["my_counter"] + 1

if SpecialVars["my_counter"] >= 5 then
    SpecialVars["my_counter"] = 0
    -- 触发里程碑效果
end
```

### Asset-Only Mod

只替换资源，不需 Lua 逻辑：

```lua
function Setup(mod)
    mod:RedirectSourcePath("Characters/Default", "rdl/Characters/MyChar")
end
```

---

## CSV 编写关键约定

1. 文件名对应配置类型，如 `Data/Card/mycards.csv`
2. ID 留间隔（1001, 2001, 3001）方便后续扩展
3. 图标路径不带后缀名（游戏自动追加 `.png`）
4. 所有 CSVs 使用 UTF-8 编码
5. PackBelong 必须指向一个已存在的 CardPack ID
6. 如果添加了 Data CSV 但没有 Text CSV，卡牌在游戏内会显示为空白名称
