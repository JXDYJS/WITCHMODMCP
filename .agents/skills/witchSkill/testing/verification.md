# 验证清单与故障排查

---

## 部署前检查清单

### ModConfig.json
- [ ] `ModName` 与文件夹名一致
- [ ] `ModAuthor` 已填写
- [ ] `ModVersion` 已设置
- [ ] `Enabled` 为 `true`

### Data CSV
- [ ] 第 1 行是列名（表头）
- [ ] 第 2 行是 `#` 开头的注释行
- [ ] `Id` 列在文件内唯一
- [ ] ID 不与游戏保留范围 (1-5000) 冲突
- [ ] 所有 Card/Buff/Relic 的 `PackBelong` 指向存在的 CardPack
- [ ] 所有 Card 的 `BaseScript` 非空（`AttackCardItem` 或 `CommonCardItem`）
- [ ] 所有 Lua 脚本列使用冒号 `self:xxx()` 而非点 `self.xxx()`

### Text CSV
- [ ] 每个 Data CSV 条目都有对应的 Text CSV 条目
- [ ] `{0}`~`{3}` 占位符与 `InitScript` 中的 `DesVal1`~`DesVal4` 匹配

### 部署
- [ ] 文件已复制到游戏 `Mods/` 目录
- [ ] 游戏 Mod 管理器中已启用本 Mod

---

## 测试验证原则

**编写 Mod 后必须用 MCP 工具验证，不能只靠人肉检查。**

### 方式一：AI 直接用 MCP Tools 验证（推荐）
这是最快捷的方式——AI 完成代码后立即调工具验证：
1. `search_config` 确认数据已加载到 `DataConfigCache`
2. `get_recent_logs` 检查有无 CSV/Lua 错误
3. `enter_game` → `start_new_game` → `get_lobby_state` 确认卡包可见
4. `start_run` → `load_scene fakefight` → `give_item card <id>` 注入测试
5. `get_fight_state` → `play_card` 验证卡牌效果

> **⚠️ 调试 `load_scene` 注意事项：`load_scene` 必须从 MAP 页面调用（`start_run` 后自动进入 MAP）。
> 不要在 FIGHT 页面内再次调用 `load_scene`，否则会破坏战斗状态（`FightPlayer.Instance` 变 null）。**

### 方式二：编写 Python 测试脚本
复制 `[skill]/testing/witch_mcp.py` 到工作区根目录，然后：

```python
# test_my_mod.py
from witch_mcp import WitchMcp
import sys

mcp = WitchMcp()

def test_data_loaded():
    r = mcp.search_config("MyMod")
    assert r["matchCount"] > 0, f"MyMod data not loaded! Got {r['matchCount']} matches"
    print(f"✅ Data loaded: {r['matchCount']} entries")

def test_card_injectable():
    mcp.call("enter_game")
    mcp.call("start_new_game", {"mode": "Normal", "useExistingSave": False})
    mcp.call("set_lobby_state", {"careerId": "career_1"})
    mcp.call("start_run")
    mcp.call("load_scene", {"type": "fakefight"})
    r = mcp.call("give_item", {"type": "card", "value": "MyMod_CsvFile_CardId"})
    print(f"✅ Card injectable: {r}")

if __name__ == "__main__":
    test_data_loaded()
    test_card_injectable()
```

运行：`python test_my_mod.py`

> **关于 `witch_mcp.py`：** 它位于 skill 目录 `testing/` 下。复制到工作区根目录后即可 import 使用，连接 `http://localhost:3100/`，无需权鉴。支持 `--port` CLI 参数。

---

## 调试第一原则：读日志

**任何 Mod 加载/数据问题，第一步永远是 `get_recent_logs`。** 游戏会在日志中打印明确的错误信息：

| 问题 | 日志特征 |
|------|---------|
| CSV 列名错误 | `[Mod] Data/xxx.csv 解析失败: 未找到列 YYY` |
| Lua 编译错误 | `[Lua] xxx.lua: line N: syntax error` |
| ModConfig 错误 | `[Mod] ModConfig.json 解析失败: ...` |
| 缺少 BaseScript | `[Mod] 卡牌 xxx 缺少 BaseScript` |
| PackBelong 无效 | `[Mod] 卡包 xxx 不存在` |
| Mod 未加载 | 没有 `[Mod] 已加载: YourMod.YourAuthor` 行 |

只有在日志完全干净的情况下，才考虑用 `inspect`/`query_config` 查更深层状态。

## 跨模块测试工作流

验证一个 Mod 是否工作，按以下顺序执行：

```python
# 1. 连接检查
scene = g.call("get_scene_state")
print(f"当前页面: {scene['page']}")

# 2. 读日志（首要调试手段！）
logs = g.call("get_recent_logs", {"count": 100})
for entry in logs:
    m = entry.get('message', '')
    if any(kw in m for kw in ['[Mod]', 'Error', 'CSV', 'Lua', 'YourModName']):
        print(f"  [{entry['type']}] {m}")

# 3. 如果有错误 → 修复后重启游戏 → 再回来验证

# 4. 用 search_config 确认数据加载
loaded = g.call("search_config", {"pattern": "YourModFolder"})
if loaded['matchCount'] == 0:
    print("⚠️ 数据未加载！检查 CSV 格式或日志错误")
else:
    print(f"✅ 已加载 {loaded['matchCount']} 条数据")
    for key in loaded['matchedKeys']:
        print(f"  {key}")

# 5. 无错误 → 进入游戏验证
g.call("enter_game")
g.call("start_new_game", {"mode": "Normal", "useExistingSave": False})

# 6. 大厅检查卡包是否可见
lobby = g.call("get_lobby_state")
print("可用卡包:", [p['id'] for p in lobby['cardPacks']['available']])

# 7. 启程进战斗注入卡牌测试
g.call("set_lobby_state", {"careerId": "career_1"})
g.call("start_run")  # 进入 MAP 页面
g.call("load_scene", {"type": "fakefight"})  # ⚠️ 确保从 MAP 调用，不要在 FIGHT 中再调
g.call("give_item", {"type": "card", "value": "YourMod_CsvFile_CardId"})
fight = g.call("get_fight_state")
print(f"手牌: {len(fight['hand'])}")

# 8. 出牌测试
result = g.call("play_card", {"index": 0, "targetIndex": 0})
print(f"出牌结果: {result}")
```

---

## 快速诊断

### 第一步：读日志（`get_recent_logs({"count": 100})`）

找 `[Mod]`、`Error`、`CSV`、`Lua` 关键词。如果是 CSV 加载问题，日志里直接有报错行号。

| 症状 | 日志搜索关键词 | 修复 |
|------|---------------|------|
| Mod 没有加载 | 搜 `你的Mod名` | 检查 ModConfig.json `Enabled: true`、文件夹名是否匹配 |
| CSV 加载失败 | 搜 `CSV`、`解析`、`fail` | 检查列名是否正确（对照模板 `Lib/DataConfigs/`） |
| Lua 错误 | 搜 `Lua`、`Error` | 检查脚本列语法，确认用冒号 `self:` 而非点 |
| 卡包不存在 | 搜 `PackBelong`、`卡包` | 确认 `PackBelong` 填的是运行时 ID |
| 缺少 BaseScript | 搜 `BaseScript` | 添加 `AttackCardItem` 或 `CommonCardItem` |

### 症状速查表（日志优先）

| 症状 | 检查 |
|------|------|
| `search_config` 搜 Mod 名得到 0 条 | CSV 未加载，检查 `get_recent_logs` 中的 CSV 解析错误 |
| `dump_mod_state` 找不到 Mod | ModConfig.json `Enabled=false` |
| 日志显示 "ModConfig.json parse failed" | JSON 语法错误 |
| `query_config` 查不到条目 | CSV 在错误的 Data/ 子目录下 |
| 游戏内看不到卡牌 | `PackBelong` 未设置或指向不存在的 CardPack |
| 卡牌无名 | 缺少 Text CSV |
| 卡牌无法打出 | `BaseScript` 未设置 |
| 卡牌显示 "?" 图标 | `Icon` 路径错误或图片不存在 |
| 日志显示 Lua 编译错误 | 脚本列 Lua 语法错误 |
| 脚本不执行 | 列名不包含 "Script" |
| `self.AddBuff` 报错 | 用点调用，应该用冒号 `self:AddBuff()` |
| `dict[key]` 失败 | xLua 不支持，用 `dict:get_Item(key)` |
| `CS.xxx` 为 nil | 该类型未导出到 xLua |
| C# DLL 不加载 | Assembly name 是 `Entry`，应该改成 `ModName.ModAuthor` |

---

## 常见错误与修复

### "ModId 冲突"
另一个 Mod 使用了相同的 `ModName.ModAuthor` → 修改 `ModName`。

### "依赖错误"
`Dependencies` 中的 Mod 不存在或未启用 → 检查依赖是否正确。

### "BaseScript 未设置"
Card CSV 缺少 `BaseScript` 列 → 必须指定 `AttackCardItem` 或 `CommonCardItem`。

### "Icon 找不到"
`Icon` 列填写的路径不带 `.png` 后缀，实际文件在 `ModResource/Images/` 或 `ModResource/Icon/`。

### "C# DLL 方法找不到"
游戏版本更新可能导致某些类型/方法名变化 → 用 `inspect` 工具检查实际类型名。
