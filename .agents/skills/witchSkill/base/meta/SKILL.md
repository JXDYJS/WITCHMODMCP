---
name: witch-mod-mcp-meta
description: "WitchModMCP meta/global state tools: page detection, player snapshot, game install info, save inspection, game mode listing. Use when the user wants to know what page the game is on, read the player's current stats (HP/SAN/money/deck), find the game installation path, check available saves, or list game modes. Triggers: get_scene_state, get_game_data, get_game_info, check_mode_saves, list_game_modes, scene state, game data, 游戏路径, 安装目录, 场景检测, 页面状态, 存档, 游戏模式."
---

# Meta Module

Global state probes — detect the current game page, read player/runtime snapshots, inspect saves, and list available game modes. Use these as the first step in any workflow to orient the AI.

## Tools

| Tool | Params | Returns |
|------|--------|---------|
| `get_scene_state` | — | `{page, inRun, inFight, fightType?, player?, modals, transitioning, overlays, level}` |
| `get_game_data` | — | `{player?, fight?, runtime?}` — player HP/SAN/money/deck snapshot |
| `get_game_info` | — | `{dataPath, gameRoot, managedPath, modsPath, unityVersion, platform, loadedMods}` — game install info |
| `check_mode_saves` | `{mode?}` | `{hasSaves, totalSaves, validSaves, saves: [{name, mode, level, career?, cardCount, relicCount}]}` |
| `list_game_modes` | — | `{modes: [{mode, hasSave, saveCount, save?}]}` |
| `get_recent_logs` | `{count=50, level="All"}` | JSON array of recent log entries, optional level filter (`All`, `Log`, `Warning`, `Error`) |

---

### get_scene_state

Detects the current game UI page. Returns which page is active and whether there are blocking modals or transitions.

**Detected page values:**
| Page | Meaning |
|------|---------|
| `MAIN_MENU` | Title screen — no save loaded |
| `MODE_SELECT` | Mode choice dialog |
| `LOBBY` | Career selection hall (`GameEntryUI`) |
| `FIGHT` | In combat |
| `SHOP` | Shop UI open |
| `BREAKS` | **安全屋/休息处**（每层第一个事件节点后进入）— 有保险箱和食物。**不是 MAP！** 不要误以为回到了地图选点页面 |
| `MAP` | Map screen or in-run but not fighting |
| `HUB` | House/hub scene |
| `UNKNOWN` | Unrecognized state |

**Return fields:**
| Field | Type | Description |
|-------|------|-------------|
| `page` | string | Current page identifier |
| `inRun` | bool | Whether a run is in progress |
| `inFight` | bool | Whether currently in combat |
| `fightType` | string | Combat type when inFight (e.g. `Player`, `Enemy`) |
| `fightPlayer` | object | `{hp, maxHp, power, shield}` when inFight |
| `modals` | bool | Whether a blocking modal is open |
| `transitioning` | bool | Whether a scene transition/animation is playing |
| `activeUI` | string or null | **The topmost active modal/popup** — tells you exactly what tool to use next without checking the scene tree. `null` = no active modal. |
| `activeUIs` | string[] | All currently active UI modals (topmost first) |
| `overlays` | string[] | Active overlay UIs (e.g. `SettingUI`, `BackpackUI`) |
| `player` | object | `{hp, maxHp, san, maxSan, money}` from RoleTable |
| `level` | int | Current level when inRun |

**activeUI quick reference:**
| Value | What to do next |
|-------|----------------|
| `BattleRewardsUI` | `get_rewards_state` 查看奖励列表 → `click_ui` 按全局 index 领取单项奖励（金钱直接到账、卡牌打开 CardChoiceUI、祝福打开 BlessingChoiceGenerator）→ 最后 `claim_rewards` 关闭 |
| `CardChoiceUI` | Call `pick_card_reward` or `skip_card_reward` |
| `BlessingChoiceGenerator` | Call `pick_blessing_reward` or `skip_blessing_reward` |
| `DeckUI` | Call `get_deck_selection` → `select_deck_cards` |
| `BreaksUI` | Use `scan_ui` to find the continue/leave button, then `click_ui` |
| `EventUI` | `get_event_state` 查看选项详情 → `event_choose_option` 或 `event_advance_dialogue` ⚠️ 部分事件有多阶段选项，选完一次后重新检查 `activeUI`，如果 EventUI 还在则继续选，没有了才 `event_advance_dialogue` |
| `ShopUI` | Use `get_shop_state`, `shop_buy`, `shop_sell`, `shop_refresh` |
| `SafeBoxUI` | Use `safebox_*` tools |
| `MapSelectUI` | Use `map_select_state` → `map_select_assign` → `map_select_confirm` |
| `OutDeckUI` | Call `get_outdeck_state` → `outdeck_move_card` / `outdeck_decompose` |
| `SettingUI` / `BackpackUI` / any other UI | Use `scan_ui` to list interactable buttons, then `click_ui` to pick the right one |
| `null` | No modal; use page-level tools normally |

> ℹ️ `scan_ui` + `click_ui` is the **general-purpose navigation tool**. Whenever you don't know how to enter/leave a page or what to click, call `scan_ui` to discover all interactable buttons on screen, then use `click_ui` by index. This works for any UI without hardcoded logic.

**Python:**
```python
state = g.call("get_scene_state")
print(f"Page: {state['page']}, inFight: {state['inFight']}")
```

### get_game_data

Returns a compact player/fight/runtime snapshot. See root `SKILL.md` for field meanings.

**Return structure:**
```
player:
  hp, maxHp        current / max health
  san, maxSan      SAN 理智
  money            gold
  cardCount        deck size
  relicCount       relics held
  blessCount       blessings active
  unCardCount      removed/locked pile
  isDead           death flag

fight:
  inFight          false when not in combat
  fightType        combat kind (common/elite/boss)
  playerPower      current energy
  playerShield     current block

runtime:
  level            floor/stage depth
  time             time-flow resource
  truth            truth meta resource
  exp              experience
```

Empty `player` → no save loaded. `*Error` field → that section threw an exception.

**Python:**
```python
data = g.get_game_data()
if "player" in data:
    print(f"HP: {data['player']['hp']}/{data['player']['maxHp']}")
```

### get_game_info

Returns the game's installation paths and version information. Useful for scripts that need to locate the game's files on disk.

**Return fields:**
| Field | Type | Description |
|-------|------|-------------|
| `dataPath` | string | Unity `Application.dataPath` — the game's `_Data` folder |
| `gameRoot` | string | Parent of dataPath — the game install root directory |
| `managedPath` | string | Path to `Managed/` (game DLLs) |
| `modsPath` | string | Path to `Mods/` directory |
| `unityVersion` | string | Unity engine version (e.g. `6000.0.46f1`) |
| `platform` | string | Build target platform (`WindowsPlayer`) |
| `productName` | string | Unity product name |
| `companyName` | string | Unity company name |
| `loadedMods` | array | `[{name, directory}]` — all currently loaded mods and their disk paths |
| `loadedModDirectories` | array | Raw mod directory paths from GameConfigManager |

**Python:**
```python
info = g.call("get_game_info")
print(f"Game: {info['productName']} ({info['unityVersion']})")
print(f"Root: {info['gameRoot']}")
print(f"Mods: {info['modsPath']}")
for m in info.get('loadedMods', []):
    print(f"  {m['name']}: {m['directory']}")
```

### check_mode_saves

Inspect save files for a specific game mode, or all modes. Uses `ModeChoiceUI.CheckSave` to filter valid saves.

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `mode` | string | No | (all) | Game mode filter, e.g. `Normal`, `Sublimation`, `Slot`, `Teach`, `Story` |

**Python:**
```python
saves = g.call("check_mode_saves", {"mode": "Normal"})
print(f"Valid saves: {saves['validSaves']}")
for s in saves['saves']:
    print(f"  {s['name']} — level {s['level']}, {s.get('career', '?')}")
```

### list_game_modes

List all available game modes (including those registered by mods) and whether each has a valid save.

**Python:**
```python
modes = g.call("list_game_modes")
for m in modes['modes']:
    print(f"{m['mode']}: {m['saveCount']} saves")
```

## Best practices

1. Always start with `get_scene_state` to orient before calling any mutation tool. If the game is in an unexpected page, report it instead of plowing ahead.
2. Use `get_game_data` for a quick player status check. Use `get_fight_state` (Combat module) for detailed combat information.
3. `check_mode_saves` and `list_game_modes` are most useful at the MAIN_MENU or MODE_SELECT page, before calling `start_new_game`.
4. A `*Error` field in `get_game_data` (e.g. `playerError`) is a signal to switch to diagnostics tools (`inspect`, `dump_mod_state`) to investigate what went wrong.
