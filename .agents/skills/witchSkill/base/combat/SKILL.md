---
name: witch-mod-mcp-combat
description: "WitchModMCP combat tools: fight state snapshot, card play, turn end, card pile control, entity attribute modification. Use when the user wants to read the full battle state (player/enemy HP, hand, draw/discard piles, buffs, intents), play a card, end the current turn, manipulate card piles, or modify entity stats during a fight. Triggers: combat, 战斗, fight, play_card, end_turn, get_fight_state, set_card_pile, set_fight_entity, 出牌, 打牌, 回合."
---

# Combat Module

Complete battle read-write loop. All tools require the game to be in a fight (`get_scene_state` returns `page=FIGHT`) — except `get_skills_state`, which also works outside fights.

> **⚠️ IMPORTANT: Do NOT call `load_scene` to load a fight while already in a fight!**
> If you call `load_scene` (fight/fakefight) while already on `page=FIGHT`,
> the new fight will have a broken `FightPlayer` (its `Init()` won't run, leaving
> `FightPlayer.Instance` null), causing `end_turn`, `set_card_pile` (draw into hand),
> and other tools to fail.
>
> **Correct approach:** Win the current fight first (reduce enemy HP to 0; `claim_rewards` only collects and closes the battle-reward UI after victory — it does not end an ongoing fight), then return to the map, then use the map passage tools (`map_select_state` → `map_select_assign` → `map_select_confirm`) to enter the next fight. For test scripts that need to chain multiple fights, always exit to the map page between fights.

## Tools

| Tool | Params | Returns | Notes |
|------|--------|---------|-------|
| `get_fight_state` | — | `{inFight, phase, player, enemies, hand, drawPile, discardPile, exhaustPile, masterDeckCount, inSelectionMode}` | Full battle snapshot |
| `play_card` | `{cardId? / index?, targetIndex?, choices?}` | `{result, message, cardId, handBefore, handAfter, targetIndex?, targetHpBefore?, targetHpAfter?, discardedCount?, autoConfirmed?}` | Play a card from hand |
| `use_skill` | `{index, targetIndex?, ignoreCooldown?, setCooldown?}` | `{result, skillRuntimeId, skillRawId, skillName, targetIndex?, player?}` | Use career skill 1 or 2 |
| `get_skills_state` | — | `{result, careerId, careerName, inFight, skillCount, skills: [{index, runtimeId, rawId, name, cooldown, canUse, actionImage}]}` | Check skill cooldowns and availability |
| `end_turn` | — | `{result, message, phase?}` | End the player's current turn |
| `set_card_pile` | `{pile, action, cards?, indices?, shuffle?}` | `{result, changes: []}` | Manipulate hand/draw/discard/exhaust piles |
| `set_fight_entity` | `{instanceId?, target?, hp?, maxHp?, shield?, power?, maxPower?, addBuffs?, removeBuffs?, clearBuffs?}` | `{result, changes: []}` | Modify player or enemy attributes |
| `get_deck_selection` | — | `{totalCards, cards: [{index, cardId, name, cost, rarity, tag, isSelected}]}` | When DeckUI is open (card selection modal), list all selectable cards. |
| `select_deck_cards` | `{indices: [int]}` | `{result, clicked, clickedCount, message}` | Select/toggle cards in the DeckUI modal. Auto-closes when required count is reached. |

---

### get_fight_state

Returns a complete snapshot of the current battle. Call this before any combat mutation to understand the board state.

**Return fields:**
| Field | Type | Description |
|-------|------|-------------|
| `inFight` | bool | Whether in a fight |
| `phase` | string | `Player` / `Enemy` turn（等于 `FightType` 枚举，还可能为 `Partner`/`OtherTurn`/`Win`/`Loss` 等） |
| `isFake` | bool | Whether this is a fake fight (preview/test) |
| `turn` | int | 当前地图层数（`MapManager.Level`，非回合数），未进战斗为 0 |
| `player` | object | `{instanceId, hp, maxHp, shield, power, maxPower, isDead, buffs: [{id, level, type}]}` |
| `enemies` | array | `[{index, instanceId, id, name, hp, maxHp, shield, isDead, attack, defend, buffs, intents}]` — each enemy |
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

### use_skill

Use a career skill (Skill1/Skill2). Must be in combat during the player's turn.

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `index` | int | Yes | — | Skill index: 1 or 2 |
| `targetIndex` | int | No | — | Target enemy index (optional) |
| `ignoreCooldown` | bool | No | true | Whether to ignore cooldown |
| `setCooldown` | int | No | — | Set cooldown after use (game default if omitted) |

> **⚠️ 技能可能触发选牌模态！** 某些技能（如"从抽牌堆选择一张卡"）会弹出 `DeckUI` 选牌界面。
>
> **释放技能后必须立即检查 `get_fight_state` 的 `inSelectionMode` 字段：**
> - `true` → 使用 `get_deck_selection` 查看可选牌 → `select_deck_cards` 选择
> - 如果不处理直接 `end_turn`，游戏可能跳过选牌导致技能效果异常
>
> ```python
> # 正确用法：
> r = g.call("use_skill", {"index": 1})
> fight = g.call("get_fight_state")
> if fight.get('inSelectionMode'):
>     selection = g.call("get_deck_selection")
>     g.call("select_deck_cards", {"indices": [0]})
> ```

**Python:**
```python
# Use skill 1 on enemy 0
result = g.call("use_skill", {"index": 1, "targetIndex": 0})
print(f"Skill used: {result['skillName']}, HP now: {result['player']['hp']}")

# Use skill with cooldown management
result = g.call("use_skill", {
    "index": 2, "ignoreCooldown": False, "setCooldown": 3
})
```

### get_skills_state

Check the current skill state — cooldown, availability, and runtime IDs.

**Python:**
```python
skills = g.call("get_skills_state")
for s in skills['skills']:
    print(f"Skill {s['index']}: {s['name']} (cd={s['cooldown']}, canUse={s['canUse']})")
```

### get_deck_selection

When a card/game effect opens the DeckUI (e.g. "choose a card from draw pile" or "discard cards"), list all selectable cards.

**Return:**
| Field | Type | Description |
|-------|------|-------------|
| `totalCards` | int | Number of selectable cards |
| `cards` | array | `[{index, cardId, name, cost, rarity, tag, isSelected}]` |

**Python:**
```python
# After a skill triggers a card selection modal
selection = g.call("get_deck_selection")
for c in selection['cards']:
    print(f"  [{c['index']}] {c['name']} ({c['cardId']}) — {'selected' if c['isSelected'] else ''}")
```

### select_deck_cards

Select/toggle cards in the DeckUI modal. When the required selection count is reached (e.g. "choose 1 card"), the UI auto-closes.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `indices` | int[] | Yes | Card indices to click (0-based). Toggles selection state. |

**Python:**
```python
# Select card at index 4 (auto-closes if count reached)
g.call("select_deck_cards", {"indices": [4]})

# Multi-select (for "discard 2" type effects)
g.call("select_deck_cards", {"indices": [0, 2]})
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
| `instanceId` | int | No* | **(recommended)** Runtime instance ID from `get_fight_state` player/enemies[].instanceId |
| `target` | string | No* | `"player"` or enemy index as string (e.g. `"0"`) — legacy, prefer `instanceId` |
| `hp` | int | No | Set current HP |
| `maxHp` | int | No | Set maximum HP |
| `shield` | int | No | Set defend/shield value |
| `power` | int | No | Set energy/power (player only) |
| `maxPower` | int | No | Set max energy (player only) |
| `addBuffs` | array | No | `[{id: string, level: int}]` — buffs to add |
| `removeBuffs` | string[] | No | Buff IDs to remove |
| `clearBuffs` | bool | No | Clear all buffs |

*\*Must provide either `instanceId` or `target`.*

**Python:**
```python
# Heal player to full and add a buff (using instanceId)
fight = g.call("get_fight_state")
player_id = fight["player"]["instanceId"]
g.call("set_fight_entity", {
    "instanceId": player_id,
    "hp": 80,
    "addBuffs": [{"id": "buff_regenerate", "level": 2}]
})

# Set enemy 0 HP to 1 (using instanceId)
enemy_id = fight["enemies"][0]["instanceId"]
g.call("set_fight_entity", {"instanceId": enemy_id, "hp": 1})
```

## Best practices

1. Always call `get_fight_state` before mutating — it gives you the full board context including enemy indices, hand positions, and buffs.
2. When playing cards, prefer `index` over `cardId` when you've just read the hand state, since indices are unambiguous.
3. `end_turn` only works during `phase=Player`. Check `get_fight_state` first.
4. `set_card_pile` and `set_fight_entity` are low-level manipulation tools — prefer `play_card` and `end_turn` for natural gameplay.
5. After any mutation, re-read with `get_fight_state` to confirm the change took effect.
