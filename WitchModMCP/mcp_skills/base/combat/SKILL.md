---
name: witch-mod-mcp-combat
description: "WitchModMCP combat tools: fight state snapshot, card play, turn end, card pile control, entity attribute modification. Use when the user wants to read the full battle state (player/enemy HP, hand, draw/discard piles, buffs, intents), play a card, end the current turn, manipulate card piles, or modify entity stats during a fight. Triggers: combat, 战斗, fight, play_card, end_turn, get_fight_state, set_card_pile, set_fight_entity, 出牌, 打牌, 回合."
---

# Combat Module

Complete battle read-write loop. All tools require the game to be in a fight (`get_scene_state` returns `page=FIGHT`).

## Tools

| Tool | Params | Returns | Notes |
|------|--------|---------|-------|
| `get_fight_state` | — | `{inFight, phase, player, enemies, hand, drawPile, discardPile, exhaustPile, masterDeckCount, inSelectionMode}` | Full battle snapshot |
| `play_card` | `{cardId? / index?, targetIndex?, choices?}` | `{result, cardId, handBefore, handAfter, targetHpBefore?, targetHpAfter?}` | Play a card from hand |
| `end_turn` | — | `{result, message, phase?}` | End the player's current turn |
| `set_card_pile` | `{pile, action, cards?, indices?, shuffle?}` | `{result, changes: []}` | Manipulate hand/draw/discard/exhaust piles |
| `set_fight_entity` | `{target, hp?, maxHp?, shield?, power?, maxPower?, addBuffs?, removeBuffs?, clearBuffs?}` | `{result, changes: []}` | Modify player or enemy attributes |

---

### get_fight_state

Returns a complete snapshot of the current battle. Call this before any combat mutation to understand the board state.

**Return fields:**
| Field | Type | Description |
|-------|------|-------------|
| `inFight` | bool | Whether in a fight |
| `phase` | string | `Player` / `Enemy` turn |
| `isFake` | bool | Whether this is a fake fight (preview/test) |
| `turn` | int | Current level/turn number |
| `player` | object | `{hp, maxHp, shield, power, maxPower, isDead, buffs: [{id, level, type}]}` |
| `enemies` | array | `[{index, id, name, hp, maxHp, shield, isDead, attack, defend, buffs, intents}]` — each enemy |
| `hand` | array | `[{index, cardId, instanceId, cost}]` — cards in hand |
| `drawPile` | object | `{count, top5: [{cardId, instanceId}]}` — draw pile count + top 5 |
| `discardPile` | object | `{count, last5: [{cardId, instanceId}]}` — discard pile count + last 5 |
| `exhaustPile` | object | `{count, cards: [{cardId, instanceId}]}` — exhausted cards |
| `masterDeckCount` | int | Total cards in the master deck (all piles combined) |
| `inSelectionMode` | bool | Whether a card selection/discard modal is active |
| `selectedCardCount` | int | Number of cards currently selected |

**Python:**
```python
state = g.call("get_fight_state")
print(f"Phase: {state['phase']}, HP: {state['player']['hp']}")
for e in state['enemies']:
    print(f"  Enemy {e['index']}: {e['id']} HP={e['hp']}")
for c in state['hand']:
    print(f"  Hand[{c['index']}]: {c['cardId']} cost={c['cost']}")
```

### play_card

Play a card from the hand. Supports identification by hand index or cardId. Attack cards can specify a target enemy index. After playing, handles modal choices (discard/select/auto-confirm).

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `cardId` | string | No* | Card config ID (e.g. `card_1`) |
| `index` | int | No* | 0-based hand position |
| `targetIndex` | int | No | Target enemy index for attack cards |
| `choices` | object | No | Post-play modal handling |

*Either `cardId` or `index` required.

**choices object:**
| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `discardIndices` | int[] | — | Hand indices to discard |
| `selectIndices` | int[] | — | Hand indices to select |
| `autoConfirm` | bool | true | Auto-confirm if selection modal appears |
| `autoSelectFirst` | bool | false | Auto-select first valid card if nothing selected |

**Python:**
```python
# Play card by index, targeting enemy 0
result = g.call("play_card", {"index": 0, "targetIndex": 0})
print(result)

# Play card by cardId with auto discard
result = g.call("play_card", {
    "cardId": "card_strike_1",
    "choices": {"discardIndices": [3, 4]}
})
```

### end_turn

Force-end the current player's turn, triggering enemy actions. Only works during the player phase.

**Python:**
```python
result = g.call("end_turn")
print(result['message'])  # "已触发结束回合指令，敌方即将行动"
```

### set_card_pile

Low-level control over the four card piles in a fight.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `pile` | string | Yes | `hand` / `draw` / `discard` / `exhaust` |
| `action` | string | Yes | `add` / `remove` / `clear` / `set` |
| `cards` | string[] | No | Card IDs for add/remove/set |
| `indices` | int[] | No | Hand indices for remove from hand |
| `shuffle` | bool | No | Shuffle draw pile after add (default false) |

**Actions per pile:**
| Pile | add | remove | clear | set |
|------|-----|--------|-------|-----|
| hand | Draw cards into hand | Remove by index/cardId | Empty hand | Replace hand with cards |
| draw | Add to draw pile | Remove by cardId | Empty draw | Replace draw pile |
| discard | Add to discard | Remove by cardId | Empty discard | Replace discard |
| exhaust | Exhaust (remove from game) | Restore from exhaust | Return all to draw | — |

**Python:**
```python
# Put 3 specific cards on top of draw pile
g.call("set_card_pile", {
    "pile": "draw", "action": "add",
    "cards": ["card_1", "card_2", "card_3"],
    "shuffle": False
})

# Clear the exhaust pile (return all to draw)
g.call("set_card_pile", {"pile": "exhaust", "action": "clear"})
```

### set_fight_entity

Modify a player or enemy entity's attributes during combat.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `target` | string | Yes | `"player"` or enemy index as string (e.g. `"0"`) |
| `hp` | int | No | Set current HP |
| `maxHp` | int | No | Set maximum HP |
| `shield` | int | No | Set defend/shield value |
| `power` | int | No | Set energy/power (player only) |
| `maxPower` | int | No | Set max energy (player only) |
| `addBuffs` | array | No | `[{id: string, level: int}]` — buffs to add |
| `removeBuffs` | string[] | No | Buff IDs to remove |
| `clearBuffs` | bool | No | Clear all buffs |

**Python:**
```python
# Heal player to full and add a buff
g.call("set_fight_entity", {
    "target": "player",
    "hp": 80,
    "addBuffs": [{"id": "buff_regenerate", "level": 2}]
})

# Set enemy 0 HP to 1
g.call("set_fight_entity", {"target": "0", "hp": 1})
```

## Best practices

1. Always call `get_fight_state` before mutating — it gives you the full board context including enemy indices, hand positions, and buffs.
2. When playing cards, prefer `index` over `cardId` when you've just read the hand state, since indices are unambiguous.
3. `end_turn` only works during `phase=Player`. Check `get_fight_state` first.
4. `set_card_pile` and `set_fight_entity` are low-level manipulation tools — prefer `play_card` and `end_turn` for natural gameplay.
5. After any mutation, re-read with `get_fight_state` to confirm the change took effect.
