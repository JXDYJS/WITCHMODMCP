# WitchModMCP Skill 文档审查报告（绿色区域）

审查日期：2026-08-06
审查方式：只读。对照工具源码（`WitchModMCP.Contracts/Tools/*.cs`）、反编译游戏源码、游戏中实时 MCP 调用、以及模板 `Lib/DataConfigs/` 真实 CSV 表头逐条核对。
基准文件：根 `SKILL.md`、`insights/SKILL.md`（视为已人工校对）。

> 重要说明：`base/combat`、`base/diagnostics`、`insights`、`code-patterns`、`templates` 不在本次审查范围，仅作为交叉引用。

---

## 1. base/core/SKILL.md

整体准确。工具名、参数、返回结构均与源码一致。

- 无重大问题。
- L47 `reload_tools` 说明与 `ReloadToolsTool.cs` 一致（`{status:"ok", hint}`）。
- L57 `eval_command` → `ConsoleLogic.Input()`、L34 list_commands 反射 `Commands` 类，均与 `ConsoleCommandTool.cs` / `ListCommandsTool.cs` 一致。

---

## 2. base/meta/SKILL.md

整体良好，个别返回字段说明与实况有出入。

| 行 | 当前写法 | 证据 | 建议 |
|----|---------|------|------|
| L17 | `check_mode_saves` 返回 `{hasSaves, totalSaves, validSaves, saves:[{name,mode,level,career?,cardCount,relicCount}]}` | `CheckModeSavesTool.cs`：无存档时只返回 `{hasSaves:false, saves:[]}`，`totalSaves/validSaves` 仅在存在存档时出现；saves[] 还含 `createdTime/seed/hardLevel` 字段（L40-66） | 注明 `totalSaves/validSaves` 仅在无存档以外的分支出现；补充 createdTime/seed/hardLevel |
| L46 | `fightType` 说明 "e.g. `Player`, `Enemy`" | `FightType.cs` 枚举还有 `Init/Start/OtherTurn/Partner/Win/Loss/Escape` | 可提示"可取其他值" |
| L134 | `loadedModDirectories` 为返回字段 | 实时 `get_game_info` 返回**不含**此字段；但 repo 源码 `GetGameInfoTool.cs:100-107` 会条件返回 | 疑似部署版本与 repo 源码有偏差（`gameRootParent` 反而是源码有而文档未列的字段）。建议以 `list_tools` 实测字段为准复核 |
| L14 | 总览行缺 `activeUI/activeUIs/isFake/fightPlayer` | 实际返回含这些字段 | 建议补充，与 L50-52 详表保持一致 |

其余（get_game_data 字段、get_game_info 其余字段、get_recent_logs 参数、页面值、activeUI 快查表）均与源码/实况一致。

---

## 3. base/lobby/SKILL.md

基本准确。

- L33 `availablePartners` 列了 `{Id,Name,Bless,Attack,Defend,Hp,CardList}`，源码 `GetLobbyStateTool.cs:185-188` 还含 `ActionCount`。补充即可（低危）。
- L65 示例 `cardPackIds: ["pack_1",...]` 是假 ID，实际应为运行时 ID（如 `cardpack_1`、`{ModFolder}_cardpack_{id}`）。示例可接受，但建议加注。

---

## 4. base/gameflow/SKILL.md

| 行 | 当前写法 | 证据 | 建议 |
|----|---------|------|------|
| L97 | "Also closes subsequent `CardChoiceUI` / `BlessingChoiceGenerator` if they appear (skipping them)." | `ClaimRewardsTool.cs` 只处理 `BattleRewardsUI` 的 Close 按钮和 `CardChoiceUI` 的 ExitButton，**没有**处理 `BlessingChoiceGenerator` | 删除 BlessingChoiceGenerator 的说法，或注明"仅 CardChoiceUI" |
| L18 | `map_select_assign` 返回 `{result, placed, message}` | `MapSelectTool.cs` 实际还返回 `placedCount/movedCount/nullActivatedCount/errors` | 属省略，可接受 |

`map_select_confirm` "同一工具兼作开始路线与下一节点" 的说法与 `MapSelectTool.cs` 及游戏流程一致。

---

## 5. base/deck/SKILL.md

| 行 | 当前写法 | 证据 | 建议 |
|----|---------|------|------|
| L26 | 总览返回 `{isOpen, equippedCards, reserveCards, limits}` | `OutDeckTool.cs`（GetOutDeckStateTool）**无 `limits` 字段**，实际为 `cardBottomCount/cardTopCount/maxAlCardCount` | 总览表改为实际字段 |
| L46 | `equippedCards[].type` = 卡牌类型 | 工具读的是卡 CSV 的 `Type` 列（`OutDeckTool.cs:54`），但卡 CSV 没有 `Type` 列（只有 `Action`），故该字段实际不出现 | 删除或改为从 `Action` 派生 |
| L16-18 | 机制表 `CardBottomCount/CardTopCount/MaxAlCardCount` | 与 `OutDeckUIData` 字段一致 ✓ | — |

L20、L73、L94 关于 `ShowCard.MoveItem()/DecomposeItem()` 调用路径的描述与 `OutDeckTool.cs` 完全一致。L96 分解消耗公式来自游戏源生逻辑，未发现冲突。

---

## 6. devtools/SKILL.md（重点：结构性错误）

| 行 | 当前写法 | 证据 | 建议 |
|----|---------|------|------|
| L8 | "DeveloperTools 是 WitchModMCP 的扩展工具集，提供 18 个额外工具。需配合基座 WitchModMCP 同时加载使用" | 这 18 个工具（get_scene_state/enter_game/start_new_game/start_run/check_mode_saves/list_game_modes/get_fight_state/play_card/end_turn/set_card_pile/set_fight_entity/claim_rewards/get_lobby_state/set_lobby_state/raycast_mouse/get_screenshot/set_rng_seed/decompile_source）全部在**基座** `WitchModMCP.Contracts` 中实现；游戏中不存在单独的 DeveloperTools Mod，repo 内也无其源码 | "18 个额外工具"是**事实错误**。应改为"开发者工具子集文档"或说明这些工具与基座重复/来自基座 |

L14-17 工具数合计 18 与 L21-40 速查表一致，但"额外/扩展"的定位不成立。

---

## 7. devtools/skills/SKILL.md（索引页）

- 无独立错误；沿用 L6 的"DeveloperTools 模块"框架，受上面结构性错误牵连。
- L23 工作流中 `load_scene`(基座) 的标注暗示 load_scene 属基座、其余属扩展，与真实归属（全部在基座）不一致。

---

## 8. devtools/skills/combat/SKILL.md

| 行 | 当前写法 | 证据 | 建议 |
|----|---------|------|------|
| L15 | "先通过 `claim_rewards` 结束当前战斗并回到地图，再 `map_choose_node` 进入下一场战斗" | **`map_choose_node` 工具不存在**（全 repo + 网关均无）；真实流程是回到 MapSelectUI 后用 `map_select_confirm` | 改为 `map_select_confirm` |
| L214 | "然后关闭 `CardChoiceUI` 和 `BlessingChoiceGenerator`" | 同 base/gameflow L97，`claim_rewards` 只关 BattleRewardsUI + CardChoiceUI | 删除 BlessingChoiceGenerator |
| L41 | `phase` 仅 `Player`/`Enemy` | `FightType` 枚举还有 `Partner/OtherTurn/Init/...` | 加"等"或列出 |
| L43 | `turn` 说明为"当前层数" | 源码 `get_fight_state` 的 `turn = MapManager.Level`，确为层数而非回合数，文档描述与实现一致 | 建议字段名改为 level 以免误导（工具侧问题，非文档错） |

L8-16 关于"FIGHT 内再调 load_scene 导致 FightPlayer.Instance 为 null"的警告与 `EndTurnTool.cs:48-51`、`SetCardPileTool.cs:148` 注释完全吻合 ✓。`get_fight_state` 全部返回字段、`play_card`/`set_card_pile`/`set_fight_entity` 参数均与源码一致。

---

## 9. devtools/skills/gameflow/SKILL.md（内部自相矛盾，重点）

| 行 | 当前写法 | 证据 | 建议 |
|----|---------|------|------|
| L60 | "旧版曾支持 `mappings` 批量数组和 `nodeIndex`，均已移除。当前版本每次只放一个节点" | `MapSelectTool.cs:210-226` schema **仍含 `mappings` 参数**；实现只接受 1 个 mapping（L300-305）。"已移除"错误 | 改为"`mappings` 参数保留但实现仅接受单条，等效单次放置" |
| L315 | "推荐用批量 `mappings` 一次填完…比逐次调用 `map_select_assign` 更高效可靠" | 实现 `mappings.Count != 1 → error`（L300-305），批量**不可用**。与 L60、与实现三者矛盾 | 删除或改为"单次仅放 1 个" |
| L311 | "变更细节在 `changes` 字段中"（start_run 回退） | `StartRunTool.cs` 返回仅 `{result, message, page, level}`，**无 `changes` 字段** | 删除"changes 字段"说法 |
| L36 | `selectableNodes[].index` 不稳定序号 | `map_select_state` 的 selectableNodes 只有 `nodeId/id/type/note/name`，**无 index 字段**（index 是 slots 的字段） | 修正：index 不存在于 selectableNodes |

其余（页面状态机、get_scene_state 字段含 isFake、activeUI 快查表、enter_game/start_new_game/start_run 返回）均与源码一致。

---

## 10. devtools/skills/lobby/SKILL.md

- L72 示例 `cardPackIds: ["pack_1",...]` 假 ID（同上，低危）。
- 其余与 base/lobby 一致，无错误。

---

## 11. devtools/skills/diagnostics/SKILL.md

| 行 | 当前写法 | 证据 | 建议 |
|----|---------|------|------|
| L17/L112 | `decompile_source` 返回 `{status, manifestPath, dlls}`，status 为 `fresh`/`decompiled` | `DecompileSourceTool.cs`：status 实际有 `fresh/running/started/decompiled`；`dlls` 仅在 `decompiled` 分支返回（L264-275），最常见路径（fresh/started）**无 `dlls` 字段** | 补 running/started；注明 `dlls` 字段在缓存命中时不返回，示例代码 `r['dlls']['Witch.dll']['dir']` 需判空 |
| L106 | 参数仅 `outputDir, force` | schema 还有 `dlls`（自定义 DLL 列表）、`clean`（清理旧缓存） | 补充两参数 |
| L166 | "仅反编译 witch.dll 和 witch.core.dll" | 支持 `dlls` 参数反编译其他 DLL | 补充 |

get_screenshot（{mimeType,base64,width,height,size}）、raycast_mouse（含 source/isUI/canvasName/components/activeSelf 等）、set_rng_seed（{result, changes}）均与源码/实况一致。

---

## 12. gameplay/SKILL.md

整体准确，使用的工具/返回字段均可验证：

- L41 `from witch_mcp import WitchMcp` 与 `testing/witch_mcp.py` 一致 ✓
- L77-82 slots 的 `filled`/`index` 字段与 `map_select_state` 一致 ✓
- L118-120 事件 `opt['index']/['text']/['interactable']`、`event['title']` 与 `GetEventStateTool.cs` 一致 ✓
- 仅使用正常游玩工具、明确禁用 load_scene，原则正确。

无实质错误。

---

## 13. deployment/build-dll.md

- AssemblyName 必须 `ModName.ModAuthor`、运行时文件须命名为 `Entry.dll`：与真实 Mod（`Mods/FateGambler/Scripts/Entry.dll` + ModConfig ModName=FateGambler/ModAuthor=opencode）一致 ✓
- 其余为工程配置示例，无错误。

---

## 14. deployment/deploy.md

- `deploy_mod` 参数 mod_path/game_path/restart_delay 与网关 schema 一致 ✓
- 重启命令 `Get-Process -Name "Witch*"` + `Witch's Apocalyptic Journey.exe` 与真实 exe 名一致 ✓
- 无错误。

---

## 15. testing/automated-test.py（问题最多）

该脚本作为模板被 `verification.md` 与根 SKILL.md 推荐复制使用，但存在多处**必现运行时错误**：

| 行 | 当前写法 | 问题 | 证据/建议 |
|----|---------|------|----------|
| L42 | `RUN_CONFIG["mode"] = "Standard"` | 游戏模式无 "Standard" | 实时 `list_game_modes` 返回 Normal/Slot/Story/Sublimation/Teach。改为 "Normal" |
| L43 | `"career": "Witch"` | `career` 不是 `set_lobby_state` 的参数（应为 `careerId`），值 "Witch" 也非有效职业 ID | 改用 `careerId` + 有效运行时 ID |
| L177-180 | `set_lobby_state({"career":..., "confirm":True})` | `career` 与 `confirm` 均不是参数 | 应为 `{"careerId": ...}`，删 `confirm` |
| L151 | `query_config({"tableName": "CardConfig", "id": cid})` 且断言 `"item" in cfg` | ① 表名 "CardConfig" 无法解析（`query_config` 的 tableName 只按 GameConfigManager 成员名找，实时测试 `"Card"` 都返回"找不到配置表"）；② Card 真实表名非 CardConfig | 改用 `search_config` 校验运行时 ID |
| L156 | `query_config({"tableName": "BuffConfig", ...})` | 同上；且 Buff 在 DataConfigCache，`query_config` 本来就查不到 | 改用 `search_config` |
| L202 | `give_item({"item_type": "card", "value": str(cid)})` | give_item 参数是 `type`，非 `item_type` | 改 `{"type": "card", "value": ...}` |
| L222 | `play_card({"card_index": ..., "target_index": 0})` | play_card 参数是 camelCase 的 `index` / `targetIndex`，snake_case 会被忽略 | 改 `{"index":..., "targetIndex":0}` |
| L142-143 | `isinstance(l, str) and "Error" in l` | `get_recent_logs` 返回的是**对象数组**（每条含 `type/message`），不是字符串 | 改为 `l.get('message')` / `l['type']` 判断（verification.md 已用正确写法） |
| L235-237 | `test_card.get("targetHpBefore")` | `targetHpBefore` 不在手牌卡对象里，在 play_card 返回结果里 | 用 play_card 返回值 |
| L26-29 | `CARD_IDS/BUFF_IDS/PACK_ID/CAREER_ID` 示例 | 卡牌 ID 在游戏内是运行时 ID 格式，示例值 1001 等与真实不同 | 属模板占位，可接受但建议注明需用 `search_config` 核实 |

结论：该脚本按文档流程复制后无法直接通过，需先修复上述参数名与表名。

---

## 16. testing/verification.md

| 行 | 当前写法 | 证据 | 建议 |
|----|---------|------|------|
| L65/L133 | `set_lobby_state({"careerId": "career_1"})` | 参数名正确 ✓；但基座职业运行时 ID 是 `Career_1`（大写 C，`NativeIds` 可查），小写 `career_1` 可能查不到 | 值改为 `Career_1`（或用 get_lobby_state 返回的 Id） |
| L44 | load_scene 必须从 MAP 调用的警告 | 与工具行为一致 ✓ | — |
| L116-122 | `search_config` 返回 `matchCount/matchedKeys` | 与 `SearchConfigTool.cs` 一致 ✓ | — |

其余（日志排查表、症状速查表）方向正确。

---

## 17. testing/witch_mcp.py

- L23 示例 `query_config '{"tableName": "CardConfig", "limit": 3}'` 用了错误的 "CardConfig"（同上，无法解析）。低危（仅文档示例）。
- L120 `get_recent_logs(count)` 未封装 `level` 参数（工具支持），可选补充。
- 其余封装参数（inspect/query_config/search_config/load_scene/give_item）均与 schema 一致。

---

## 18. patterns/SKILL.md ⚠️（重点文件，问题最严重）

该文件的 CSV 列名大多为**臆造**，与真实表头严重不符。已用模板 `Lib/DataConfigs/Data/*.csv` 真实表头与游戏源码逐条核对。

**真实表头（证据）：**
- Card：`Id,Rarity,Expend,Tag,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action,PackBelong`
- Buff：`Id,InitScript,ApplyScript,ClearScript,ReducePerTurn,ReducePerAttacked,ReducePerUse,UpperBound,Icon,Type,Rarity,Effects,SoundEffects,Action,CanZero`
- Career：`Id,SanMax,SkillScript,Animation,Vocal,Skill1,Skill2,ChoiceIcon,DollIcon,Character,Avatar,CareerImage,ActionImage1,ActionImage2,Dialogue,EmojiPath,AttackEffect,SkillEffect,HitEffect,DefendEffect`
- Relic：`Id,Rarity,OwnScript,FightScript,Icon,PackBelong`
- Text（Card）：`Id,是否锁定,Type,Note,Name,Name_en,Name_zh-Hant,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja`

### 18.1 L26-52 "Card CSV Columns (common fields)" — 大量臆造列

| 行 | 臆造/错误列 | 证据 | 建议 |
|----|-----------|------|------|
| L30 | `Cost` | 真实为 `Expend`（能量费用） | 改为 Expend |
| L31 | `CardType` | 无此列 | 删除（编辑器有 CardType，CSV 没有） |
| L32 | `TargetType` | 无此列 | 删除 |
| L33-40 | `DamageType/Damage/Defend/Magic/Heal` | 无此列；伤害/护盾/治疗由 UseScript 内 `SetStatus/Damage/ChangeHp/ChangeDefence/AddDescription` 实现 | 删除 |
| L41-42 | `Buff/SelfBuff` | 无此列 | 删除 |
| L43-44 | `Exhaust/Ethereal` | 无此列（"消耗/虚无"由 Tag 等表达） | 删除 |
| L45 | `Rarity` 枚举 `common/uncommon/rare/special` | Rarity 是数值（insights §11.3：1=Common,2=Uncommon,3=Rare,4=Special；模板值 1/2/3） | 改为数值 |
| L46 | `PackBelong` | 列名对，但见 L47 语义 | — |
| L27-29,32 | `Name_{lang}/Description_{lang}` | 卡 CSV 无名称/描述列，它们在 Text CSV 中 | 移入 Text CSV 说明 |

### 18.2 L54-68 "Buff CSV Columns" — 臆造

`MaxLayer`(实为 UpperBound)、`isClear`(实为 ReducePerTurn/ReducePerAttacked/ReducePerUse 的 0/1)、`isDispel`、`UseScript`(实为 ApplyScript/ClearScript)、`Duration`、`LinkScript` 均不存在。`Type` 枚举 `buff/debuff/neutral` 错误——真实值为中文本地化词（如"增益/减益"，见模板 `buff.csv` 数据行）。建议整表替换为真实 15 列。

### 18.3 L70-84 "Career CSV Columns" — 臆造

`HpMax/RoleDataId/CardAsset/CardList/RelicList/PartnerList/Attribute/PackBelong` 均不在真实 Career 表头中（Career 无这些列；起始卡/随从等由 SkillScript/其他机制处理）。建议整表替换为真实 20 列。

### 18.4 其他错误

| 行 | 当前写法 | 证据 | 建议 |
|----|---------|------|------|
| L21-24 | "Name/Description columns: 4 languages = zh-Hans, zh-Hant, en, ja" | Text CSV 列名为 `Name`(即简中)、`Name_en`、`Name_zh-Hant`、`Name_ja`，**没有 `Name_zh-Hans` 列** | 修正列名 |
| L23 | "Text CSVs mirror Data CSVs structure" | Text CSV 结构不同（含是否锁定/Type/Note + 本地化列） | 改写 |
| L129 | `Direction: "row"` | 真实值为方向（如 rdl `AnimationLib/rdl/Attack/config.json` 为 `"Right"`） | 改为 "Right"/"Left" 等 |
| L130 | "Frame dimensions: 300×300 (skill animations)" | 未验证；rdl 实际帧图是 1536×640 图集 | 标注需实测 |
| L139 | Buff icon 31×31 | 实际 EdictOfStars `Icon/Buff/bloodstain.png` 为 32×32 | 改 32×32 |
| L140 | Relic icon 128×128 | 实际 JogasakiNoah `Icon/Relic/found_pen.png` 为 55×55 | 标注不确定 |
| L184 | `AddCardByCardList("1", "CardId_Here")` | 真实签名 `AddCardByCardList(string count, string tag="all")`（ScriptExecutor.cs:4331），第 2 参是**标签过滤**（检索抽牌堆），非 cardId | 修正参数语义 |
| L242-245 | `self.Vars.DesVal1 = tostring(6)` | `Vars` 是 C# `IDictionary<string,string>`（ScriptExecutor.cs:3590），xLua 须用 `self.Vars:set_Item("DesVal1",...)`，且与本文档 L251-254"xLua Limitations"自相矛盾 | 改用 `set_Item` |
| L204 | `ScriptExecutor.PlayerInfo.AddBless(DataId.blessing_1)` | 缺 `CS.` 前缀；正确写法 `CS.ScriptExecutor.PlayerInfo.AddBless("...")`（AddBless 为 PlayerInfo 上的 static 方法） | 加 `CS.` 前缀 |
| L313 | `RoleTable.Inst.PropertyChanged` | 单例属性是 `RoleTable.Instance`（RoleTable.cs:128），无 `Inst` | 改为 `RoleTable.Instance` |
| L264/L285 | `ToughCountZero`、`OnDiceValue` 事件 | `EventType.cs` 枚举中不存在这两个 | 删除 |
| L340 | `Expend` 释义 "Cards to expend (sacrifice)" | Expend 是**能量费用**（insights §11.3 / ICard.cs `SetCardCostVisual(GetValueOrDefault("Expend","0"))`） | 改为"能量费用" |
| L342 | `BaseScript` 为"列"（Required=Yes） | `BaseScript` 不是 CSV 列，是在 `InitScript` 内 `self.Vars:set_Item("BaseScript", "AttackCardItem"/"CommonCardItem")` 设置的（insights §11.3） | 移入 InitScript 说明 |
| L343 | `PackBelong` Required=Yes | 根 SKILL.md 规则 6 / insights §13.2：省略 PackBelong 即进入默认卡池，非必填 | 改为 No（默认卡池） |
| L324-354 | `Cost/CardType/TargetType/DamageType/Damage/Defend/Magic/Heal/Buff/SelfBuff/Exhaust/Ethereal/TriggerScript/ConditionScript/SoundEffects` | 均非真实 Card CSV 列（真实 12 列见上） | 整表替换 |

> 注：L260-296 "Fight Event System" 其余条目（Attack/AddEnemy/AttackDone/CostPower/NoPower/AddPower/Dead/OnEnemyDead/Resurrection/EndRound/ICreateCardItem/CreateCardItem/EndCreateCardItem/NoPowerWhenTry/Action/BurnCard/Init/OnDiceCheck/Win/Escape/StartRound/Shuffle/OnCameraMove/FightStart/Hurt/Heal/SelectCardEnd）与 `EventType.cs` 枚举一致，**仅 ToughCountZero / OnDiceValue 两项错误**。L298-307 全局事件（UIOpen-{Name}/UIHelp/UIClose-{Name}/LanguageChange）也在枚举中 ✓。

---

## 全局问题汇总

### 高危（建议优先人工复核/修复）

1. **patterns/SKILL.md 的 CSV 列名整段臆造**（Card/Buff/Career 三大表），属于"会直接误导 AI 写出错 CSV"的最高危项。建议按模板 `Lib/DataConfigs/Data/*.csv` 表头整体重写 §6 与 §"Complete Card CSV Column Reference"。
2. **devtools/skills/combat/SKILL.md L15 `map_choose_node` 是不存在的工具**（全仓库+网关均无）。
3. **`claim_rewards` 不会关闭 BlessingChoiceGenerator**——base/gameflow L97 与 devtools/skills/combat L214 两处同错（`ClaimRewardsTool.cs` 只处理 BattleRewardsUI + CardChoiceUI）。
4. **testing/automated-test.py 存在多处必现错误**（`career`/`confirm`/`item_type`/`card_index`/`target_index` 参数名错误、`CardConfig`/`BuffConfig` 表名错误、mode "Standard" 不存在），复制即用会直接失败。
5. **devtools/skills/gameflow/SKILL.md 内部自相矛盾**：L60 称 `mappings` 已移除，L315 却推荐批量 `mappings`；且实现确实只接受单条。同一文件前后打架。

### 中危（跨文件一致性问题）

6. **query_config 的 tableName 实际无法解析 `Card/Event/Enemy` 等表名**（实时测试 `"Card"` 也报"找不到配置表"，`QueryConfigTool` 按 GameConfigManager 成员名查找，而表存于私有 `_tables`）。牵连文档：automated-test.py（CardConfig/BuffConfig）、witch_mcp.py 示例、verification.md 症状表、devtools/diagnostics L155"查 CardConfig 表"。根 SKILL.md 规则 5 已正确要求"不要用 query_config 探测格式"，建议各文档补注"query_config 的 tableName 按 GameConfigManager 成员名解析，常规表名可能不可用，改用 search_config"。
7. **`set_lobby_state` 参数名**：正确为 `careerId`。root SKILL.md L159 `set_lobby_state({"career": ...})` 与 automated-test.py 均用了 `career`（基准确有笔误，建议一并复核）。
8. **`loadedModDirectories` 字段**：repo 源码有、实时 get_game_info 无；meta 文档照源码写。建议以实况为准复核（版本漂移）。
9. **`check_mode_saves` 的 `totalSaves/validSaves`** 仅在存在存档时返回，多处文档代码直接 `saves['validSaves']` 会 KeyError。
10. **"DeveloperTools 18 个额外工具"定位错误**：全部工具实为基座 WitchModMCP.Contracts 实现，无独立 DeveloperTools Mod。

### 低危（反复出现的小错误模式）

11. 示例用假卡包 ID（`pack_1..pack_6`）：base/lobby、devtools/lobby。
12. `career_1` 小写职业 ID（verification.md）vs 真实 `Career_1`。
13. `get_recent_logs` 返回对象数组被当字符串判断（automated-test.py）；verification.md 写法正确。
14. `AddCardByCardList` 第二参语义（tag 过滤）在 patterns 与 insights 中描述都不精确，patterns 更错。

### 建议复核项

- `decompile_source` 的 `dlls` 字段返回条件（fresh/started 路径不返回），根 SKILL.md "SOURCE ACCESS GATE" 与 devtools 文档都按"总是返回 dlls"描述，建议实测一次。
- patterns 动画帧尺寸（300×300）、遗物图标尺寸（128×128）与实样不符，建议按游戏实际资源核对后重写。

---

*本报告仅审查，未修改任何文件。*
