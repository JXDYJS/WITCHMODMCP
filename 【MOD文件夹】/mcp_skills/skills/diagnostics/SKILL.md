---
name: witch-mod-mcp-diagnostics
description: "WitchModMCP diagnostics and developer backdoor tools: C# runtime reflection, config table queries, mod state inspection, scene GameObject tree, log capture, raycasting, screenshot capture, RNG seed control, and item injection. Use when the user needs to debug mod behaviour by reflecting over C# objects, querying game config tables like CardConfig/RelicConfig, inspecting which mods loaded, navigating the Unity scene hierarchy, capturing screen output, controlling randomness, or injecting test items. Triggers: diagnostics, debug, inspect, query_config, dump_mod_state, get_scene_tree, raycast_mouse, set_rng_seed, get_screenshot, give_item, reflection, 调试, 反射, 查配置, 日志, 截图, 场景树."
---

# Diagnostics Module

Developer backdoor tools for deep inspection and debugging. These tools break the normal game flow — use them deliberately for mod development and testing.

## Tools

| Tool | Params | Returns |
|------|--------|---------|
| `inspect` | `{typeName, memberPath?, maxDepth=3, maxItems=20}` | C# type reflection: members or member value |
| `query_config` | `{tableName?, id?, limit=5}` | Game config table listing or item query |
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
