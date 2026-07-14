# 魔女之灾 Mod 开发百科全书

> 适用环境：opencode（自动注入）、Claude Desktop（拖入 context）、Cursor（.cursorrules）、
> Windsurf（.windsurfrules）、或其他支持 markdown 参考的 AI 工具。
> 如果 MCP 网关未运行，部分验证步骤需手动执行。

---

## 快速入门（3 步）

```
1. 克隆模板仓库
   git clone https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git

2. 复制对应模板，重命名
   Lua Mod:   复制 ModTemplate/ → 你的Mod名
   C# DLL:    复制 DllTemplate/ → 你的Mod名
   文件夹名必须和 ModConfig.json 中 ModName 一致

3. 编辑 ModConfig.json，至少填写 ModName / ModAuthor / ModVersion
   启用：设置 "Enabled": true
```

---

## 核心规则

### 1. ID 命名空间

运行时 ID 自动生成为：`{Mod文件夹名}_{Csv文件名}_{原始Id}`

例如：`EdictOfStars_starcards_1001`

这意味着**不同 Mod 的文件可以有相同的原始 Id**，游戏通过前缀区分。

### 2. PackBelong 必须指向存在的 CardPack

每个 Card/Buff/Relic 的 `PackBelong` 列必须在 Data/CardPack/ 中有对应条目。
CardPack 也需要 Text CSV 才能显示名称。

### 3. 必须有 Text CSV

只写 Data CSV 不写 Text CSV → 游戏内显示空白名称。
Text CSV 镜像 Data CSV 结构，提供 4 语言（zh-Hans / zh-Hant / en / ja）。

### 4. BaseScript 必填

Card CSV 必须指定 `BaseScript` 列：
- `AttackCardItem`：可指定目标（造成伤害用）
- `CommonCardItem`：无目标（给自己加 Buff / 全局效果）

### 5. 第 2 行是注释

所有 CSV 的第 2 行自动被游戏忽略（推荐写 `#` 开头的字段说明）。

### 6. `*` 前缀排除随机池

Id 以 `*` 开头的卡牌不会出现在随机池中，只能通过特定手段获得。

### 7. Lua 用冒号调用方法

```lua
-- 正确
self:AddBuff(DataId.buff_bleeding, "3")
-- 错误：self.AddBuff(id, level) 在 xLua 中不工作
```

### 8. xLua 无法用 `[]` 访问字典

```lua
-- 正确
local val = myDict:get_Item("key")
myDict:set_Item("key", "value")
-- 错误
-- local val = myDict["key"]
```

### 9. C# 类型用 `CS.` 前缀

```lua
CS.UnityEngine.Debug.Log("[MyMod] message")
CS.Commands.Log("MyMod", "message")
```

### 10. 部署后必须重启游戏

所有 CSV/Lua 变更需要重启游戏才能生效。
只有 C# DLL 改动可以用 `reload_tools` 热重载。

---

## 工作流路由表

当你的意图匹配以下情况时，查阅对应子目录：

| 意图 | 查阅 |
|------|------|
| 开始一个新 Mod，需要模板 | `templates/using-templates.md` |
| 想知道一个完整职业 Mod 长什么样 | `templates/reference-example.md` |
| 写卡牌 Lua 逻辑（Buff/伤害/抽牌） | `code-patterns/buff-as-resource.md`、`code-patterns/cooldown-dice.md` |
| 写 Entry.lua 入口 | `code-patterns/entry-patterns.md` |
| 做一个完整职业（含动画、圣物、卡牌） | `code-patterns/career-mod.md` |
| 做 C# Hook Mod | `code-patterns/entry-patterns.md`（C# 部分） |
| 写测试脚本验证 Mod | `testing/automated-test.py`、`testing/verification.md` |
| 部署 Mod 到游戏目录 | `deployment/deploy.md` |
| 编译 C# DLL | `deployment/build-dll.md` |
| 调试 Mod 加载失败 | `testing/verification.md`（故障排查） |

---

## MCP 工具使用路由

当你想用 MCP 工具操作游戏时：

| 意图 | 调用工具 |
|------|---------|
| "游戏当前在哪个页面？" | `get_scene_state` |
| "玩家血量/金钱/牌组？" | `get_game_data` |
| "有哪些控制台命令？" | `list_commands` → `eval_command` |
| "我编译了 C# 工具" | `reload_tools` → `list_tools` |
| "我要钱/卡牌/圣物" | `give_item` |
| "跳到 BOSS 战" | `load_scene({"type": "fight", "id": "boss"})` |
| "查卡牌配置" | `query_config({"tableName": "CardConfig", "id": 1001})` |
| "查服务器信息" | `list_tools` |
| "哪个 Mod 加载了？" | `dump_mod_state` |
| "看场景内对象" | `get_scene_tree` |
| "看最新日志" | `get_recent_logs({"count": 30})` |
| "出牌打敌人" | `get_fight_state` → `play_card` |
| "结束回合" | `end_turn` |
| "配置大厅" | `set_lobby_state` |
| "开始新对局" | `enter_game` → `start_new_game` → `set_lobby_state` → `start_run` |
| "鼠标指到哪个 UI 了？" | `raycast_mouse` |
| "截个图" | `get_screenshot` |

---

## 工具调用最佳实践

1. **`list_tools` 是真理源头** — 每次连线先调 `list_tools`，确认工具有哪些
2. **先读后写** — 用读工具理解当前状态，再用写工具修改
3. **每次调用会短暂阻塞游戏主线程** — 参数设限控制返回大小
4. **每次修改后重新读取** — 确认变更已生效
5. **优先使用专用工具而非 `eval_command`** — `give_item` 比 `eval_command("give ...")` 结构化更好

---

## 此 Skill 的目录结构

```
.agents/skills/witchSkill/
  SKILL.md                           ← 本文件：核心规则 + 路由索引

  templates/                         ← 模板创建
    using-templates.md                 模板仓库使用指南
    reference-example.md               完整示例 Mod 参考（Defect）

  code-patterns/                     ← 代码编写（从真实 Mod 提炼的抽象模式）
    buff-as-resource.md                 Buff 当资源用（黑魔/妹红/麻将）
    card-transform.md                   卡牌转换 + 伴星系统
    cooldown-dice.md                    冷却/骰子/里程碑/相位循环
    entry-patterns.md                   Entry.lua 三种模式 + C# Hook 模式
    career-mod.md                       完整职业 Mod 架构

  testing/                           ← 测试
    automated-test.py                   真实可运行的自动化测试脚本
    verification.md                     验证清单 + 跨模块工作流 + 故障排查

  deployment/                        ← 部署
    deploy.md                           部署工具 + 手动复制步骤
    build-dll.md                        C# 编译流水线
```
