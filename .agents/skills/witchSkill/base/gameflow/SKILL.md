---
name: witch-mod-mcp-gameflow
description: "WitchModMCP game flow tools: navigate the game state machine from main menu through hub, lobby, map, and fights. Use when the user wants to enter the game from the main menu, start a new game with a mode selection, begin a run from the lobby, jump to a specific scene (event/fight/fakefight), or claim battle rewards after a fight. Triggers: gameflow, enter_game, start_new_game, start_run, load_scene, claim_rewards, 开始游戏, 启程, 跳转, 进入游戏, 奖励."
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
| `map_select_assign` | `{slotIndex, nodeId}` | `{result, placed, message}` | Place a selectable node into a slot (single per call). |
| `map_select_clear` | `{slotIndex}` | `{result, message}` | Remove node from a slot. |
| `map_select_confirm` | — | `{result, message}` | Confirm node arrangement AND advance. Called after every node—used both for starting the passage AND for moving to the next node. |
| `load_scene` | `{type, id?}` | `{type, id, result}` | Jump to event/fight/fakefight scene. |
| `claim_rewards` | — | `{claimed?, actions: []}` | Close battle rewards UI by clicking the confirm button (not calling Close() directly). |

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

**Return:**
| Field | Type | Description |
|-------|------|-------------|
| `result` | string | `success` / `already_in_lobby` / `error` / `timeout` |
| `mode` | string | The requested mode |
| `usedExisting` | bool | Whether an existing save was loaded |
| `page` | string | `LOBBY` on success |
| `message` | string | Status description |

**Python:**
```python
# New Normal run
result = g.call("start_new_game", {"mode": "Normal"})
if result['result'] == 'success':
    print("In lobby, ready to configure")

# Continue existing Sublimation save
result = g.call("start_new_game", {
    "mode": "Sublimation",
    "useExistingSave": True
})
```

### start_run

In the career selection hall (after `start_new_game`), click the "Start" button to begin the run. Moves from lobby to the map screen. Has a fallback mechanism if `GameEntryUI.StartGame()` fails.

**Return:**
| Field | Type | Description |
|-------|------|-------------|
| `result` | string | `success` / `already_in_run` / `error` / `timeout` |
| `message` | string | Status description |
| `page` | string | `MAP` on success |
| `level` | int | Starting level (on success) |

**Python:**
```python
result = g.call("start_run")
if result['result'] == 'success':
    print(f"Run started at level {result.get('level', '?')}")
```

### load_scene

Jump to a specific scene from the current run context (map screen or hub). Useful for quick-testing specific encounters.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `type` | string | Yes | `event` / `fight` / `fakefight` |
| `id` | string | No | Specific ID, or for fights: `common` / `elite` / `boss` |

**Python:**
```python
# Jump to a boss fight
result = g.call("load_scene", {"type": "fight", "id": "boss"})

# Jump to a random event
result = g.call("load_scene", {"type": "event"})

# Jump to a fake/practice fight
result = g.call("load_scene", {"type": "fakefight"})
```

### claim_rewards

After a fight victory, clicks the "Close" button on `BattleRewardsUI` (unconverted rewards auto-convert to gold), then closes any subsequent `CardChoiceUI` and `BlessingChoiceGenerator` dialogs. After calling this, use `load_scene` to proceed to the next encounter.

**Python:**
```python
# After winning a fight
result = g.call("claim_rewards")
print(result['actions'])
```

### map_select_state

Get the full state of the map node selection UI (MapSelectUI). Called during passage/road events when the game shows tile-selectable nodes.

**Return:**
| Field | Type | Description |
|-------|------|-------------|
| `selectableNodes` | array | `[{nodeId, id, type, note, name}]` — available nodes ("hand") |
| `slots` | array | `[{index, name, filled, node?}]` — 6 slots (0=start, 1-4=mid, 5=end) |
| `canContinue` | bool | All slots filled? |
| `result` | string | `"success"` |

**Important: `nodeId` is the stable config-table ID.** Use it for `map_select_assign` — never use `index` which changes after each placement.

### map_select_assign

Place one selectable node into a slot. Single placement per call.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `slotIndex` | int | Yes* | Slot index (0-5) |
| `nodeId` | string | Yes* | Stable node ID from `selectableNodes[].nodeId` |

*Must pass both `slotIndex` and `nodeId`.

**Python:**
```python
g.call("map_select_assign", {"slotIndex": 1, "nodeId": "shop"})
```

### map_select_clear

Remove a node from a slot, returning it to the hand.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `slotIndex` | int | Yes | Slot index (0-5) |

### map_select_confirm

Confirm the current node arrangement and advance to the next node. **This is the same tool for both starting a passage and moving to the next node** — after each node is completed (event/fight/building), call `map_select_confirm` again to progress.

**Python:**
```python
# After filling all slots, confirm to start the passage
state = g.call("map_select_state")
if state['canContinue']:
    g.call("map_select_confirm")
```

## Common workflows

### Full run setup
```python
# 1. Enter game
g.call("enter_game")
# 2. Start new game → lobby
g.call("start_new_game", {"mode": "Normal"})
# 3. Configure lobby (optional — see Lobby module)
g.call("set_lobby_state", {"careerId": "Career_1", ...})
# 4. Start run
g.call("start_run")
# 5. Jump to boss fight
g.call("load_scene", {"type": "fight", "id": "boss"})
```

### Fight loop
```python
g.call("load_scene", {"type": "fight", "id": "common"})
# ... fight logic (see Combat module) ...
g.call("claim_rewards")
g.call("load_scene", {"type": "fight", "id": "boss"})
```

### Passage navigation (node selection)
```python
# After entering a passage, game shows MapSelectUI
state = g.call("map_select_state")
# Place nodes one at a time
g.call("map_select_assign", {"slotIndex": 1, "nodeId": state['selectableNodes'][0]['nodeId']})
g.call("map_select_assign", {"slotIndex": 2, "nodeId": state['selectableNodes'][1]['nodeId']})
# ...
# Confirm to start traversal
g.call("map_select_confirm")
# After completing first node (event/fight), game returns to selection
# → call map_select_confirm again to go to the next node
```

## Best practices

1. All navigation tools poll for completion with timeouts. If a timeout occurs, call `get_scene_state` (Meta module) to diagnose the current page.
2. `start_run` has an automatic fallback that tries `PlayerManager.StartGame()` if `GameEntryUI.StartGame()` fails. Results are transparent in the `changes` field.
3. `claim_rewards` is idempotent — calling it multiple times is safe but subsequent calls may report no actions taken.
4. After `load_scene` with `type=fight`, wait briefly before calling `get_fight_state` (Combat module) — the fight may take a frame to initialize.
5. For fake fights, the game may still show transition UI; `get_fight_state` will report `isFake=true`.
6. `map_select_confirm` is used for both **starting a passage** AND **moving to the next node** after completing one. The same tool, called repeatedly as the player progresses.
7. `map_select_assign` only places one node per call — if the game rejects placement (e.g. shop when no ShopUI exists), the error will indicate why.
