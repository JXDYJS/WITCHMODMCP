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

## 跨模块测试工作流

验证一个 Mod 是否工作，按以下顺序执行：

```python
# 1. 连接检查
scene = g.call("get_scene_state")
print(f"当前页面: {scene['page']}")

# 2. Mod 加载检查
state = g.call("dump_mod_state")
for m in state['mods']:
    print(m['assemblyName'])

# 3. 错误日志检查
logs = g.call("get_recent_logs", {"count": 30})
for entry in logs:
    if 'Error' in entry:
        print(f"  ERROR: {entry}")

# 4. 配置表验证
cfg = g.call("query_config", {"tableName": "CardConfig", "id": 卡牌ID})
print(cfg)

# 5. 注入卡牌测试
g.call("give_item", {"item_type": "card", "value": "卡牌ID"})

# 6. 假战斗验证
g.call("load_scene", {"type": "fakefight"})
fight = g.call("get_fight_state")
print(f"手牌: {len(fight['hand'])}")

# 7. 出牌测试
result = g.call("play_card", {"card_index": 0, "target_index": 0})
print(f"出牌结果: {result}")

# 8. 结束回合
g.call("end_turn")
```

---

## 快速诊断

| 症状 | 检查 |
|------|------|
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
