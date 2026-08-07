---
name: witch-mod-mcp-gameflow
description: "WitchModMCP game flow tools: navigate the game state machine from main menu through hub, lobby, map, and fights. Use when the user wants to enter the game from the main menu, start a new game with a mode selection, begin a run from the lobby, or navigate the map passage (node selection/confirm). Triggers: gameflow, enter_game, start_new_game, start_run, map_select, claim_rewards, 开始游戏, 启程, 进入游戏, 奖励, 地图选点, 节点编排."
---

# Gameflow Module

Drive the game's state machine across major scenes: main menu → hub → mode select → lobby → map → fight.

## Tools

| Tool | Params | Returns | Notes |
|------|--------|---------|-------|
| `enter_game` | — | `{result, message, page}` | Main menu → Hub. Polls for completion (up to 15s). |
| `start_new_game` | `{mode, useExistingSave?}` | `{result, mode, usedExisting, page, message}` | Select mode → enter lobby. Polls (up to 15s). |
| `start_run` | — | `{result, message, page, level?}` | Lobby → Map. Polls (up to 20s). Has fallback. |
| `map_select_state` | — | `{selectableNodes, slots, canContinue, result}` | Node selection UI state. Only available during passage/road events. |
| `map_select_assign` | `{slotIndex, nodeId}` | `{result, placed, placedCount, movedCount, nullActivatedCount, message}` | Place a selectable node into a slot (single per call). |
| `map_select_clear` | `{slotIndex}` | `{result, message}` | Remove node from a slot. |
| `map_select_confirm` | — | `{result, message}` | Confirm node arrangement AND advance. Called after every node—used both for starting the passage AND for moving to the next node. |
| `claim_rewards` | — | `{claimed?, actions: []}` | ⚠️ 将未领取奖励全部转化为金钱的快捷按钮。见下方"正确领取流程" |
| `load_scene` | `{type, id?}` | `{type, id, result}` | **🚨 仅开发/调试用！会绕过正常流程节点，可能破坏存档流程导致坏档。正常游玩绝对不要调用！** |

---

### enter_game

From the **main menu**, click "Start Game" and wait for the hub (house scene) to load. If already in hub or a run, returns immediately.

**Return:**
| Field | Type | Description |
|-------|------|-------------|
| `result` | string | `success` / `already_in_hub` / `already_in_run` / `timeout` / `unknown_state` |
| `message` | string | Human-readable status |
| `page` | string | `HUB` / `MAIN_MENU` / `UNKNOWN` |

**Python:**
```python
result = g.call("enter_game")
if result['result'] == 'success':
    print("Entered hub")
```

### start_new_game

Open the mode selection dialog, choose a game mode, and navigate to the career selection hall (lobby).

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `mode` | string | Yes | — | Game mode: `Normal`, `Sublimation`, `Slot`, `Teach`, `Story` |
| `useExistingSave` | bool | No | false | Whether to continue an existing save for this mode |

**Python:**
```python
# New Normal run
result = g.call("start_new_game", {"mode": "Normal"})

# Continue existing Sublimation save
result = g.call("start_new_game", {
    "mode": "Sublimation",
    "useExistingSave": True
})
```

### start_run

In the career selection hall (after `start_new_game`), click the "Start" button to begin the run. Moves from lobby to the map screen.

### load_scene

> **🚨 警告：仅开发/调试用！此工具直接跳转到指定场景，绕过所有正常流程节点，可能破坏存档数据，导致坏档！**
>
> **正常游玩流程绝对不要调用此工具。必须通过 `map_select_assign` + `map_select_confirm` 自然推进。**
>
> **确认你真的需要使用它，再使用。**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `type` | string | Yes | `event` / `fight` / `fakefight` |
| `id` | string | No | Specific ID, or for fights: `common` / `elite` / `boss` |

```python
# 🔧 仅开发测试用：
# result = g.call("load_scene", {"type": "fight", "id": "boss"})
```

### claim_rewards

> **⚠️ 这是一个"全部转化为金钱"的快捷按钮，不是正常的奖励领取流程。**
>
> 正常流程是：
> 1. 战斗胜利后检查 `activeUI`：
> 2. 如果有 `CardChoiceUI` → 用 `pick_card_reward` 选一张卡
> 3. 如果有 `BlessingChoiceGenerator` → 用 `pick_blessing_reward` 选一个祝福
> 4. 所有奖励选完后，`claim_rewards` 关闭界面

Clicks the "Close" button on `BattleRewardsUI`. Unconverted rewards auto-convert to gold. Also closes a subsequent `CardChoiceUI` if it appears (skipping it). ⚠️ It does **NOT** handle `BlessingChoiceGenerator` — handle blessings with `pick_blessing_reward` / `skip_blessing_reward` first.

**正确领取流程：**
```python
# 1. 检查是否有卡牌奖励
scene = g.call("get_scene_state")
if scene.get('activeUI') == 'CardChoiceUI':
    cards = g.call("get_deck_selection")  # 可选
    g.call("pick_card_reward", {"index": 0})  # 选第1张

# 2. 检查是否有祝福奖励
scene = g.call("get_scene_state")
if scene.get('activeUI') == 'BlessingChoiceGenerator':
    g.call("pick_blessing_reward", {"index": 0})  # 选第1个

# 3. 关闭奖励界面
g.call("claim_rewards")
```

### map_select_state

Get the full state of the map node selection UI (MapSelectUI). Called during passage/road events when the game shows tile-selectable nodes.

**Important: `nodeId` is the stable config-table ID.** Use it for `map_select_assign` — `selectableNodes` has no `index` field (`index` only exists on `slots`), so always reference nodes by `nodeId`.

### map_select_assign

Place one selectable node into a slot. Single placement per call.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `slotIndex` | int | Yes | Slot index (0-5) |
| `nodeId` | string | Yes | Stable node ID from `selectableNodes[].nodeId` |

### map_select_confirm

Confirm the current node arrangement and advance to the next node. **This is the same tool for both starting a passage and moving to the next node** — after each node is completed, call `map_select_confirm` again to progress.

```python
# Confirm to advance
state = g.call("map_select_state")
if state['canContinue']:
    g.call("map_select_confirm")
```

## Common workflows

### 正常游戏流程（从主菜单到战斗）
```python
# 1. 进入游戏
g.call("enter_game")
# 2. 开始新游戏 → 大厅
g.call("start_new_game", {"mode": "Normal"})
# 3. 配置大厅（可选）
g.call("set_lobby_state", {"careerId": "Career_1", ...})
# 4. 启程
g.call("start_run")
# 5. 编排本层节点
state = g.call("map_select_state")
# — 用 map_select_assign 填充6个槽位 —
# 6. 确认并前进
g.call("map_select_confirm")
```

### 一层内的正常推进
```python
# 每完成一个节点(战斗/事件/建筑)，回到 MapSelectUI
# → 再次调用 map_select_confirm 进入下一个节点
# → 直到最后一个节点完成后进入下一层

state = g.call("map_select_state")
if state.get('canContinue'):
    g.call("map_select_confirm")
```

### 战斗后正确领取奖励
```python
# 1. 胜利后检查 activeUI
scene = g.call("get_scene_state")

# 2. 处理卡牌奖励
if scene.get('activeUI') == 'CardChoiceUI':
    g.call("pick_card_reward", {"index": 0})

# 3. 处理祝福奖励
scene = g.call("get_scene_state")
if scene.get('activeUI') == 'BlessingChoiceGenerator':
    g.call("pick_blessing_reward", {"index": 0})

# 4. 关闭奖励界面
g.call("claim_rewards")
```

### 节点编排
```python
state = g.call("map_select_state")
# 逐个放置节点
g.call("map_select_assign", {"slotIndex": 1, "nodeId": state['selectableNodes'][0]['nodeId']})
g.call("map_select_assign", {"slotIndex": 2, "nodeId": state['selectableNodes'][1]['nodeId']})
# ...
# 确认前进
g.call("map_select_confirm")
# 完成节点后回到选择 → 再次 confirm 到下一个节点
```

## Best practices

1. All navigation tools poll for completion with timeouts. If a timeout occurs, call `get_scene_state` (Meta module) to diagnose the current page.
2. `start_run` has an automatic fallback.
3. **`claim_rewards` 只是快捷转化按钮。** 正常流程应该先用 `pick_card_reward` / `pick_blessing_reward` 领取奖励，再用 `claim_rewards` 关闭。
4. `load_scene` 是开发调试工具，正常游玩不要使用。正常前进请用 `map_select_confirm`。
5. `map_select_confirm` is used for both **starting a passage** AND **moving to the next node** after completing one.
6. After `load_scene` with `type=fight`, wait briefly before calling `get_fight_state`.
7. `map_select_assign` only places one node per call.
