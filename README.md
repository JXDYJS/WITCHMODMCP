<div align="center">

# WitchModMCP

**MCP (Model Context Protocol) gateway for *Witch's Apocalyptic Journey* — mod development & gameplay automation**

[中文](./README.zh-CN.md)

</div>

---

WitchModMCP is a mod for *Witch's Apocalyptic Journey* that exposes an MCP interface, allowing AI agents to read game state, navigate menus, manage battles, and inspect runtime data in real time.

It is the foundational component for AI-assisted mod development and gameplay automation.

## Installation

**Option 1: Let your AI do it (recommended)**

Copy this prompt and send it to your AI tool (opencode, Claude Code, Cursor, etc.):

> Clone https://github.com/JXDYJS/WITCHMODMCP.git, read INSTALL.md, and help me install it.

Your AI will read [INSTALL.md](./INSTALL.md) and guide you through the process.

**Option 2: DIY**

Read [INSTALL.md](./INSTALL.md) and follow the steps manually.

---

### Known Issues

1. Some tools can trigger game bugs.
2. The game may show popup dialogs that the AI cannot detect.
3. Actions may be performed through popups directly.
4. Some tools return success even when the operation failed.
5. The Lua console is unfinished.
6. Only tested in single-player World Deduction mode.

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
| `select_deck_cards(indices)` | Deck | Select cards in DeckUI |
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
| `doLua(luaCode)` | Lua | Execute Lua in the game's xLua environment, with native access to any loaded C# type via the `CS` global |

**Lua Console** — an in-browser Lua REPL (CodeMirror editor + output pane) for running Lua interactively against the live game. The console page is **hosted by the MCP gateway** (http://127.0.0.1:3200/console, port via `MCP_CONSOLE_PORT`) — installing the gateway installs the console, no extra setup. The mod keeps serving a legacy xterm page at http://localhost:3100/console as a fallback. The AI can also open it for you via the `open_console` tool.
