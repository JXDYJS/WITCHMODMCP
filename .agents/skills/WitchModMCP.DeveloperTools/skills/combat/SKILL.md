---
name: witch-mod-mcp-developertools-combat
description: "DeveloperTools combat tools: fight state snapshot, card play, turn end, card pile control, entity attribute modification, reward claiming. Use when the user wants to read the full battle state (player/enemy HP, hand, draw/discard piles, buffs, intents), play a card, end the current turn, manipulate card piles, modify entity stats, or claim battle rewards. Triggers: combat, 战斗, fight, play_card, end_turn, get_fight_state, set_card_pile, set_fight_entity, claim_rewards, 出牌, 打牌, 回合, 奖励."
---

# Combat 模块 — 战斗操控

完整的战斗读写流程。所有工具需要游戏处于战斗页面（`get_scene_state` 返回 `page=FIGHT`）。

## 工具总览

| 工具 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `get_fight_state` | — | 完整战斗快照 | 读取当前战斗全部状态 |
| `play_card` | `{cardId?/index?, targetIndex?, choices?}` | `{result, cardId, handBefore, handAfter, targetHpBefore?, targetHpAfter?}` | 打出手牌 |
| `end_turn` | — | `{result, message, phase?}` | 结束玩家回合 |
| `set_card_pile` | `{pile, action, cards?, indices?, shuffle?}` | `{result, changes}` | 控制卡牌堆内容 |
| `set_fight_entity` | `{target, hp?, maxHp?, shield?, ...}` | `{result, changes}` | 修改实体属性 |
| `claim_rewards` | — | `{claimed?, actions}` | 领取战斗奖励 |

---

## 工具详情

### get_fight_state

读取当前战斗的完整快照。**每次战斗操作前调用**。

**返回字段：**
| 字段 | 类型 | 说明 |
|------|------|------|
| `inFight` | bool | 是否有战斗 |
| `phase` | string | `Player` / `Enemy` |
| `isFake` | bool | 是否是假战斗 |
| `turn` | int | 当前层数 |
| `player` | object | `{hp, maxHp, shield, power, maxPower, isDead, buffs}` |
| `enemies` | array | `[{index, id, name, hp, maxHp, shield, isDead, attack, defend, buffs, intents}]` |
| `hand` | array | `[{index, cardId, instanceId, cost}]` — 手牌 |
| `drawPile` | object | `{count, top5}` — 抽牌堆 |
| `discardPile` | object | `{count, last5}` — 弃牌堆 |
| `exhaustPile` | object | `{count, cards}` — 消耗堆 |
| `masterDeckCount` | int | 主卡组总张数 |
| `inSelectionMode` | bool | 是否有选牌模态 |
| `selectedCardCount` | int | 已选牌数量 |

**Python：**
```python
state = g.call("get_fight_state")
if not state.get('inFight'):
    print("不在战斗中")
    exit()

print(f"阶段: {state['phase']}, HP: {state['player']['hp']}/{state['player']['maxHp']}")
for e in state['enemies']:
    print(f"  敌人 {e['index']}: {e['id']} HP={e['hp']} 盾={e['shield']}")
for c in state['hand']:
    print(f"  手牌[{c['index']}]: {c['cardId']} 费用={c['cost']}")
```

### play_card

打出手牌中的一张卡。支持按 index 或 cardId 定位。攻击卡可指定 targetIndex。

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `cardId` | string | 否* | 卡牌 Config ID |
| `index` | int | 否* | 手牌位置(0-based) |
| `targetIndex` | int | 否 | 攻击目标敌人索引 |
| `choices` | object | 否 | 出牌后的模态处理 |

*`cardId` 与 `index` 二选一

**choices 参数：**
| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `discardIndices` | int[] | — | 需要弃掉的手牌索引 |
| `selectIndices` | int[] | — | 需要选择的手牌索引 |
| `autoConfirm` | bool | true | 出现选牌界面时自动确认 |
| `autoSelectFirst` | bool | false | 自动选第一张可用卡 |

**Python：**
```python
# 按索引出牌，攻击敌人 0
r = g.call("play_card", {"index": 0, "targetIndex": 0})
print(f"出牌结果: {r['result']}")
print(f"手牌: {r['handBefore']} → {r['handAfter']}")

# 按 cardId 出牌，自动处理弃牌
r = g.call("play_card", {
    "cardId": "card_strike_1",
    "choices": {"discardIndices": [2, 3]}
})

# 出牌并自动选择（适合"发现"类卡牌）
r = g.call("play_card", {
    "index": 1,
    "choices": {"autoSelectFirst": True}
})
```

### end_turn

强制结束玩家回合，触发敌方行动。仅在 `phase=Player` 时有效。

**Python：**
```python
r = g.call("end_turn")
if r['result'] == 'success':
    print("回合结束，等待敌方行动")
```

### set_card_pile

低层卡牌堆控制。支持 hand / draw / discard / exhaust 四个堆。

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `pile` | string | 是 | `hand` / `draw` / `discard` / `exhaust` |
| `action` | string | 是 | `add` / `remove` / `clear` / `set` |
| `cards` | string[] | 否 | CardId 列表 |
| `indices` | int[] | 否 | 手牌索引（仅 hand 移除时使用） |
| `shuffle` | bool | 否 | add 后是否洗牌（仅 draw） |

**各堆支持的操作：**
| 堆 | add | remove | clear | set |
|----|-----|--------|-------|-----|
| hand | 抽牌到手上 | 按 index/cardId 移除 | 清空手牌 | 替换手牌 |
| draw | 加到抽牌堆 | 按 cardId 移除 | 清空抽牌堆 | 替换抽牌堆 |
| discard | 加到弃牌堆 | 按 cardId 移除 | 清空弃牌堆 | 替换弃牌堆 |
| exhaust | 消耗（从游戏移除） | 恢复（回到抽牌堆） | 全部回到抽牌堆 | — |

**Python：**
```python
# 放 3 张特定卡到抽牌堆顶部
g.call("set_card_pile", {
    "pile": "draw", "action": "add",
    "cards": ["card_1", "card_2", "card_strike_1"]
})

# 清空手牌
g.call("set_card_pile", {"pile": "hand", "action": "clear"})

# 从弃牌堆移除某张卡
g.call("set_card_pile", {
    "pile": "discard", "action": "remove",
    "cards": ["card_bad_1"]
})

# 消耗堆全部恢复
g.call("set_card_pile", {"pile": "exhaust", "action": "clear"})
```

### set_fight_entity

修改战斗中实体（玩家/敌人）的属性。

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `target` | string | 是 | `"player"` 或敌人索引（如 `"0"`） |
| `hp` | int | 否 | 设置 HP |
| `maxHp` | int | 否 | 设置最大 HP |
| `shield` | int | 否 | 设置护盾值 |
| `power` | int | 否 | 设置能量（仅玩家） |
| `maxPower` | int | 否 | 设置最大能量（仅玩家） |
| `addBuffs` | array | 否 | `[{id: string, level: int}]` |
| `removeBuffs` | string[] | 否 | 要移除的 Buff ID 列表 |
| `clearBuffs` | bool | 否 | 清空所有 Buff |

**Python：**
```python
# 玩家满血 + 加再生 Buff
g.call("set_fight_entity", {
    "target": "player",
    "hp": 80,
    "maxHp": 80,
    "addBuffs": [{"id": "buff_regenerate", "level": 2}]
})

# 敌人 0 变 1 血
g.call("set_fight_entity", {"target": "0", "hp": 1})

# 移除敌人的护盾
g.call("set_fight_entity", {"target": "0", "shield": 0})

# 清空敌人所有 Buff
g.call("set_fight_entity", {"target": "0", "clearBuffs": True})
```

### claim_rewards

战斗胜利后领取奖励。关闭 `BattleRewardsUI`（未领取的奖励自动转化为金钱），然后关闭 `CardChoiceUI` 和 `BlessingChoiceGenerator`。之后再调用 `load_scene`（基座工具）进入下一场景。

**Python：**
```python
# 胜利后领取奖励
r = g.call("claim_rewards")
print(f"领取动作: {r['actions']}")

# 继续下一场战斗
g.call("load_scene", {"type": "fight", "id": "common"})
```

---

## 典型工作流：完整战斗循环

```python
# === 进入假战斗 ===
g.call("load_scene", {"type": "fakefight"})  # 基座工具

# === 战斗循环 ===
for round_num in range(5):
    fight = g.call("get_fight_state")
    print(f"\n=== 回合 {round_num} ===")
    print(f"手牌: {len(fight['hand'])} 张, 能量: {fight['player']['power']}")

    # 出牌：打出所有可用的牌
    for card in fight['hand']:
        if card.get('cost', 99) <= fight['player']['power']:
            r = g.call("play_card", {
                "index": card['index'],
                "targetIndex": 0
            })
            if r['result'] == 'success':
                print(f"  打出 {card['cardId']}")

    # 结束回合
    g.call("end_turn")

    # 检查是否胜利
    fight = g.call("get_fight_state")
    alive = [e for e in fight['enemies'] if not e['isDead']]
    if len(alive) == 0:
        print("胜利！")
        break

# === 领取奖励 ===
g.call("claim_rewards")
```

## 最佳实践

1. **先 get_fight_state 再操作** — 获取手牌位置、敌人索引、能量等上下文
2. **出牌优先用 index** — 刚读取手牌状态后，index 比 cardId 更确定
3. **end_turn 仅玩家回合可用** — 检查 `phase` 字段
4. **set_card_pile 是底层操作** — 优先用 `play_card` 和 `end_turn` 做自然游戏流程
5. **set_fight_entity 适合模拟** — 设 HP=1 快速测试击杀，设满血测试生存
6. **claim_rewards 是幂等的** — 多次调用安全，但后续调用可能没有动作
7. **每次 mutation 后重新读取** — 用 `get_fight_state` 确认变更生效
