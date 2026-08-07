---
name: witch-mod-mcp-developertools-lobby
description: "DeveloperTools lobby (career selection hall) tools: read and modify career, partner, attribute allocation, and active card packs. Use when the user wants to inspect or change the character setup before starting a run. Triggers: lobby, 大厅, career, 职业, partner, 随从, card pack, 卡包, attributes, 属性加点, get_lobby_state, set_lobby_state."
---

# Lobby 模块 — 大厅配置

读取和修改职业选择大厅（GameEntryUI）。所有工具需要游戏处于 LOBBY 页面（`start_new_game` 之后、`start_run` 之前）。

## 工具总览

| 工具 | 参数 | 返回 |
|------|------|------|
| `get_lobby_state` | — | `{inLobby, career?, partner?, attributes, cardPacks, availableCareers, availablePartners}` |
| `set_lobby_state` | `{careerId?, partnerId?, attributes?, cardPackIds?}` | `{result, changes}` |

---

## 工具详情

### get_lobby_state

获取当前职业选择大厅的全部配置。

**返回字段：**
| 字段 | 类型 | 说明 |
|------|------|------|
| `inLobby` | bool | 是否在大厅中 |
| `career` | object | 已选职业的完整 data dict |
| `partner` | object | 已选随从的完整 data dict |
| `attributes` | object | `{main, second}` — 属性加点 |
| `cardPacks.activeIds` | string[] | 已启用卡包的 ID 列表 |
| `cardPacks.available` | array | 所有可用卡包详情（含卡牌/遗物/祝福数量） |
| `availableCareers` | array | 所有可用职业 |
| `availablePartners` | array | 所有可用随从 |

**Python：**
```python
lobby = g.call("get_lobby_state")
if not lobby['inLobby']:
    print("不在大厅中")
    exit()

print(f"职业: {lobby['career']['Id']}")
print(f"随从: {lobby['partner']['Id']}")
print(f"属性: {lobby['attributes']}")

active_packs = [p['id'] for p in lobby['cardPacks']['available'] if p['active']]
print(f"已启用卡包 ({len(active_packs)}): {active_packs}")
```

### set_lobby_state

修改大厅配置。每个字段可选，不传则不修改。

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `careerId` | string | 否 | 职业 ID（如 `Career_1`） |
| `partnerId` | string | 否 | 随从 ID（如 `Partner_2`） |
| `attributes` | object | 否 | `{main, second}` — `Strength` / `Lucky` / `Perceive` / `Wisdom` |
| `cardPackIds` | string[] | 否 | 启用卡包列表（必须 ≥6 个有效 ID） |

**属性可选值：** `Strength`、`Lucky`、`Perceive`、`Wisdom`

**Python：**
```python
# 选择法师职业 + 智慧属性 + 指定卡包
# ⚠️ 卡包 ID 是运行时 ID（如 "cardpack_1"，Mod 卡包为 "{ModFolder}_cardpack_{id}"），
#    实际值从 get_lobby_state 的 cardPacks.available[].id 取
r = g.call("set_lobby_state", {
    "careerId": "Career_3",
    "partnerId": "Partner_5",
    "attributes": {"main": "Wisdom", "second": "Lucky"},
    "cardPackIds": ["cardpack_1", "cardpack_2", "cardpack_3", "cardpack_4", "cardpack_5", "cardpack_6"]
})
print(f"变更: {r['changes']}")

# 只改属性
r = g.call("set_lobby_state", {
    "attributes": {"main": "Strength", "second": "Perceive"}
})
```

---

## 典型工作流：快速配置并启程

```python
# 开新游戏
g.call("start_new_game", {"mode": "Normal"})

# 读取当前大厅配置
lobby = g.call("get_lobby_state")
print(f"可用职业: {[c['Id'] for c in lobby['availableCareers']]}")

# 配置
g.call("set_lobby_state", {
    "careerId": lobby['availableCareers'][0]['Id'],
    "partnerId": lobby['availablePartners'][0]['Id'],
    "attributes": {"main": "Wisdom", "second": "Lucky"},
    "cardPackIds": [p['id'] for p in lobby['cardPacks']['available'] if p['active']]
})

# 启程
g.call("start_run")
```

## 最佳实践

1. **先读后写** — `get_lobby_state` 返回当前配置和所有可用选项，是 `set_lobby_state` 的前置步骤
2. **卡包数量限制** — 至少需要 6 个卡包。通过 `cardPacks.available` 查看已解锁的卡包
3. **变更确认** — `set_lobby_state` 后调用 `get_lobby_state` 确认变更生效
4. **完整启动流程** — `start_new_game` → `set_lobby_state`（可选）→ `start_run`
