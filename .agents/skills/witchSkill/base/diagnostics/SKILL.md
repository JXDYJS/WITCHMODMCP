---
name: witch-mod-mcp-diagnostics
description: "WitchModMCP diagnostics and developer backdoor tools: C# runtime reflection, config table queries, mod state inspection, scene GameObject tree, log capture, raycasting, screenshot capture, RNG seed control, and item injection. Use when the user needs to debug mod behaviour by reflecting over C# objects, querying game config tables like CardConfig/RelicConfig, inspecting which mods loaded, navigating the Unity scene hierarchy, capturing screen output, controlling randomness, or injecting test items. Triggers: diagnostics, debug, inspect, query_config, search_config, dump_mod_state, get_scene_tree, raycast_mouse, set_rng_seed, get_screenshot, give_item, reflection, 调试, 反射, 查配置, 日志, 截图, 场景树."
---

# Diagnostics Module

Developer backdoor tools for deep inspection and debugging. These tools break the normal game flow — use them deliberately for mod development and testing.

> **⚠️ Debugging Priority: Logs First**
>
> **`get_recent_logs` is your primary debugging tool.** When a mod doesn't load, a card doesn't appear, or something breaks:
> 1. Call `get_recent_logs({"count": 100})`
> 2. Search for `[Mod]`, `[Error]`, `CSV`, `Lua`, or your mod name in the output
> 3. The game prints specific error messages for: CSV parse failures, Lua compilation errors, missing `BaseScript`, invalid `PackBelong`, ModConfig JSON errors
> 4. Only escalate to `inspect` / `query_config` if logs are clean and you need deeper runtime state
>
> **Do NOT use `inspect` or `query_config` to debug CSV loading issues** — the game already logs them. Reading private fields (`DataConfigCache`, `_tables`) is almost never the right approach.

## Tools

| Tool | Params | Returns |
|------|--------|---------|
| `inspect` | `{typeName, memberPath?, maxDepth=3, maxItems=20}` | C# type reflection: members or member value |
| `query_config` | `{tableName?, id?, limit=5}` | Game config table listing or item query |
| `search_config` | `{pattern, limit=20, includeFields=false}` | Fuzzy keyword search across DataConfigCache (all runtime IDs) |
| `dump_mod_state` | — | `{modCount, mods: [{assemblyName, assemblyLocation, assemblyVersion, initTypes}], relatedAssemblies}` |
| `get_scene_tree` | `{rootName?, maxDepth=10, maxChildren=50, includeComponents=true, includeInactive=false}` | `{sceneName, hierarchy: [node…]}` |
| `get_recent_logs` | `{count=50}` | JSON array of recent log entries |
| `raycast_mouse` | `{screenX?, screenY?, maxResults=30}` | `{hitCount, hits: [{gameObjectName, hierarchyPath, components, ...}]}` |
| `set_rng_seed` | `{seed?, forceRng?}` | `{result, changes: []}` |
| `get_screenshot` | `{format="png", quality=75}` | `{mimeType, base64, width, height, size}` |
| `give_item` | `{type, value}` | `{type, value, result}` |

---

### inspect

Reflect over any C# type loaded in the game's AppDomain. No memberPath returns the type's static and instance member listing. With memberPath (dot-separated), traverses the object graph.

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `typeName` | string | Yes | — | Type name (partial or full), e.g. `RoleTable`, `Witch.Data.RoleTable` |
| `memberPath` | string | No | — | Dot-separated path: `Instance`, `Instance.CurHp`, `Instance.Status.Defend` |
| `maxDepth` | int | No | 3 | Recursive serialization depth for returned object values |
| `maxItems` | int | No | 20 | Max collection items to enumerate |

**Examples:**
```python
# List members of a type
result = g.call("inspect", {"typeName": "RoleTable"})
print(result['members']['static'])
print(result['members']['instance'])

# Read a specific member value
result = g.call("inspect", {
    "typeName": "RoleTable",
    "memberPath": "Instance.San"
})
print(f"SAN = {result['value']}")

# Deep inspect
result = g.call("inspect", {
    "typeName": "FightPlayer",
    "memberPath": "Instance.Status",
    "maxDepth": 2
})
```

### query_config

Query the game's config table system. Without `tableName`, lists all available tables with their type and item count. With `tableName`, returns sample rows. With `tableName` + `id`, returns a specific config entry.

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `tableName` | string | No | — | Config table name, e.g. `CardConfig`, `RelicConfig`, `CareerConfig`. Omit to list tables. |
| `id` | int | No | — | Specific config entry ID |
| `limit` | int | No | 5 | Max sample rows to return |

**Examples:**
```python
# List all config tables
tables = g.call("query_config")
for t in tables['availableTables']:
    print(f"  {t['name']}: {t['type']} ({t.get('itemCount', '?')} items)")

# Query a config table
result = g.call("query_config", {"tableName": "CardConfig", "limit": 3})
print(f"Total: {result.get('totalCount', '?')}")
for s in result['samples']:
    print(s)

# Query by ID
result = g.call("query_config", {"tableName": "CardConfig", "id": 1001})
print(result['item'])
```

### search_config

**模糊搜索 DataConfigCache（全量运行时 ID 仓库）。** `query_config` 查的是 `_tables` 字典（34 张表），`search_config` 查的是 `DataConfigCache`（~2180 条，包含所有卡牌、Buff、卡包、遗物等运行时 ID）。两者互补。

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `pattern` | string | Yes | — | 搜索关键词（忽略大小写）。匹配 DataConfigCache 的 key 和 NativeIds |
| `limit` | int | No | 20 | 最多返回条数 |
| `includeFields` | bool | No | false | 是否返回匹配条目的核心字段（Id/Name/Type/Rarity 等）。设为 true 会略微增加耗时 |
| `searchNativeIds` | bool | No | true | 是否同时搜索 NativeIds（游戏原生 ID 注册表）。NativeIds 包含 `buff_regenerate` 等内置 ID |

**返回字段：**

| 字段 | 说明 |
|------|------|
| `matchedKeys` | 匹配的 DataConfigCache key 列表。`includeFields=true` 时每项是 `{_key, id, name, type, ...}` 对象 |
| `matchCount` | DataConfigCache 中的匹配数 |
| `totalCacheSize` | DataConfigCache 总条目数 (~2180) |
| `nativeIdMatches` | NativeIds 中的匹配 ID 列表（仅 `searchNativeIds=true` 时） |
| `nativeIdMatchCount` | NativeIds 中的匹配数 |
| `totalNativeIds` | NativeIds 总条目数 (~1723) |

**典型用法：**

```python
# 1. 搜索卡牌/卡包/关键词
result = g.call("search_config", {"pattern": "plague"})
print(f"DataConfigCache 匹配 {result['matchCount']} 条（共 {result['totalCacheSize']}）")
for key in result['matchedKeys']:
    print(f"  {key}")

# 2. 搜索并查看字段信息
result = g.call("search_config", {"pattern": "cardpack", "limit": 5, "includeFields": True})
for entry in result['matchedKeys']:
    if isinstance(entry, dict):
        print(f"  {entry['_key']}: id={entry.get('id')} type={entry.get('type')} name={entry.get('name')}")

# 3. 搜索内置 Buff（在 NativeIds 中找）
result = g.call("search_config", {"pattern": "buff_regenerate", "searchNativeIds": True})
if result['nativeIdMatchCount'] > 0:
    print("内置 Buff 已注册:", result['nativeIdMatches'][0])
else:
    print("在 DataConfigCache 中查找:", result['matchedKeys'][:3] if result['matchedKeys'] else "未找到")

# 4. 验证 Mod 数据是否加载（用 Mod 文件夹名搜）
result = g.call("search_config", {"pattern": "YourModFolder"})
if result['matchCount'] == 0:
    print("⚠️ 未找到匹配数据，CSV 可能未加载或格式错误")

# 5. 从 Python 脚本使用
"""
from witch_mcp import WitchMcp
mcp = WitchMcp()
r = mcp.search_config("buff", limit=5)
for key in r["matchedKeys"]:
    print(key)
"""
```

**什么时候用 search_config vs query_config：**

| 场景 | 用哪个 |
|------|--------|
| 查某张卡牌的运行时 ID | `search_config({"pattern": "卡牌名"})` |
| 查 `_tables` 字典里的结构化配置表 | `query_config({"tableName": "CardConfig"})` |
| 验证 Mod 的 CSV 数据已加载 | `search_config({"pattern": "ModFolder"})` |
| 查具体 ID 的配置详情 | `query_config({"tableName": "CardConfig", "id": 1001})` |
| 翻所有数据找某关键词 | `search_config({"pattern": "uncommon"})` |
| 查内置 Buff 是否注册 | `search_config({"pattern": "buff_regenerate", "searchNativeIds": true})` |

### dump_mod_state

List all loaded mods by scanning assemblies for `[ModInitialize]` attributes. Includes both third-party mods and WitchModMCP's own assemblies.

**Python:**
```python
state = g.call("dump_mod_state")
print(f"Loaded mods: {state['modCount']}")
for m in state['mods']:
    print(f"  {m['assemblyName']} v{m['assemblyVersion']}")
    for t in m['initTypes']:
        print(f"    init: {t['typeName']}.{t.get('entryMethod', '(type)')}")
```

### get_scene_tree

Walk the active Unity scene's GameObject hierarchy. Filter by `rootName` to narrow down to a specific subtree.

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `rootName` | string | No | — | Game object name filter; returns subtree if found |
| `maxDepth` | int | No | 10 | Maximum recursion depth |
| `maxChildren` | int | No | 50 | Max children per node |
| `includeComponents` | bool | No | true | Include component type list per node |
| `includeInactive` | bool | No | false | Include inactive objects |

**Python:**
```python
# Full scene tree
tree = g.call("get_scene_tree", {"maxDepth": 3})
print(f"Scene: {tree['sceneName']}, roots: {tree['rootCount']}")

# Narrow to a specific root
subtree = g.call("get_scene_tree", {"rootName": "UI Root", "includeInactive": True})
```

### get_recent_logs

**⛳ Primary debugging tool — call this first whenever something breaks.**

Retrieve the most recent log entries from the in-memory ring buffer (captured via Harmony patches on the game's logging system).

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `count` | int | No | 50 | Number of recent entries to return |

**Python:**
```python
logs = g.call("get_recent_logs", {"count": 20})
for entry in logs:
    print(entry)
```

### raycast_mouse

Fire a ray from the given screen position (or current mouse position) and return all hit GameObjects. Combines EventSystem (UI), Physics3D, and Physics2D raycasts.

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `screenX` | number | No | (mouse) | Screen X coordinate in pixels |
| `screenY` | number | No | (mouse) | Screen Y coordinate in pixels |
| `maxResults` | int | No | 30 | Maximum hit results |

**Python:**
```python
# Raycast at current mouse position
hits = g.call("raycast_mouse")
print(f"Hits: {hits['hitCount']}")
for h in hits['hits'][:5]:
    print(f"  {h['gameObjectName']} — {h['hierarchyPath']}")

# Raycast at specific position
hits = g.call("raycast_mouse", {"screenX": 960, "screenY": 540})
```

### set_rng_seed

Control the game's random number generator for reproducible testing. Sets the `TempDataManager` seed pool and/or forces the next `Dice` roll.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `seed` | int | No | Set TempDataManager random seed |
| `forceRng` | number | No | Force next Dice result (0.0–1.0) |

**Python:**
```python
# Set seed for reproducible runs
g.call("set_rng_seed", {"seed": 12345})

# Force next random roll to 0.5
g.call("set_rng_seed", {"forceRng": 0.5})
```

### get_screenshot

Capture the current game画面 and return it as a base64-encoded image.

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `format` | string | No | `png` | Image format: `png` or `jpg` |
| `quality` | int | No | 75 | JPG quality (1–100), only for jpg format |

**Python:**
```python
screenshot = g.call("get_screenshot", {"format": "jpg", "quality": 80})
print(f"Size: {screenshot['width']}x{screenshot['height']}, {screenshot['size']} bytes")
# Decode and save
import base64
with open("screenshot.jpg", "wb") as f:
    f.write(base64.b64decode(screenshot['base64']))
```

### give_item

Grant resources or items to the player via the game's `Commands.give()` system. This is a debug/cheat tool — use deliberately.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `type` | string | Yes | Item type: `money`, `san`, `maxsan`, `card`, `relic`, `bless`, `power`, `exp`, `level`, `str`/`strength`, `luc`/`lucky`, `per`/`perceive`, `wis`/`wisdom`, `draw`, `randomcard`, `randomrelic`, `time`, `truth`/`true`, `win` |
| `value` | string | Yes | Amount, config ID, or `"all"` |

**Python:**
```python
# Give 100 gold
g.call("give_item", {"type": "money", "value": "100"})

# Give a specific card
g.call("give_item", {"type": "card", "value": "card_strike_1"})

# Give all cards
g.call("give_item", {"type": "card", "value": "all"})

# Give a random relic
g.call("give_item", {"type": "randomrelic", "value": "1"})
```

## Best practices

1. `inspect` is the most powerful diagnostic tool — use it when you need to understand any runtime C# object the game exposes.
2. `query_config` is essential for content mod development — use it to verify config entries match your mod's expectations.
3. `dump_mod_state` is the first thing to check when a mod fails to load or behave correctly.
4. `get_screenshot` is useful for visual verification but produces large payloads — prefer reading structured data when possible.
5. `give_item` duplicates functionality of `eval_command give ...` but provides structured JSON output instead of raw console text. Prefer `give_item` over `eval_command` for item granting.
6. `set_rng_seed` is for reproduction of specific scenarios — use `seed` for full-run reproducibility and `forceRng` to control a single dice roll.
7. `raycast_mouse` helps identify which GameObject the mouse is over — useful for scene debugging and understanding UI/click hierarchies.
