# 部署 Mod 到游戏目录

---

## 方法 1：使用 deploy_mod 工具（推荐）

如果 MCP 工具列表中存在 `deploy_mod`：

```python
# 1. 预览要部署的变更
result = g.call("deploy_mod", {
    "source": "workspace/【MOD文件夹】/YourMod",
    "dry_run": True,
})
print(result)  # 显示新增/修改/删除的文件

# 2. 确认无误后执行
result = g.call("deploy_mod", {
    "source": "workspace/【MOD文件夹】/YourMod",
    "dry_run": False,
})
```

**`deploy_mod` 会做什么：**
1. 读取工作区下的 Mod 目录
2. 按照 .modignore 规则跳过不需要的文件
3. 复制到游戏 Mods 目录
4. 返回部署文件列表

**默认 .modignore 规则（跳过以下内容）：**
- `.git*`
- `.opencode*`
- `.agents*`
- `__pycache__/`
- `*.md`（文档文件）
- `*.py`（脚本文件）
- `*.user`, `*.suo`, `*.csproj`, `*.sln`（项目文件）
- `bin/`, `obj/`（编译输出）

---

## 方法 2：手动部署（兜底）

如果 `deploy_mod` 不可用：

```bash
# PowerShell
Copy-Item -LiteralPath "workspace/【MOD文件夹】/YourMod" `
          -Destination "F:\steam\steamapps\common\Witch's Apocalyptic Journey\Witch's Apocalyptic Journey_Data\Mods\YourMod" `
          -Recurse

# CMD
xcopy /E /I "workspace\【MOD文件夹】\YourMod" "F:\steam\steamapps\common\Witch's Apocalyptic Journey\Witch's Apocalyptic Journey_Data\Mods\YourMod"
```

---

## 部署后步骤

1. **重启游戏** — CSV/Lua 变更必须重启才能生效
2. **检查 Mod 加载** — `dump_mod_state()` 确认 Mod 出现在列表中
3. **检查错误** — `get_recent_logs(count=20)` 确认无 Error
4. **运行测试** — 执行 `tests/test_<ModName>.py`
