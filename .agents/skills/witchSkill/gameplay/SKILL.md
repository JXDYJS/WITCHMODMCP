---
name: witch-mod-mcp-gameplay
description: "正常游玩游戏 Witch（魔女:终末旅途）的完整流程指南。不包含开发/调试工具。适合需要让 AI 代理替你跑局、打怪、管理卡组的场景。Triggers: gameplay, 游玩, 跑局, 正常流程, 怎么玩, 前进, 下一节点, 打怪, 卡组管理"
---

# Gameplay Guide — 正常游玩流程

**这个技能只包含正常游玩的工具和流程，不包含开发/调试工具（`load_scene`, `give_item`, `set_fight_entity` 等）。**

## 完整生命周期

```
MAIN_MENU
   │  enter_game()
   ▼
HUB (小屋)
   │  start_new_game({"mode": "Normal"})
   ▼
LOBBY (职业选择大厅)
   │  set_lobby_state() 配置职业/随从/属性
   │  start_run()
   ▼
MAP → MapSelectUI (编排本层节点)
   │  map_select_assign() 放置节点 → map_select_confirm() 前进
   │
   ├──→ EventUI (事件)       → event_choose_option() / 完成后 confirm
   ├──→ FIGHT (战斗)          → 战斗模块工具 → 领奖 → confirm
   ├──→ ShopUI (商店)         → shop_buy/sell → 关闭 → confirm
   ├──→ OutDeckUI (牌组)      → outdeck_move_card → 关闭 → confirm
   └──→ BreaksUI (休息处)     → click_ui → confirm

   (每完成一个节点，回到 MapSelectUI → 再次 confirm 进入下一个)
   (最后一个节点完成后，进入下一层 → 新 MapSelectUI)
```

---

## 阶段一：进入游戏

```python
from witch_mcp import WitchMcp
g = WitchMcp()

# 1. 检测当前页面
state = g.call("get_scene_state")
page = state['page']  # MAIN_MENU / HUB / LOBBY / MAP / FIGHT

# 2. 如果在主菜单，进入游戏
if page == 'MAIN_MENU':
    g.call("enter_game")
    state = g.call("get_scene_state")

# 3. 如果在小屋，开始新游戏
if state['page'] == 'HUB':
    g.call("start_new_game", {"mode": "Normal"})
    state = g.call("get_scene_state")

# 4. 如果在职业大厅，配置并启程
if state['page'] == 'LOBBY':
    lobby = g.call("get_lobby_state")
    # 可选：修改配置
    g.call("start_run")
```

## 阶段二：每层节点编排

进入 MAP 后，游戏弹出 `MapSelectUI`，让你编排本层的 6 个节点。

```python
# 1. 查看可选节点和当前槽位
state = g.call("map_select_state")

# 2. 将可选节点放到空槽位
# selectableNodes 是"手牌"，slots 是需要填充的槽位
for node in state['selectableNodes']:
    # 找到第一个空槽位
    empty = [s for s in state['slots'] if not s['filled']]
    if empty:
        g.call("map_select_assign", {
            "slotIndex": empty[0]['index'],
            "nodeId": node['nodeId']
        })
        # 重新读取状态
        state = g.call("map_select_state")

# 3. 所有槽位填满后，确认前进
if state['canContinue']:
    g.call("map_select_confirm")
```

## 阶段三：处理节点

每确认一次，进入一个节点。根据节点类型用不同工具：

### 战斗节点
```python
# 战斗会自然展开，用战斗模块工具
fight = g.call("get_fight_state")
# ... 出牌、用技能 ...

# 胜利后检查奖励
scene = g.call("get_scene_state")
if scene.get('activeUI') == 'CardChoiceUI':
    g.call("pick_card_reward", {"index": 0})
scene = g.call("get_scene_state")
if scene.get('activeUI') == 'BlessingChoiceGenerator':
    g.call("pick_blessing_reward", {"index": 0})
g.call("claim_rewards")
```

### 事件节点
```python
# 查看事件选项
scene = g.call("get_scene_state")
if scene.get('activeUI') == 'EventUI':
    # 读取事件详情和所有选项
    event = g.call("get_event_state")
    print(f"事件: {event['title']}")
    for opt in event['options']:
        print(f"  {opt['index']}. {opt['text']} (可用: {opt['interactable']})")
    # 选第一个选项
    g.call("event_choose_option", {"index": 1})
    # ⚠️ 部分事件有多阶段，检查 EventUI 是否还在
    scene = g.call("get_scene_state")
    if scene.get('activeUI') == 'EventUI':
        g.call("event_choose_option", {"index": 1})
    # 没有更多选项时关闭
    g.call("event_advance_dialogue")
```

### 商店节点
```python
shop = g.call("get_shop_state")
# shop_buy / shop_sell 通过 instanceId 操作
```

### 牌组管理节点
```python
deck = g.call("get_outdeck_state")
# outdeck_move_card 通过 instanceId 移动
# outdeck_decompose 分解卡牌
```

### 休息处
```python
# scan_ui 找到"继续"按钮，click_ui 点击
```

## 阶段四：推进到下一节点

**每完成一个节点，游戏回到 MapSelectUI。再次调用 `map_select_confirm` 即可进入下一个已编排的节点。**

```python
# 完成一个节点后
state = g.call("get_scene_state")
if state.get('activeUI') == 'MapSelectUI':
    g.call("map_select_confirm")
# → 自动进入下一个节点
```

> ⚠️ **不要用 `load_scene`！** 那是开发调试工具，正常游玩必须通过 `map_select_confirm` 推进。

## 阶段五：下一层

当本层最后一个节点完成后，游戏自动进入下一层，再次弹出 `MapSelectUI`。重复阶段二到四。

## 完整循环示例

```python
# 战斗循环
while True:
    scene = g.call("get_scene_state")
    page = scene.get('page')

    if page == 'MAP' and scene.get('activeUI') == 'MapSelectUI':
        # 编排/前进
        g.call("map_select_confirm")

    elif scene.get('activeUI') == 'EventUI':
        g.call("event_choose_option", {"index": 1})
        scene = g.call("get_scene_state")
        if scene.get('activeUI') == 'EventUI':
            g.call("event_choose_option", {"index": 1})
        else:
            g.call("event_advance_dialogue")

    elif scene.get('activeUI') == 'OutDeckUI':
        deck = g.call("get_outdeck_state")
        # ... 管理卡组 ...
        # 点 ExitButton 关闭

    elif page == 'FIGHT':
        fight = g.call("get_fight_state")
        # ... 战斗 ...

    elif scene.get('activeUI') == 'BattleRewardsUI':
        # 领奖
        pass

    elif scene.get('activeUI') == 'CardChoiceUI':
        g.call("pick_card_reward", {"index": 0})

    elif scene.get('activeUI') == 'BlessingChoiceGenerator':
        g.call("pick_blessing_reward", {"index": 0})
```

## 关键原则

1. **不要用 `load_scene`** — 正常流程通过 `map_select_confirm` 推进
2. **不要跳过奖励** — 先用 `pick_card_reward` / `pick_blessing_reward`，再用 `claim_rewards`
3. **事件可能有多阶段** — 选完后重新检查 EventUI
4. **释放技能后检查选牌模态** — 看 `get_fight_state.inSelectionMode`
5. **推进就是反复 confirm** — 每完成一个节点 → 回到 MapSelectUI → 再次 confirm
