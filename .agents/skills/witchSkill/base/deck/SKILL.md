---
name: witch-mod-mcp-deck
description: "WitchModMCP deck (牌组) management tools: read and modify the OutDeckUI state, move cards between equipped and reserve, and decompose cards. Use when the user needs to manage their card deck during a run — checking equipped/reserve cards, moving cards in/out of the active deck, or destroying cards for gold. Triggers: deck, 牌组, outdeck, 装备/备选, 分解, 卡牌管理, get_outdeck_state, outdeck_move_card, outdeck_decompose."
---

# Deck Module — 牌组管理

牌组（OutDeckUI）管理工具。所有工具需要 OutDeckUI 处于打开状态（`get_scene_state` 返回 `activeUI=OutDeckUI`）。

## 牌组机制

游戏获得的卡牌不会直接进入装备牌组，而是进入**备选**区。只有手动将卡牌从备选移到装备牌组，卡牌才会在战斗中出现。

| 字段 | 说明 |
|------|------|
| `CardBottomCount` | 装备牌组**下限**（默认 13） — 装备不能少于这个数 |
| `CardTopCount` | 装备牌组**上限**（默认 18） — 装备不能多于这个数 |
| `MaxAlCardCount` | 备选区**上限**（默认 3） — 备选不能多于这个数 |

移动和分解操作调用游戏源生的 `ShowCard.MoveItem()` / `ShowCard.DecomposeItem()`，和右键菜单完全一致的执行路径（音效、延迟帧、对象池、同步）。

## Tools

| Tool | Params | Returns | Notes |
|------|--------|---------|-------|
| `get_outdeck_state` | — | `{isOpen, equippedCards, reserveCards, limits}` | 读取牌组完整状态 |
| `outdeck_move_card` | `{instanceId}` | `{result, action, message}` | 移动卡牌（装备↔备选自动检测方向） |
| `outdeck_decompose` | `{instanceId}` | `{result, action, cost, message}` | 分解卡牌（消耗金钱移除） |

---

### get_outdeck_state

读取当前 OutDeckUI 的完整状态，包括装备中卡牌、备选卡牌、以及各项限制值。

**返回字段：**
| 字段 | 类型 | 说明 |
|------|------|------|
| `isOpen` | bool | 牌组界面是否已打开 |
| `equippedCards` | array | 装备中卡牌列表 |
| `equippedCards[].cardId` | string | 卡牌 Config ID |
| `equippedCards[].name` | string | 卡牌显示名称 |
| `equippedCards[].instanceId` | string | **运行时唯一实例 ID**（用于 move/decompose） |
| `equippedCards[].cost` | string | 费用 |
| `equippedCards[].rarity` | string | 稀有度 |
| `equippedCards[].type` | string | 卡牌类型（攻击牌/技能牌等） |
| `equippedCards[].tag` | string | 标签（Retain/Burnout 等） |
| `reserveCards` | array | 备选卡牌列表（同 equippedCards 结构） |
| `cardBottomCount` | int | 装备下限 |
| `cardTopCount` | int | 装备上限 |
| `maxAlCardCount` | int | 备选上限 |
| `equippedCount` | int | 当前装备数 |
| `reserveCount` | int | 当前备选数 |

**Python：**
```python
state = g.call("get_outdeck_state")
if state.get('isOpen'):
    print(f"装备: {state['equippedCount']}/{state['cardTopCount']}")
    print(f"备选: {state['reserveCount']}/{state['maxAlCardCount']}")
    for card in state['equippedCards']:
        print(f"  {card['name']} ({card['instanceId']})")
```

### outdeck_move_card

将一张卡牌在装备↔备选之间移动。自动检测卡牌当前所在侧并移到另一侧。

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `instanceId` | string | 是 | 运行时实例 ID（从 `get_outdeck_state` 获取） |

内部调用了游戏源生的 `ShowCard.MoveItem()`，与右键菜单→"移动"完全一致：约束校验、音效、数据操作、对象池 CreateItem+Release、延迟帧 Null 激活、ChangeCardShow。

**Python：**
```python
# 将一张装备中的卡移到备选
r = g.call("outdeck_move_card", {"instanceId": "ed93af51-..."})
if r['result'] == 'success':
    print(f"移动成功: {r['action']}")

# 再调一次可移回
r = g.call("outdeck_move_card", {"instanceId": "ed93af51-..."})
```

### outdeck_decompose

分解一张卡牌，消耗金钱并从牌组/备选中永久移除。

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `instanceId` | string | 是 | 运行时实例 ID（从 `get_outdeck_state` 获取） |

内部调用了游戏源生的 `ShowCard.DecomposeItem()`，与右键菜单→"销毁"完全一致：Eternal 标签检查、金钱检查、下限检查、诅咒/Wisdom 检查、扣钱、ItemCheck（GameSaveAnalyser 同步、ObjectPool.Release、ChangeCardShow）。

分解消耗为 `20 + GameVar.ExpensiveCard` 金钱。

**Python：**
```python
r = g.call("outdeck_decompose", {"instanceId": "ed93af51-..."})
if r['result'] == 'success':
    print(f"已分解，消耗 {r['cost']} 金钱")
```

## Best practices

1. 操作前先调 `get_outdeck_state` 获取当前状态和 instanceId 列表
2. `instanceId` 是**运行时唯一 ID**，同一张卡的不同副本有不同的 instanceId
3. 移动/分解操作调用游戏源生方法，约束不满足时游戏会弹出提示弹窗，工具会返回 error
4. 装备中的卡不可少低于 `cardBottomCount`，不可多于 `cardTopCount`

## activeUI 快速参考

在 `base/meta/SKILL.md` 的 activeUI 快速参考中新增：

| 值 | 下一步该做什么 |
|----|--------------|
| `OutDeckUI` | `get_outdeck_state` → `outdeck_move_card` / `outdeck_decompose` |
