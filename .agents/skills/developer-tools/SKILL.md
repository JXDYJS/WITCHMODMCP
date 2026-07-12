---
name: witch-mod-mcp-developertools
description: "Mod development aid for the game Witch (女巫/魔法少女 roguelike deckbuilder): extended tools for combat automation, game flow orchestration, lobby configuration, screenshot diagnostics, raycasting, source decompilation, and RNG control. Requires the base WitchModMCP mod to be loaded. Use when you need the extra DeveloperTools beyond the base WitchModMCP toolset. Not a cheat/trainer tool for regular players. Triggers: DeveloperTools, 开发者工具, 战斗自动化, 流程编排, 存档管理, 截图, 反编译, 出牌, 启程, 大厅配置, 随机种子, 射线检测, 假战斗."
---

# DeveloperTools

DeveloperTools 是 WitchModMCP 的扩展工具集，提供 18 个额外工具。需配合基座 WitchModMCP 同时加载使用。所有工具通过同一 HTTP 端口暴露，`list_tools` 会同时列出基座和扩展工具。

## 模块索引

| 模块 | 工具数 | 说明 |
|------|--------|------|
| [Gameflow](./skills/gameflow/SKILL.md) | 6 | 页面感知与流程导航 — 从主菜单到战斗的完整流程 |
| [Combat](./skills/combat/SKILL.md) | 6 | 战斗操控 — 出牌、回合控制、卡牌堆操作、领取奖励 |
| [Lobby](./skills/lobby/SKILL.md) | 2 | 大厅配置 — 职业、随从、属性、卡包 |
| [Diagnostics](./skills/diagnostics/SKILL.md) | 4 | 开发者诊断 — 截图、射线、RNG 种子、反编译 |

## 全工具速查

| 工具 | 模块 | 说明 |
|------|------|------|
| `get_scene_state` | Gameflow | 检测当前页面/状态 |
| `enter_game` | Gameflow | 主菜单 → 小屋 |
| `start_new_game` | Gameflow | 选择模式 → 大厅 |
| `start_run` | Gameflow | 大厅 → 启程 |
| `check_mode_saves` | Gameflow | 检查存档详情 |
| `list_game_modes` | Gameflow | 列出游戏模式 |
| `get_fight_state` | Combat | 战斗完整快照 |
| `play_card` | Combat | 出牌 |
| `end_turn` | Combat | 结束回合 |
| `set_card_pile` | Combat | 控制卡牌堆 |
| `set_fight_entity` | Combat | 修改实体属性 |
| `claim_rewards` | Combat | 领取战斗奖励 |
| `get_lobby_state` | Lobby | 读取大厅配置 |
| `set_lobby_state` | Lobby | 修改大厅配置 |
| `raycast_mouse` | Diagnostics | 鼠标射线检测 |
| `get_screenshot` | Diagnostics | 截图 |
| `set_rng_seed` | Diagnostics | 随机种子控制 |
| `decompile_source` | Diagnostics | 反编译游戏源码 |

## 典型流程：假战斗测试

```python
import sys; sys.path.insert(0, "scripts")
from witch_mcp import WitchMcp
g = WitchMcp(port=3100)

# 1. 确认连接
if not g.ping():
    raise SystemExit("游戏未运行或 Mod 未加载")

# 2. 检测当前页面
state = g.call("get_scene_state")
print(f"当前页面: {state['page']}")

# 3. 导航到游戏小屋
if state['page'] == 'MAIN_MENU':
    g.call("enter_game")

# 4. 开新游戏 → 大厅
if state['page'] in ('HUB', 'MAIN_MENU'):
    g.call("start_new_game", {"mode": "Normal"})

# 5. 配置大厅（可选）
g.call("set_lobby_state", {
    "careerId": "Career_1",
    "cardPackIds": ["pack_1","pack_2","pack_3","pack_4","pack_5","pack_6"]
})

# 6. 启程
g.call("start_run")

# 7. 跳入假战斗（基座 WitchModMCP 工具）
g.call("load_scene", {"type": "fakefight"})

# 8. 读取战斗状态
fight = g.call("get_fight_state")
print(f"手牌: {len(fight['hand'])} 张, 敌人: {len(fight['enemies'])} 个")

# 9. 出牌测试
r = g.call("play_card", {"index": 0, "targetIndex": 0})
print(f"出牌结果: {r['result']}")

# 10. 结束回合
g.call("end_turn")

# 11. 获胜后领取奖励
g.call("claim_rewards")
```

## 核心原则

1. **先 get_scene_state 再行动** — 每次操作前确认当前页面状态
2. **读后再写** — 先读取状态确认上下文，再执行变更操作
3. **利用假战斗快速测试** — 用基座的 `load_scene type=fakefight` 进入测试战斗
4. **变更后重新读取确认** — 每个 mutation 后重新读取状态验证结果
