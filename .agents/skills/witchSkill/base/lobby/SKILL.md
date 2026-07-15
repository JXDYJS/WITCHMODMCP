---
name: witch-mod-mcp-lobby
description: "WitchModMCP lobby (career selection hall) tools: read and modify career, partner, attribute allocation, and active card packs. Use when the user wants to inspect or change the character setup before starting a run — selecting a witch career, choosing a partner, allocating Strength/Lucky/Perceive/Wisdom points, or enabling/disabling card packs. Triggers: lobby, 大厅, career, 职业, partner, 随从, card pack, 卡包, attributes, 属性加点, get_lobby_state, set_lobby_state."
---

# Lobby Module

Read and modify the career selection hall (`GameEntryUI`). All tools require the game to be on the LOBBY page (after `start_new_game` but before `start_run`).

## Tools

| Tool | Params | Returns |
|------|--------|---------|
| `get_lobby_state` | — | `{inLobby, career?, partner?, attributes: {main, second}, cardPacks: {activeIds, available: [{id, type, name, active}]}, availableCareers, availablePartners}` |
| `set_lobby_state` | `{careerId?, partnerId?, attributes?, cardPackIds?}` | `{result, changes: []}` |

---

### get_lobby_state

Returns the full configuration of the current lobby: selected career, partner, attribute allocation, active card packs, and all available options.

**Return fields:**
| Field | Type | Description |
|-------|------|-------------|
| `inLobby` | bool | Whether the game is in the career selection hall |
| `career` | object | Selected career data (all key-value pairs from config) |
| `partner` | object | Selected partner data (all key-value pairs from config) |
| `attributes` | object | `{main: string, second: string}` — attribute point allocation |
| `cardPacks.activeIds` | string[] | IDs of currently enabled card packs |
| `cardPacks.available` | array | All unlocked card packs with `{id, type, name, description, icon, active, cardCount, relicCount, blessCount}` |
| `availableCareers` | array | All unlocked careers with `{Id, Name, SanMax}` |
| `availablePartners` | array | All unlocked partners with `{Id, Name, Bless, Attack, Defend, Hp, CardList}` |

**Python:**
```python
lobby = g.call("get_lobby_state")
print(f"Career: {lobby['career']['Id']}")
print(f"Partner: {lobby['partner']['Id']}")
print(f"Attributes: {lobby['attributes']}")
active = [p['id'] for p in lobby['cardPacks']['available'] if p['active']]
print(f"Active packs: {active}")
```

### set_lobby_state

Modify the lobby configuration. All parameters are optional — only provided fields are changed.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `careerId` | string | No | Career ID, e.g. `Career_1` |
| `partnerId` | string | No | Partner ID, e.g. `Partner_2` |
| `attributes` | object | No | `{main: "Strength"|"Lucky"|"Perceive"|"Wisdom", second: same}` |
| `cardPackIds` | string[] | No | Must contain at least 6 valid unlocked pack IDs |

**Valid attribute values:** `Strength`, `Lucky`, `Perceive`, `Wisdom`

**Python:**
```python
# Select a mage career with wisdom focus
result = g.call("set_lobby_state", {
    "careerId": "Career_3",
    "partnerId": "Partner_5",
    "attributes": {"main": "Wisdom", "second": "Lucky"},
    "cardPackIds": ["pack_1", "pack_2", "pack_3", "pack_4", "pack_5", "pack_6"]
})
print(result['changes'])
```

## Best practices

1. Always call `get_lobby_state` first to see the current configuration and available options before making changes.
2. The card pack minimum is 6 — check `cardPacks.available` in `get_lobby_state` to know what's unlocked.
3. After `set_lobby_state`, call `get_lobby_state` again to confirm changes took effect.
4. If the user wants to start a run, the workflow is: `start_new_game` → `set_lobby_state` (optional) → `start_run`.
