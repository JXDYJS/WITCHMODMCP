# 部署 Mod 到游戏目录

---

## 方法 1：使用 deploy_mod 工具（推荐）

如果 MCP 工具列表中存在 `deploy_mod`，AI 直接调用它：

```python
result = g.call("deploy_mod", {
    "mod_path": "E:/path/to/YourMod",
})
```

`deploy_mod` 会自动复制 mod 到游戏目录 → 重启游戏 → 等重连 → 检查加载状态和日志错误。

| 参数 | 必填 | 说明 |
|------|------|------|
| `mod_path` | 是 | Mod 文件夹路径（如 `E:/WitchModPlayer/Nightwatcher`） |
| `game_path` | 否 | 游戏安装路径，不传自动检测 Steam 位置 |
| `restart_delay` | 否 | 重启前等待秒数（默认 5） |

---

## 方法 2：手动部署（兜底）

如果 `deploy_mod` 不可用，AI 给用户提供复制命令建议（路径根据用户实际目录生成）：

```bash
# ⚠️ 务必先删除旧目录，否则重复执行 -Recurse 会嵌套复制（YourMod/YourMod/）
Remove-Item -LiteralPath "游戏Mods目录/YourMod" -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item -LiteralPath "path/to/YourMod" -Destination "游戏Mods目录/YourMod" -Recurse
```

---

## 部署后步骤

1. **重启游戏** — CSV/Lua 变更必须重启才能生效。AI 应直接杀启进程：
   ```powershell
   Get-Process -Name "Witch*" -ErrorAction SilentlyContinue | Stop-Process -Force
   Start-Sleep -Seconds 3
   Start-Process -FilePath "游戏安装目录/Witch's Apocalyptic Journey.exe"
   Start-Sleep -Seconds 25
   ```
2. **检查 Mod 加载** — `dump_mod_state()` 确认 Mod 出现在列表中
3. **检查错误** — `get_recent_logs(count=20)` 确认无 Error
4. **运行测试** — 执行 `tests/test_<ModName>.py`
