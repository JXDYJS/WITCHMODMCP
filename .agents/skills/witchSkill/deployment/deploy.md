# 部署 Mod 到游戏目录

---

## 方法 1：使用 deploy_mod 工具（推荐）

如果 MCP 工具列表中存在 `deploy_mod`，AI 直接调用它：

```python
result = g.call("deploy_mod", {
    "source": "path/to/YourMod",
    "dry_run": True,
})
```

`deploy_mod` 会根据自身规则决定怎么部署，不需要 AI 操心底层路径。

---

## 方法 2：手动部署（兜底）

如果 `deploy_mod` 不可用，AI 给用户提供复制命令建议（路径根据用户实际目录生成）：

```bash
Copy-Item -LiteralPath "path/to/YourMod" -Destination "游戏Mods目录/YourMod" -Recurse
```

---

## 部署后步骤

1. **重启游戏** — CSV/Lua 变更必须重启才能生效
2. **检查 Mod 加载** — `dump_mod_state()` 确认 Mod 出现在列表中
3. **检查错误** — `get_recent_logs(count=20)` 确认无 Error
4. **运行测试** — 执行 `tests/test_<ModName>.py`
