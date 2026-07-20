---
name: witch-mod-mcp-developertools-gameflow
description: "DeveloperTools game flow tools: page detection, game state machine navigation from main menu through hub, lobby, map, and into fights. Use when the user wants to know what page the game is on, enter the game, start a new run, check saves, or list game modes. Triggers: gameflow, 流程, 页面, get_scene_state, enter_game, start_new_game, start_run, check_mode_saves, list_game_modes, 页面检测, 启程, 导航."
---

# Gameflow 模块 — 页面感知与流程导航

驱动游戏状态机跨越所有主要页面：主菜单 → 模式选择 → 职业大厅 → 地图 → 战斗。

## 工具总览

| 工具 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `get_scene_state` | — | `{page, inRun, inFight, fightType?, player?, modals, transitioning, overlays}` | 检测当前页面。**每次操作的起点** |
| `enter_game` | — | `{result, message, page}` | 主菜单 → 小屋。轮询等待（最长 15s） |
| `start_new_game` | `{mode, useExistingSave?}` | `{result, mode, usedExisting, page, message}` | 选模式 → 大厅。轮询等待（最长 15s） |
| `start_run` | — | `{result, message, page, level?}` | 大厅 → 启程。轮询等待（最长 20s），有自动回退机制 |
| `check_mode_saves` | `{mode?}` | `{hasSaves, totalSaves, validSaves, saves}` | 检查指定模式的存档详情 |
| `list_game_modes` | — | `{modes: [{mode, hasSave, saveCount, save?}]}` | 列出所有可用的游戏模式 |
| `map_select_state` | — | `{selectableNodes, slots, canContinue}` | 获取地图节点编排界面状态 |
| `map_select_assign` | `{slotIndex, nodeId}` | `{result, placed, message}` | 将可选节点放置到槽位。**单次放一个** |
| `map_select_clear` | `{slotIndex}` | `{result}` | 清空指定槽位 |
| `map_select_confirm` | — | `{result}` | 确认编排并继续前进 |

---

### map_select_state

获取地图节点编排界面(MapSelectUI)的当前状态。

**返回字段：**
| 字段 | 类型 | 说明 |
|------|------|------|
| `selectableNodes` | array | 可选节点列表（"手牌"） |
| `selectableNodes[].nodeId` | string | **稳定 ID**，唯一标识一个节点（如 "shop"、"Breaks"、"level_10006"） |
| `selectableNodes[].index` | int | **⚠️ 不稳定序号**，每次状态刷新都会变化，不要依赖 |
| `slots` | array | 6个槽位（0=起点, 1-4=中间, 5=终点） |
| `canContinue` | bool | 是否所有槽位已填满可以继续 |

**重要：`index` 字段是遍历时动态生成的序号，不是稳定 ID。** 每次放置节点后，可选列表缩小、索引全部偏移。**永远不要用 `index` 来定位节点。**

### map_select_assign

将可选节点放置到槽位。**每次调用只放一个节点。**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `slotIndex` | int | 是 | 槽位索引 (0-5) |
| `nodeId` | string | 是 | **稳定 ID**，从 `map_select_state` 的 `selectableNodes[].nodeId` 获取 |

**返回：**
| 字段 | 说明 |
|------|------|
| `result` | `"success"` / `"error"` |
| `placed` | 成功放置的 `[{slotIndex, nodeId}]` |
| `message` | 状态描述 |

**⚠️ 关键行为说明：**
- 使用 `nodeId`（稳定配置表ID）而非 `index`。`index` 是容器遍历的动态序号，每次放置后偏移
- 旧版曾支持 `mappings` 批量数组和 `nodeIndex`，均已移除。当前版本每次只放一个节点
- 如果同一个 `nodeId` 已被放置，再次引用会失败

**Python：**
```python
# 逐次放置
g.call("map_select_assign", {"slotIndex": 1, "nodeId": "shop"})
g.call("map_select_assign", {"slotIndex": 2, "nodeId": "level_10007"})
```

### map_select_clear

清空指定槽位的节点。

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `slotIndex` | int | 是 | 槽位索引 (0-5) |

### map_select_confirm

确认当前地图节点编排并继续前进。所有6个槽位都必须已填充。

**这个工具同时负责两件事：**
1. **开始路线** — 首次填充完所有槽位后确认，开始走路线
2. **下一个节点** — 完成一个节点（事件/战斗/建筑）后再次确认，前进到路线上下一个节点

也就是说，走完一层的过程中需要**多次调用** `map_select_confirm`。

**Python：**
```python
# 填充后确认
state = g.call("map_select_state")
if state['canContinue']:
    g.call("map_select_confirm")

# 完成第一个节点后，游戏回到选点界面
# 再次确认就到下一个节点
g.call("map_select_confirm")
```

---

## 页面状态机

游戏状态机转换图：

```
MAIN_MENU ──enter_game──→ HUB ──start_new_game──→ LOBBY ──start_run──→ MAP ──load_scene──→ FIGHT
                                ↑                       │                  ↓
                                └─── 返回小屋 ────────────┘              claim_rewards → 继续战斗/事件
```

**get_scene_state 可检测的页面值：**

| 页面 | 含义 | 可执行的操作 |
|------|------|-------------|
| `MAIN_MENU` | 标题画面 | `enter_game` |
| `MODE_SELECT` | 模式选择 | `start_new_game` |
| `LOBBY` | 职业选择大厅 | `set_lobby_state`(Lobby模块), `start_run` |
| `FIGHT` | 战斗中 | 战斗模块的所有工具, `claim_rewards` |
| `MAP` | 地图/跑局中 | `load_scene`(基座工具) |
| `HUB` | 小屋/中枢 | `start_new_game` |
| `UNKNOWN` | 未识别 | 先截图确认 |

---

## 工具详情

### get_scene_state

**最关键的工具。** 所有操作前必须先调用它确认当前页面。

**返回字段：**
| 字段 | 类型 | 说明 |
|------|------|------|
| `page` | string | 当前页面标识 |
| `inRun` | bool | 是否在跑局中 |
| `inFight` | bool | 是否在战斗中 |
| `fightType` | string | 战斗类型（Player/Enemy） |
| `isFake` | bool | 是否是假战斗 |
| `fightPlayer` | object | `{hp, maxHp, power, shield}` |
| `player` | object | `{hp, maxHp, san, maxSan, money}` |
| `modals` | bool | 是否有遮挡弹窗 |
| `transitioning` | bool | 是否有转场动画 |
| `activeUI` | string/null | **最上层的活动弹窗/模态** — 直接告诉你当前该用什么工具，无需查场景树。`null` = 无活跃弹窗 |
| `activeUIs` | string[] | 所有活跃的 UI 弹窗（最上层排第一个） |
| `overlays` | string[] | 活动覆盖层（SettingUI, BackpackUI） |
| `level` | int | 当前层数（跑局中） |

**activeUI 快速参考：**
| 值 | 下一步该做什么 |
|----|--------------|
| `BattleRewardsUI` | `claim_rewards` |
| `CardChoiceUI` | `pick_card_reward` 或 `skip_card_reward` |
| `BlessingChoiceGenerator` | `pick_blessing_reward` 或 `skip_blessing_reward` |
| `DeckUI` | `get_deck_selection` → `select_deck_cards` |
| `BreaksUI` | 用 `scan_ui` 找到继续/离开按钮，再用 `click_ui` 点 |
| `EventUI` | `event_choose_option` 或 `event_advance_dialogue` ⚠️ 部分事件有多阶段选项，选完一次后重新检查 `activeUI`，如果 EventUI 还在则继续选 |
| `ShopUI` | 使用 `get_shop_state`, `shop_buy`, `shop_sell`, `shop_refresh` |
| `SafeBoxUI` | 使用 `safebox_*` 系列工具 |
| `MapSelectUI` | `map_select_state` → `map_select_assign` → `map_select_confirm` |
| `OutDeckUI` | `get_outdeck_state` → `outdeck_move_card` / `outdeck_decompose` |
| `SettingUI` / `BackpackUI` / 其他任意UI | 用 `scan_ui` 列出所有可交互按钮，再用 `click_ui` 点击目标按钮 |

> 💡 `scan_ui` + `click_ui` 是 **通用导航工具**。不知道如何进入/离开某个页面、不知道该点什么时，先调 `scan_ui` 扫描当前所有可交互按钮，再用 `click_ui` 按索引点击。无需任何硬编码。

**Python：**
```python
state = g.call("get_scene_state")
print(f"页面: {state['page']}")
if state.get('modals'):
    print("有弹窗遮挡")
if state['page'] == 'FIGHT':
    print(f"战斗阶段: {state.get('fightType')}")
    print(f"玩家 HP: {state['fightPlayer']['hp']}/{state['fightPlayer']['maxHp']}")
```

### enter_game

从主菜单进入游戏小屋。如果已经在小屋或跑局中，立刻返回成功。

| 返回字段 | 说明 |
|----------|------|
| `result` | `success` / `already_in_hub` / `already_in_run` / `timeout` / `unknown_state` |
| `page` | `HUB` / `MAIN_MENU` |

**Python：**
```python
r = g.call("enter_game")
if r['result'] == 'success':
    print("已进入小屋")
elif r['result'] == 'already_in_hub':
    print("已经在屋里了")
```

### start_new_game

从当前状态打开模式选择，创建或续档一个游戏存档。

| 参数 | 类型 | 必填 | 默认 | 说明 |
|------|------|------|------|------|
| `mode` | string | 是 | — | `Normal`, `Sublimation`, `Slot`, `Teach`, `Story` |
| `useExistingSave` | bool | 否 | false | 是否使用已有存档继续 |

**Python：**
```python
# 开新 Normal 档
r = g.call("start_new_game", {"mode": "Normal"})
if r['result'] == 'success':
    print("进入大厅")

# 继续 Sublimation 存档
r = g.call("start_new_game", {
    "mode": "Sublimation",
    "useExistingSave": True
})
```

### start_run

在大厅中点击"启程"按钮，完成最终初始化（属性加点、卡组构建），进入地图。

**返回：**
| 字段 | 说明 |
|------|------|
| `result` | `success` / `already_in_run` / `error` / `timeout` |
| `page` | `MAP`（成功时） |
| `level` | 起始层数 |

**Python：**
```python
r = g.call("start_run")
if r['result'] == 'success':
    print(f"启程成功，起始层 {r.get('level', '?')}")
elif r['result'] == 'timeout':
    state = g.call("get_scene_state")
    print(f"超时，当前页面: {state['page']}")
```

### check_mode_saves

检查指定模式的存档详情。不传 mode 则返回所有模式。

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `mode` | string | 否 | 游戏模式过滤 |

**Python：**
```python
saves = g.call("check_mode_saves", {"mode": "Normal"})
print(f"有效存档: {saves['validSaves']}")
for s in saves['saves']:
    print(f"  {s['name']} — Lv.{s['level']}, {s.get('career', '?')}")
```

### list_game_modes

列出所有可用的游戏模式（包括 Mod 注册的额外模式）及其存档情况。

**Python：**
```python
modes = g.call("list_game_modes")
for m in modes['modes']:
    print(f"{m['mode']}: {m['saveCount']} 存档")
```

---

## 典型工作流：全流程启动到战斗

```python
# === 阶段 1：确认位置 ===
state = g.call("get_scene_state")
print(f"起点: {state['page']}")

# === 阶段 2：确保在小屋 ===
if state['page'] == 'MAIN_MENU':
    g.call("enter_game")

# === 阶段 3：开新游戏 → 大厅 ===
if state['page'] in ('HUB', 'MAIN_MENU'):
    g.call("start_new_game", {"mode": "Normal"})

# === 阶段 4：配置大厅（可选） ===
g.call("set_lobby_state", {
    "careerId": "Career_1",
    "partnerId": "Partner_10001"
})

# === 阶段 5：启程 ===
g.call("start_run")

# === 阶段 6：跳转假战斗 ===
g.call("load_scene", {"type": "fakefight"})  # 基座工具
```

## 典型工作流：跳转指定战斗

```python
# 如果已在跑局中，直接跳转到指定战斗
g.call("load_scene", {"type": "fight", "id": "boss"})   # 首领战
g.call("load_scene", {"type": "fight", "id": "elite"})  # 精英
g.call("load_scene", {"type": "fight", "id": "common"}) # 普通
g.call("load_scene", {"type": "event"})                 # 事件
g.call("load_scene", {"type": "fakefight"})             # 假战斗
```

## 最佳实践

1. **永远先调 get_scene_state** — 这是所有操作的起点。如果页面是 FIGHT，用 Combat 模块工具；如果是 LOBBY，用 Lobby 模块
2. **处理返回状态** — 导航工具有 success / timeout / already_xx 等状态。timeout 后调用 get_scene_state 诊断
3. **start_run 的回退机制** — 如果 `GameEntryUI.StartGame()` 失败，工具自动尝试 `PlayerManager.StartGame()`。变更细节在 `changes` 字段中
4. **假战斗 vs 真战斗** — `load_scene type=fakefight` 快速进入测试战斗；`type=fight` 消耗地图进度。优先用 fakefight 做卡牌测试
5. **存档管理** — 测试前用 `check_mode_saves` 确认已有存档，避免意外覆盖
6. **地图节点编排一定要用 `nodeId` 而非 `index`** — `index` 是动态遍历序号，每次放置后变化。`nodeId`（如 "shop"、"Breaks"、"level_10006"）是配置表稳定 ID，不变不重复
7. **推荐用批量 `mappings` 一次填完** — 一次性传递所有 `{slotIndex, nodeId}`，工具内部按顺序处理，只同步一次。比逐次调用 `map_select_assign` 更高效可靠
