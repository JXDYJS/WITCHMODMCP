<div align="center">

# WitchModMCP

**MCP (Model Context Protocol) gateway for the game *Witch's Apocalyptic Journey* — mod development & gameplay automation**

[中文](#zh-cn) | [English](#en)

</div>

---

## <span id="en">English</span>

WitchModMCP is a mod for *Witch's Apocalyptic Journey* (女巫/魔女/终末旅途) that exposes an MCP (Model Context Protocol) interface, allowing AI agents to read game state, navigate menus, manage battles, and inspect runtime data in real time.

It is the foundational component for AI-assisted mod development and gameplay automation.

### Architecture

```
AI Agent (opencode)
    │  MCP JSON-RPC (stdin/stdout)
    ▼
mcp_gateway/server.py          ← Python MCP stdio server
    │  HTTP JSON-RPC (localhost:3100)
    ▼
WitchModMCP (Unity Mod)         ← In-game HTTP server
    │  C# reflection & Harmony
    ▼
Witch's Apocalyptic Journey     ← Game runtime
```

### Tools

#### General Tools

Tools for navigating the game, managing runs, and playing normally.

| Tool | Scope | Description |
|------|-------|-------------|
| `get_scene_state()` | Game | Detect current page (MAIN_MENU / MODE_SELECT / LOBBY / MAP / FIGHT / HUB) and blocking modals |
| `get_game_data()` | Game | Get player snapshot: HP, SAN, money, deck summary, inventory |
| `get_game_info()` | Game | Get game install path, version, and Mods directory |
| `check_mode_saves(mode?)` | Game | Check save details for one or all game modes |
| `list_game_modes()` | Game | List all available game modes and their save status |
| `enter_game()` | Gameflow | Click "Start Game" from main menu to enter hub |
| `start_new_game(mode)` | Gameflow | Select a game mode and start a new run (enters lobby) |
| `start_run()` | Gameflow | Begin the run from lobby → map screen |
| `claim_rewards()` | Gameflow | Dismiss battle rewards (unclaimed rewards auto-convert to gold) |
| `get_lobby_state()` | Lobby | Read current career, partner, attribute allocation, active card packs |
| `set_lobby_state(careerId?, partnerId?, attributes?, cardPackIds?)` | Lobby | Configure career selection before starting a run |
| `get_fight_state()` | Combat | Full battle snapshot: player/enemy HP, hand, draw/discard piles, buffs, intents |
| `play_card(cardId?, index?, targetIndex?, choices?)` | Combat | Play a card from hand with optional target and auto-choice handling |
| `end_turn()` | Combat | End current player turn, triggers enemy actions |
| `get_skills_state()` | Combat | Get current skill cooldown and availability |
| `use_skill(index, targetIndex?, ignoreCooldown?)` | Combat | Activate a career skill in combat |
| `get_outdeck_state()` | Deck | Read equipped and reserve cards |
| `outdeck_move_card(instanceId)` | Deck | Move a card between equipped and reserve |
| `outdeck_decompose(instanceId)` | Deck | Destroy a card for gold |
| `get_deck_selection()` | Deck | List selectable cards in DeckUI |
| `select_deck_cards(indices)` | Deck | Select cards in DeckUI (e.g. for removal or transformation events) |
| `get_shop_state()` | Shop | Read shop inventory, player cards, gold, refresh count |
| `shop_buy(instanceId)` | Shop | Buy an item from the shop |
| `shop_sell(instanceId)` | Shop | Sell a card at the shop |
| `shop_refresh()` | Shop | Refresh shop inventory (costs gold) |
| `get_event_state()` | Event | Get current event ID, title, description, and option details |
| `event_choose_option(index)` | Event | Select an event option (1-based) |
| `event_advance_dialogue()` | Event | Close event and return to map |
| `map_select_state()` | Map | Read available nodes and slot assignments |
| `map_select_assign(slotIndex?, nodeId?, mappings?)` | Map | Place a node on a map slot |
| `map_select_clear(slotIndex)` | Map | Clear a node from a slot |
| `map_select_confirm()` | Map | Confirm map layout and proceed |
| `get_rewards_state()` | Rewards | List available battle rewards |
| `pick_card_reward(index)` | Rewards | Select a card reward (0-based) |
| `skip_card_reward()` | Rewards | Skip card reward choice |
| `get_blessing_state()` | Rewards | List available blessing options |
| `pick_blessing_reward(index)` | Rewards | Select a blessing (0-based) |
| `skip_blessing_reward()` | Rewards | Skip blessing choice |
| `get_safebox_state()` | SafeBox | Read safe contents and pocket items |
| `safebox_open()` | SafeBox | Open the safe box UI |
| `safebox_close()` | SafeBox | Save and close the safe box |
| `safebox_deposit(type, index)` | SafeBox | Deposit a card or relic |
| `safebox_withdraw(type, index)` | SafeBox | Withdraw a card or relic |
| `safebox_deposit_money()` | SafeBox | Deposit gold (max 100 per use) |
| `safebox_withdraw_money()` | SafeBox | Withdraw gold (max 200 per use) |

#### Developer Tools

Tools for mod developers to inspect runtime state, debug mod behavior, and test game mechanics.

| Tool | Scope | Description |
|------|-------|-------------|
| `ping()` | Core | Verify gateway process is alive |
| `list_tools()` | Core | List all currently loaded MCP tools with schemas |
| `list_commands()` | Core | List all game debug console commands |
| `eval_command(command)` | Core | Execute any game console command |
| `reload_tools()` | Core | Hot-reload MCP tool DLLs without restarting the game |
| `deploy_mod(mod_path, game_path?, restart_delay?)` | Core | Deploy a mod folder to the game's Mods directory and restart |
| `inspect(typeName, memberPath?, maxDepth?, maxItems?)` | Diagnostics | Reflect over any C# type or instance at runtime |
| `query_config(tableName?, id?, limit?)` | Diagnostics | Query game config tables (CardConfig, EnemyConfig, etc.) |
| `search_config(pattern, limit?, includeFields?, searchNativeIds?)` | Diagnostics | Fuzzy-search all loaded config data by keyword |
| `dump_mod_state()` | Diagnostics | List all loaded mods and their assemblies |
| `get_env_info()` | Diagnostics | Scan MCPSkillNamespace attributes on all loaded assemblies |
| `get_scene_tree(rootName?, maxDepth?, maxChildren?, includeComponents?, includeInactive?)` | Diagnostics | Dump the Unity scene GameObject hierarchy |
| `get_recent_logs(count?, level?)` | Diagnostics | Tail recent game logs with level filtering |
| `give_item(type, value)` | Diagnostics | Spawn any item: cards, relics, blessings, gold, stats, etc. |
| `get_screenshot(format?, quality?)` | Diagnostics | Capture game window screenshot (base64) |
| `raycast_mouse(screenX?, screenY?, maxResults?)` | Diagnostics | Raycast from mouse or screen position to identify objects |
| `set_rng_seed(seed?, forceRng?)` | Diagnostics | Force RNG seed for reproducible testing |
| `get_modal_state()` | Diagnostics | Detect and read active modal dialogs |
| `scan_ui(panel?, includeInactive?, interactableOnly?)` | Diagnostics | Scan all interactable UI elements with hierarchy paths |
| `click_ui(instanceId?, index?, allowInactive?)` | Diagnostics | Click a UI element by instanceId or index |
| `decompile_source(outputDir, dlls?, force?, clean?)` | Diagnostics | Decompile game DLLs for source inspection (cached) |
| `load_scene(type, id?)` | Debug | Jump to any event, fight, or fake-fight scene |
| `set_card_pile(pile, action, cards?, indices?, shuffle?)` | Debug | Manipulate hand, draw pile, discard pile, or exhaust pile |
| `set_fight_entity(instanceId?, target?, hp?, shield?, power?, buffs?, ...)` | Debug | Modify any combat entity's attributes and buffs |

### Installation

> Coming soon — installation instructions are being finalized.

---

## <span id="zh-cn">中文</span>

WitchModMCP 是《女巫/魔女/终末旅途》（Witch's Apocalyptic Journey）的一个 Mod，通过 MCP（Model Context Protocol）接口暴露游戏运行时状态，让 AI 代理能够实时读取游戏状态、导航菜单、管理战斗和检查运行时数据。

它是 AI 辅助 Mod 开发和游戏自动化基础设施的核心组件。

### 架构

```
AI 代理 (opencode)
    │  MCP JSON-RPC (stdin/stdout)
    ▼
mcp_gateway/server.py          ← Python MCP stdio 服务器
    │  HTTP JSON-RPC (localhost:3100)
    ▼
WitchModMCP (Unity Mod)         ← 游戏内 HTTP 服务器
    │  C# 反射 & Harmony
    ▼
Witch's Apocalyptic Journey     ← 游戏运行时
```

### 工具

#### 通用工具

用于正常游玩游戏、管理跑局和导航的工具。

| 工具 | 分类 | 说明 |
|------|------|------|
| `get_scene_state()` | 游戏 | 检测当前页面（主菜单/模式选择/大厅/地图/战斗/中枢）及弹窗阻挡 |
| `get_game_data()` | 游戏 | 获取玩家快照：生命、SAN、金钱、牌组概况、背包 |
| `get_game_info()` | 游戏 | 获取游戏安装路径、版本号、Mods 目录 |
| `check_mode_saves(mode?)` | 游戏 | 查询一个或全部模式的存档详情 |
| `list_game_modes()` | 游戏 | 列出所有可用游戏模式及存档状态 |
| `enter_game()` | 流程 | 从主菜单点击"开始游戏"进入小屋 |
| `start_new_game(mode)` | 流程 | 选择模式并开新局（进入职业大厅） |
| `start_run()` | 流程 | 在大厅点击"启程"进入地图界面 |
| `claim_rewards()` | 流程 | 关闭战斗奖励（未领取奖励自动转金钱） |
| `get_lobby_state()` | 大厅 | 读取当前职业、随从、属性加点、卡包配置 |
| `set_lobby_state(careerId?, partnerId?, attributes?, cardPackIds?)` | 大厅 | 配置大厅选人状态 |
| `get_fight_state()` | 战斗 | 完整战斗快照：双方属性、手牌、抽/弃牌堆、Buff、AI 意图 |
| `play_card(cardId?, index?, targetIndex?, choices?)` | 战斗 | 打出手牌（支持自动处理选牌弹窗） |
| `end_turn()` | 战斗 | 结束当前回合，触发敌方行动 |
| `get_skills_state()` | 战斗 | 查看职业技能冷却状态 |
| `use_skill(index, targetIndex?, ignoreCooldown?)` | 战斗 | 在战斗中释放职业技能 |
| `get_outdeck_state()` | 牌组 | 查看装备中和备选卡牌 |
| `outdeck_move_card(instanceId)` | 牌组 | 移动卡牌（装备↔备选） |
| `outdeck_decompose(instanceId)` | 牌组 | 分解卡牌获得金钱 |
| `get_deck_selection()` | 牌组 | 获取 DeckUI 可选卡牌列表 |
| `select_deck_cards(indices)` | 牌组 | 在 DeckUI 中选择卡牌 |
| `get_shop_state()` | 商店 | 查看商店商品、玩家卡牌、金钱和刷新次数 |
| `shop_buy(instanceId)` | 商店 | 购买指定商品 |
| `shop_sell(instanceId)` | 商店 | 出售卡牌 |
| `shop_refresh()` | 商店 | 刷新商店商品（消耗金钱） |
| `get_event_state()` | 事件 | 获取当前事件 ID、标题、描述和所有选项 |
| `event_choose_option(index)` | 事件 | 选择事件选项（从 1 开始） |
| `event_advance_dialogue()` | 事件 | 结束事件返回地图 |
| `map_select_state()` | 地图 | 查看可选节点和槽位填充情况 |
| `map_select_assign(slotIndex?, nodeId?, mappings?)` | 地图 | 将节点放置到指定槽位 |
| `map_select_clear(slotIndex)` | 地图 | 清空指定槽位 |
| `map_select_confirm()` | 地图 | 确认编排并继续前进 |
| `get_rewards_state()` | 奖励 | 查看可领取的战斗奖励列表 |
| `pick_card_reward(index)` | 奖励 | 选择卡牌奖励（从 0 开始） |
| `skip_card_reward()` | 奖励 | 跳过选卡奖励 |
| `get_blessing_state()` | 奖励 | 查看祝福选项详情 |
| `pick_blessing_reward(index)` | 奖励 | 选择祝福（从 0 开始） |
| `skip_blessing_reward()` | 奖励 | 跳过祝福选择 |
| `get_safebox_state()` | 保险箱 | 查看保险箱和背包物品 |
| `safebox_open()` | 保险箱 | 打开保险箱界面 |
| `safebox_close()` | 保险箱 | 保存并关闭保险箱 |
| `safebox_deposit(type, index)` | 保险箱 | 存入卡牌或遗物 |
| `safebox_withdraw(type, index)` | 保险箱 | 取出卡牌或遗物 |
| `safebox_deposit_money()` | 保险箱 | 存入金钱（每次最多 100） |
| `safebox_withdraw_money()` | 保险箱 | 取出金钱（每次最多 200） |

#### 开发工具

用于 Mod 开发者检查运行时状态、调试 Mod 行为和测试游戏机制的工具。

| 工具 | 分类 | 说明 |
|------|------|------|
| `ping()` | 核心 | 验证网关进程是否存活 |
| `list_tools()` | 核心 | 列出所有已加载的 MCP 工具及其 schema |
| `list_commands()` | 核心 | 列出所有游戏控制台调试命令 |
| `eval_command(command)` | 核心 | 执行任意游戏控制台命令 |
| `reload_tools()` | 核心 | 热重载 MCP 工具 DLL，无需重启游戏 |
| `deploy_mod(mod_path, game_path?, restart_delay?)` | 核心 | 部署 Mod 到游戏目录并重启验证 |
| `inspect(typeName, memberPath?, maxDepth?, maxItems?)` | 诊断 | 在运行时反射任意 C# 类型或实例 |
| `query_config(tableName?, id?, limit?)` | 诊断 | 查询游戏配置表（CardConfig, EnemyConfig 等） |
| `search_config(pattern, limit?, includeFields?, searchNativeIds?)` | 诊断 | 模糊搜索所有已加载的配置数据 |
| `dump_mod_state()` | 诊断 | 列出所有已加载的 Mod 及其程序集 |
| `get_env_info()` | 诊断 | 扫描所有程序集的 MCPSkillNamespace 特性 |
| `get_scene_tree(rootName?, maxDepth?, maxChildren?, includeComponents?, includeInactive?)` | 诊断 | 导出 Unity 场景 GameObject 层级树 |
| `get_recent_logs(count?, level?)` | 诊断 | 查看最近游戏日志（支持按级别过滤） |
| `give_item(type, value)` | 诊断 | 给予任何物品：卡牌、遗物、祝福、金钱、属性等 |
| `get_screenshot(format?, quality?)` | 诊断 | 截取游戏画面（base64） |
| `raycast_mouse(screenX?, screenY?, maxResults?)` | 诊断 | 从鼠标/屏幕位置发射射线识别物体 |
| `set_rng_seed(seed?, forceRng?)` | 诊断 | 设置随机种子用于可重现测试 |
| `get_modal_state()` | 诊断 | 检测并读取当前弹窗内容 |
| `scan_ui(panel?, includeInactive?, interactableOnly?)` | 诊断 | 扫描所有可交互 UI 元素及层级路径 |
| `click_ui(instanceId?, index?, allowInactive?)` | 诊断 | 按 instanceId 或索引点击 UI 元素 |
| `decompile_source(outputDir, dlls?, force?, clean?)` | 诊断 | 反编译游戏 DLL 用于源码查询（缓存） |
| `load_scene(type, id?)` | 调试 | 跳转到任意事件、战斗或假战斗场景 |
| `set_card_pile(pile, action, cards?, indices?, shuffle?)` | 调试 | 控制手牌、抽牌堆、弃牌堆、消耗堆 |
| `set_fight_entity(instanceId?, target?, hp?, shield?, power?, buffs?, ...)` | 调试 | 修改任意战斗实体的属性和 Buff |

### 安装

> 安装说明正在整理中，即将补充。

---

<div align="center">
MIT License — see [LICENSE](./LICENSE)
</div>
