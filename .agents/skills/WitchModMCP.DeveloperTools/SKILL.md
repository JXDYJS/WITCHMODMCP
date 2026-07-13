---
name: witch-mod-mcp-developertools
description: "Mod development aid for the game Witch (女巫/魔法少女 roguelike deckbuilder): extended tools for combat automation, game flow orchestration, lobby configuration, screenshot diagnostics, raycasting, source decompilation, and RNG control. Requires the base WitchModMCP mod to be loaded. Use when you need the extra DeveloperTools beyond the base WitchModMCP toolset. Not a cheat/trainer tool for regular players. Triggers: DeveloperTools, 开发者工具, 战斗自动化, 流程编排, 存档管理, 截图, 反编译, 出牌, 启程, 大厅配置, 随机种子, 射线检测, 假战斗."
---

# DeveloperTools — Extended Toolset

DeveloperTools 是 WitchModMCP 的扩展工具集，提供增强版工具和独有工具。需配合基座 WitchModMCP 同时加载使用。

**工具发现**: `list_tools` 自动合并基座和扩展的所有工具。通过 `sourceMod` 字段区分来源。

## 传输方式

DeveloperTools 通过 WitchModMCP 的网关暴露。AI 通过 MCP stdio 与 gateway 通信，无需直接连接 HTTP 端口。

参见 WitchModMCP 主 SKILL.md 了解网关架构和连接方式。

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

## 核心原则

1. **先 get_scene_state 再行动** — 每次操作前确认当前页面状态
2. **读后再写** — 先读取状态确认上下文，再执行变更操作
3. **利用假战斗快速测试** — 用基座的 `load_scene type=fakefight` 进入测试战斗
4. **变更后重新读取确认** — 每个 mutation 后重新读取状态验证结果
