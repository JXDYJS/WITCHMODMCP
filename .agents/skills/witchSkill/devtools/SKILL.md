---
name: witch-mod-mcp-developertools
description: "Mod development aid for the game Witch (魔女:终末旅途 roguelike deckbuilder): developer-tool documentation for combat automation, game flow orchestration, lobby configuration, screenshot diagnostics, raycasting, source decompilation, and RNG control. These tools are implemented by the base WitchModMCP.Contracts mod (no separate DeveloperTools mod exists) — this module is a developer-debugging view over the same tools documented in base/*. Not a cheat/trainer tool for regular players. Triggers: DeveloperTools, 开发者工具, 战斗自动化, 流程编排, 存档管理, 截图, 反编译, 出牌, 启程, 大厅配置, 随机种子, 射线检测, 假战斗."
---

# DeveloperTools

DeveloperTools 是 WitchModMCP 的**开发者工具子集文档**，涵盖 18 个工具的使用说明。这些工具全部由**基座** `WitchModMCP.Contracts` 实现（不存在单独的 DeveloperTools Mod），与基座 `base/*` 模块共用同一 HTTP 端口，`list_tools` 会一次性列出全部工具。

> ⚠️ **本模块与 `base/` 模块描述的是同一批工具**：`get_scene_state`/`enter_game`/`start_new_game`/`start_run` 等见 `base/meta` 与 `base/gameflow`；`get_fight_state`/`play_card`/`end_turn` 等见 `base/combat`；`get_lobby_state`/`set_lobby_state` 见 `base/lobby`。本模块侧重于**开发调试视角**的编排（假战斗测试、流程导航、源码反编译）。

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

```
1. get_scene_state           → 确认当前页面
2. enter_game                → 主菜单 → 小屋
3. start_new_game {mode}     → 选择模式 → 大厅
4. set_lobby_state           → 配置职业/卡包
5. start_run                 → 启程
6. load_scene {fakefight}    → 跳入假战斗
7. get_fight_state           → 读取战斗状态
8. play_card {index, target} → 出牌测试
9. end_turn                  → 结束回合
10. claim_rewards            → 领取奖励（获胜后）
```

## 核心原则

1. **先 get_scene_state 再行动** — 每次操作前确认当前页面状态
2. **读后再写** — 先读取状态确认上下文，再执行变更操作
3. **利用假战斗快速测试** — 用基座的 `load_scene type=fakefight` 进入测试战斗
4. **变更后重新读取确认** — 每个 mutation 后重新读取状态验证结果
