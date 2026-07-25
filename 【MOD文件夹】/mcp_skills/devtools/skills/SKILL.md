---
name: witch-mod-mcp-developertools-index
description: "Index of all DeveloperTools skill modules for browsing available tools and documentation."
---

# DeveloperTools — 模块索引

此页面列出所有 DeveloperTools 子模块。通用协议、传输层等见[主 SKILL.md](../SKILL.md)。

## 子模块列表

| 模块 | 工具 | 说明 |
|------|------|------|
| [Gameflow](./gameflow/SKILL.md) | `get_scene_state`, `enter_game`, `start_new_game`, `start_run`, `check_mode_saves`, `list_game_modes` | 页面感知与流程导航 |
| [Combat](./combat/SKILL.md) | `get_fight_state`, `play_card`, `end_turn`, `set_card_pile`, `set_fight_entity`, `claim_rewards` | 战斗操控 |
| [Lobby](./lobby/SKILL.md) | `get_lobby_state`, `set_lobby_state` | 大厅配置 |
| [Diagnostics](./diagnostics/SKILL.md) | `get_screenshot`, `raycast_mouse`, `set_rng_seed`, `decompile_source` | 开发者诊断 |

## 跨模块工作流

| 工作流 | 步骤 | 涉及模块 |
|--------|------|---------|
| **假战斗测试** | `get_scene_state` → `enter_game` → `start_new_game` → `set_lobby_state` → `start_run` → `load_scene`(基座) → `get_fight_state` → `play_card`/`end_turn` | Gameflow → Lobby → Combat |
| **卡牌 Mod 测试** | `load_scene`(基座) → `get_fight_state` → `set_card_pile`(给抽牌堆加测试卡) → `play_card` → `end_turn` → 验证 | Combat |
| **UI 调试** | `raycast_mouse` → `get_screenshot` → (基座) `get_scene_tree` | Diagnostics |
| **源码查阅** | `decompile_source` → 读取反编译文件 | Diagnostics |
