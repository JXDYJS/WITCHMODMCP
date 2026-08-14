# WitchModMCP Skill 文档审查报告（红色区域：combat / diagnostics / code-patterns）

审查日期：2026-08-07
审查方式：只读。对照本轮最新反编译源码（`E:\Witch\game_src\88613cf3…` = Witch.dll、`50f526f3…` = Witch.Core.dll，hash 与旧版 8d8769…/ca6e93… 不同，确认旧反编译已过期）、游戏 Mods 目录（`F:\steam\…\Witch's Apocalyptic Journey_Data\Mods`）下 22 个真实 Mod 的 `Entry.lua`/CSV、以及游戏实时 MCP 调用逐条核对。
证据级别：`[源码]`=游戏源码、`[真Mod]`=真实 Mod 文件、`[实况]`=运行时 MCP 调用。

> 与 2026-08-06 报告互补：该报告审查了绿色区域（base/core、meta、lobby、gameflow、deck、devtools、patterns、testing、deployment 等）。本次范围 = `base/combat`、`base/diagnostics`、`code-patterns/`（5 文件）。报告中标注「与上一份报告同错」的行表示 08-06 报告已记录过相同问题。

---

## 1. base/combat/SKILL.md

整体质量高。`claim_rewards` 那处（L16）已修对，与 `ClaimRewardsTool.cs` 完全一致（只处理 BattleRewardsUI 的 Close 按钮 + CardChoiceUI 的 ExitButton，不结束进行中的战斗）。`load_scene` 在 FIGHT 中导致 `FightPlayer.Instance` 为 null 的警告也与 `EndTurnTool.cs:48-51`、`SetCardPileTool.cs:148` 注释吻合。所有工具参数与 `InputSchema` 一致。

| 行 | 当前写法 | 证据 | 建议 |
|----|---------|------|------|
| L42 | `phase` 仅 "`Player` / `Enemy` turn" | `[源码]` `get_fight_state` 的 phase = `FightManager.fightType.ToString()`；`FightType.cs` 枚举含 `Init/Start/Player/Partner/Enemy/OtherTurn/Win/Loss/Escape` | 加"等"或注明可能取其他值 |
| L44 | `turn` 说明为 "Current level/turn number" | `[源码]` `result["turn"] = MapManager.Instance?.Level ?? 0`，是地图层数/关卡序号，不是回合数 | 改为"地图层数（MapManager.Level）"，避免误导 |
| L45 | player 返回字段 `{hp, maxHp, shield, power, maxPower, isDead, buffs}` | `[源码]` 还有 `instanceId`，而 L232-258 的 `set_fight_entity` 示例正是 `fight["player"]["instanceId"]` | 字段表补 `instanceId`（enemies 同样漏了，L46） |
| L24 | `use_skill` 返回 `{result, skillRuntimeId, skillName, player?}` | `[源码]` 还返回 `skillRawId`、`targetIndex` | 补 `skillRawId`（低危） |
| L23 | `play_card` 返回 `{result, cardId, handBefore, handAfter, targetHpBefore?, targetHpAfter?}` | `[源码]` 还返回 `message`、`targetIndex`、`discardedCount`、`autoConfirmed` | 补全（低危） |
| L25 | `get_skills_state` 返回 `{careerId, careerName, skills:[…]}` | `[源码]` 还有 `inFight`、`skillCount`；skills 每项含 `rawId`、`actionImage` | 补全（低危） |
| L29 | `get_deck_selection` cards[].cost | `[源码]` `DeckSelectionTool.cs` 读卡数据的 `"Cost"` 键，但卡 CSV 费用列是 `Expend`（见下 patterns 部分），实际恒为 `"0"` | 属工具侧行为；文档可注明 cost 由 Expend 派生不可靠 |
| L8 | "All tools require the game to be in a fight" | 但 `get_skills_state` 自身描述「无需在战斗中也能查看职业技能信息」，`get_deck_selection` 只需 DeckUI 打开 | 改为"除 get_skills_state 外均需战斗中" |

L16 claim_rewards 措辞、L110-123 技能选牌模态、L205-211 set_card_pile 各牌堆 action 表（含 exhaust 无 set 列为 "—"）均与源码一致 ✓。

---

## 2. base/diagnostics/SKILL.md

核心工具（inspect / query_config / search_config / dump_mod_state / get_recent_logs / raycast_mouse / set_rng_seed / get_screenshot / give_item / scan_ui / click_ui / get_modal_state）的参数与源码、实况一致。`query_config({"tableName":"_tables"})` 实况可用，返回 `Dictionary<DataType, GameConfigData>` 的样本 ✓。

| 行 | 当前写法 | 证据 | 建议 |
|----|---------|------|------|
| L32 | DataConfigCache "~2183 entries" | `[实况]` `search_config` 返回 `totalCacheSize=2355` | 更新数字（会随 Mod 增长，建议写"运行时 ~2.3k"或标注以实况为准） |
| L32 / L149 | NativeIds "~1723" | `[实况]` `totalNativeIds=1825` | 同上 |
| L31 | `_tables` "（34 tables）" 且列 `Card, Event, Map, Enemy, EnemyCard, Level, Partner, PartnerCard` | `[源码]` `DataType.cs` 枚举 35 个值；`[实况]` `_tables` 实为全部已加载 DataType 的字典（含 Buff/Career/Relic/Bless/CardPack/Item 等），不只这 8 张 | 表数改为 35（或"全部已加载 DataType"）；不要暗示 Career/Buff 不在 `_tables` 中 |
| L110 | "Career, Buff, Relic, Blessing 等数据不在 `_tables` 中" | `[源码]` DataType 含 `Buff=10, Career=11, Relic=12, Bless=13, CardPack=30`，均在 `_tables` 字典键中 | 措辞改为"这些表难以用 query_config 按名定位，统一用 search_config 查运行时 ID"（结论不变，依据修正） |
| L52 | 工具表 `click_ui` 参数仅 `{index, allowInactive=false}` | `[源码]` `ClickUITool.InputSchema` 是 `{instanceId（推荐）, index, allowInactive}` | 工具表补 `instanceId`（详表 L391-397 已正确） |
| L51 | `scan_ui` elements 返回 `{index, text, type, interactable, hierarchy, panel}` | `[源码]` `ScanUITool.cs` 每元素还有 `instanceId`，而文档自己强调 instanceId 优先 | 该行补 `instanceId` |
| L44 | `get_modal_state` 返回 `{hasModal, title?, description?, buttons?}` | `[源码]` 还有 `gameObjectName`、`mustChoose`、`showConfirm`、`showCancel` | 补全（低危） |
| L324 | `give_item` type 列表（money/san/maxsan/…/win） | `[源码]` GiveItemTool 直通 `Commands.give`，工具 Description 列了更多：`timecount, randomcardbydeck, randombless, goodbless, randomrelicByRarity, randomcardByRarity, def, live, AllBuff, ench, slot, escape, unlimitsafe` | 列表是子集，建议标注"未穷举"或补全 |
| L301 | "Capture the current game画面" | 中英混杂、口语化 | 改 "Capture the current game screen"（精炼问题） |
| L368 | "instanceId 不会。`"（句末多余反引号） | — | 删多余反引号（校对问题） |

「Debugging Priority: Logs → Modal → Scene State」优先级引导、give_item 需要 Data+Text CSV 的提示、scan_ui index 全局索引/panel 只过滤返回列表、instanceId 防漂移的说明，均正确且必要 ✓。

---

## 3. code-patterns/entry-patterns.md（重点文件，AI 抄它决定 mod 能否挂上）

框架级内容准确：加载顺序「运行 Entry.lua → 加载 Entry.dll → 注册 Hook」、`function ModConfig:Setup()`、`self:AddMethodHookAfter("类型.方法", function(_)…)`、`AddMethodHookBefore/After` 的 `"Type.Method"` 字符串签名、3a 运行时建 UI、3b 反射 `GetField(name, 36)`、3c `$Rougamo_` 混淆名、C# DLL `[ModInitialize]` + `Commands.Log(tag,msg)` + `modConfig.DirectoryName`、`[HookBefore/After](typeof, nameof)`、`__instance`=被 Hook 的 this、程序集名 `ModName.ModAuthor`——全部与 `[源码]` `Witch.Mod.ModConfig.cs` / `LuaModHookAdapter.cs` / `[真Mod]` BlackMage / NanaSkillTracker / Mokou / FateGambler(`FateGambler.opencode`) 一致。

| 行 | 当前写法 | 证据 | 建议 |
|----|---------|------|------|
| L49-53 | 「常用 Hook 点」表：`FightManager.StartPlayerTurn` / `EndPlayerTurn` / `OnFightStart` / `OnFightEnd` | `[源码]` FightManager **无**这些方法（全源码 grep 0 命中）；真实时机 hook：玩家回合开始=`Fight_PlayerTurn.Init`（BlackMage/Plantago/NanaSkillTracker 都用）、战斗开始=`FightInit.Init`/`Fight_Start.Init`、战斗结束=`Fight_Win.ResetStates`/`Fight_Escape.ResetStates`/`Fight_Loss.Init` | **整表替换为真实方法名**。这是最高危项：4/5 个 hook 名是虚构的，AI 抄了会静默挂不上 |
| L30-41 | 模式 2 代码 `StatusManager:GetStatus("buff_mp")` / `StatusManager:AddStatus("buff_mp", 100, source, target)` | `[源码]` StatusManager 无 `GetStatus/AddStatus/RemoveStatus`；真实 API 是 `status:GetBuff(id)`（返回 buff，层数=`buff.buffConfig.Level`）、`status:AddBuff(id, level)`、`status:RemoveBuff(id)`。`[真Mod]` 同名函数 `EnsurePlayerResources` 在 BlackMage 里正是用 `status:GetBuff/AddBuff`；且 buff 运行时 ID 是 `BlackMage_blackmage_mp` 而非 `buff_mp` | 按真实 BlackMage 重写该函数 |
| L18 | "即使什么都不做，Entry.lua 也必须存在（否则游戏不会加载 Mod）" | `[源码]` `ModConfig.Setup`：Entry.lua 缺失只 `LogWarning("[Mod]…不存在Entry.lua")`，随后仍继续加载 Entry.dll 并 `return true` | 改为"建议保留空 Entry.lua 占位；缺失只告警不阻断加载" |
| L120-130 | 3c 双重 Hook：`"CheckRayToEnemy"` / `"$Rougamo_CheckRayToEnemy"`（无类型前缀，先原版后混淆） | `[真Mod]` NanaSkillTracker 真实写法：先 `"SkillItem.$Rougamo_CheckRayToEnemy"` 后 `"SkillItem.CheckRayToEnemy"`，均带类型前缀 | 补 `SkillItem.` 前缀；顺序对调（先混淆名） |
| L163 | C# 示例 `[HookAfter(typeof(FightManager), nameof(FightManager.StartPlayerTurn))]` | `[源码]` 方法不存在 | 换真实 hook 点（如 `Fight_PlayerTurn.Init` / `SettingUI.OnEnable`） |
| L204-206 | "Hook 方法必须 public static，返回 void" | `[源码]` `TryPatchMethod` 只检查 `method.IsStatic`（非 static 静默跳过）；返回 void 是惯例非强制 | 注明"必须 static；返回 void 为惯例" |
| L201 | "内部程序集名不能是 Entry" | `[真Mod]`/`[实况]` FateGambler Entry.dll 程序集名 `FateGambler.opencode` | 表述可更准：AssemblyName 应为 `ModName.ModAuthor` |

---

## 4. code-patterns/buff-as-resource.md（Buff 做资源系统）

**核心 API 全错**——整篇用的 `StatusManager:GetStatus` / `StatusManager:AddStatus` / `StatusManager:RemoveStatus` 在游戏源码中不存在（见 §3）。真实的读/写/删 Buff 是 `status:GetBuff(id)`、`status:AddBuff(id, level)`、`status:RemoveBuff(id)`。此外本文件的"模式 2 焚毁"还把 hook 第一个参数当成 `ctx.Target`，实为被 Hook 的实例本身。

| 行 | 当前写法 | 证据 | 建议 |
|----|---------|------|------|
| L24-32 | `StatusManager:GetStatus("buff_mp")` / `AddStatus("buff_mp",100,source,target)` | `[源码]` 方法不存在；`[真Mod]` BlackMage 用 `status:GetBuff("BlackMage_blackmage_mp")` | 换成 `GetBuff/AddBuff`；ID 改运行时格式 |
| L42 | "消耗 MP = 加负数层数"（`AddStatus("buff_mp",-20,…)`） | `[源码]` `BuffItemConfig`：`AddBuff(level)` 中 `level<0` 先被钳到 0，再因 `level==0 && !CanZero` 直接 `ClearBuff()` | 负值会**清空**而非扣减；正确做法是直接改 `buff.buffConfig.Level` 或 `RemoveBuff` |
| L50 | 元素 buff 用 `buff_astral_fire` 等 | `[真Mod]` 真实 ID `BlackMage_blackmage_astral_fire` | 改运行时 ID |
| L77-83 | 焚毁 hook `"CardItem.OnDiscard"` + `ctx.Target` | `[源码]` CardItem 无 `OnDiscard`（有 `EffectOfBurnCard`，Mokou 正是 hook 它）；`[真Mod]` hook 回调首参是 CardItem 实例（`ctx.dataConfig`），不是 context | hook 点改 `CardItem.EffectOfBurnCard`；去掉 `.Target` |
| L95-96 | `self:AddBuff(DataId.mahjong_wan_1, "1")` | `[源码]` `DataId.cs` 无 mahjong 常量（DataId 只含游戏内置 ID，Mod 数据不生成常量）；同 L100 `DataId.majo` | 直接用运行时 ID 字符串 |
| L116 | "Buff 的 `MaxLayer` 控制最大堆叠数" | `[源码]` 模板 Buff CSV 列名是 `UpperBound`（`MaxLayer` 不存在） | 改 `UpperBound` |
| L121-134 | 跨战斗持久化：`SpecialVars["stored_mp"] = mp` + `SaveSpecialVars()` | `[源码]` `SpecialVars` 是 `Dictionary<string,string>`，xLua 需 `:set_Item`；`SaveSpecialVars()` 全源码无此方法；`[真Mod]` EdictOfStars 直接 `p.SpecialVars:set_Item(...)` | 改 `:set_Item`；删 `SaveSpecialVars`；且"AddEvent('Win') 存 / 'StartRound' 恢复"注册在卡牌 executor 上会在战斗结束后失效，应改到 Entry.lua 的战斗结束 hook |

`self:AddEvent("EndRound")/("StartRound")` 事件名（`[源码]` EventType 枚举含 StartRound/EndRound/Win）✓；Buff CSV `Type` 用中文本地化词、可见性靠 Text CSV，方向正确 ✓。

---

## 5. code-patterns/card-transform.md

| 行 | 当前写法 | 证据 | 建议 |
|----|---------|------|------|
| L18 | `FightManager.Inst:FightAddCard("Mokou_Card_102")` | `[源码]` `FightAddCard` 全源码不存在；单例是 `FightManager.Instance`（无 `Inst`） | 换 `self:AddCard(id)` 或 `FightUI:CreateCardItem(dataConfig)`（`[真Mod]` EdictOfStars 用后者） |
| L32-41 | hook `GameEntryUI.ShowCareer` 回调里 `ctx.Arguments[1]` | `[真Mod]` Plantago hook 同一方法，回调 `function(ui)`——首参就是 GameEntryUI 实例，无 `Arguments` 属性 | 改为直接访问 `ui` 上的成员 |
| L46-54 | "形态 Buff 的 ApplyScript" 用 `DataId.buff_witch_form` | `[源码]` DataId 无此常量 | 换运行时 ID |
| L77 | `FightManager.Inst:FightAddCard("EdictOfStars_…companion_attack")` | 同 L18，方法不存在 | 同上 |
| L88-91 | `StatusManager:GetStatus("buff_blooming")` | 方法不存在 | 换 `GetBuff` |
| L95 | `self:Defend(5)` | `[源码]` ScriptExecutor 无 `Defend`，防御是 `ChangeDefence(string)` | 改 `self:ChangeDefence("5")` |
| L135-141 | 卡牌 ID 用 `*` 前缀排除随机池 | `[真Mod]` Mokou 卡 ID `*mokou_tail` 正是此用法 | ✓ 正确 |

`self:SetStatus("AllEnemy"/"Target")`、`self:Damage(10)`、`self:DrawCount(1)`、`self:AddBuff` 均存在 ✓。

---

## 6. code-patterns/cooldown-dice.md

| 行 | 当前写法 | 证据 | 建议 |
|----|---------|------|------|
| L13/L25 | 冷却用 `self.Vars.SkillTime` | `[源码]` `Vars` 是卡牌自身的 `IDictionary<string,string>`；冷却的真实存储是 `CS.ScriptExecutor.PlayerInfo.SkillTime`（`Dictionary<string,int>`）。`[真Mod]` Mokou/Plantago 均用 `st:ContainsKey/set_Item/get_Item` + `AddEvent("StartRound")` 每回合递减 | 整段改按 PlayerInfo.SkillTime 写法 |
| L19 | hook `"FightManager.StartPlayerTurn"` 做冷却递增 | 方法不存在（见 §3）；真实是 `AddEvent("StartRound")` | 换 `AddEvent` |
| L44 | `self.Vars.DesVal1 = tostring(remaining)` | `[源码]` xLua 访问 IDictionary 需 `:set_Item`；`[真Mod]` Mokou `self.Vars:set_Item("DesVal1", tostring(cd))` | 改 `:set_Item` |
| L44/50 | `DataId.buff_cooldown` | DataId 无此常量 | 换运行时 ID |
| L71 | `local roll = Dice.Roll()  -- 返回 0~1 之间的值` | `[源码]` `Dice.Roll()` 返回 `Dice.State`（含 `int Value/Bonus`），非浮点；且 Lua 环境只预置 `self`/`ScriptExecutor`，无全局 `Dice` | 骰子玩法重写（真实习惯：`PlayerInfo.DefaultRoll`，见事件 CSV `int a = PlayerInfo.DefaultRoll;`） |
| L90 | `RoleTable.Inst.Lucky` | `[源码]` 单例是 `RoleTable.Instance`；`[真Mod]` Mokou 用 `tonumber(CS.ScriptExecutor.PlayerInfo.Lucky)` | 改 `PlayerInfo.Lucky`（带 `CS.`） |
| L106-129 | 里程碑：`FightManager.OnFightEnd`/`OnFightStart` hook + `StatusManager:GetStatus/AddStatus` + `SpecialVars["wuwo_counter"]` + `SaveSpecialVars()` | 全部不存在（见 §3/§4） | 换 `Fight_Win.ResetStates`/`Fight_Start.Init` + `GetBuff/AddBuff` + `SpecialVars:set_Item` |
| L132-141 | 里程碑 Buff CSV 表头 `Id,MaxLayer,CanZero,Type,Icon,InitScript` | `[源码]` 模板表头是 `Id,InitScript,ApplyScript,ClearScript,ReducePerTurn,ReducePerAttacked,ReducePerUse,UpperBound,Icon,Type,Rarity,Effects,SoundEffects,Action,CanZero`；`MaxLayer` 应为 `UpperBound` | 改真实表头 |
| L140 | "CanZero=True 的 Buff 在 0 层仍存在并触发事件" | `[源码]` `BuffItemConfig`：`if (level==0 && !CanZero) buffItem.ClearBuff()` | ✓ **正确**（这是本文件唯一 API 层面全对的核心点） |
| L156-173 | 月相：`StatusManager:GetStatus/AddStatus` | 不存在 | 换 `GetBuff/AddBuff` |

---

## 7. code-patterns/career-mod.md（Career CSV 表头大面积虚构）

Career / RoleData 的 CSV 列是**编造的**，与真实模板不符（与 08-06 报告 §18.3 patterns/SKILL.md 同性质）。

| 行 | 当前写法 | 证据 | 建议 |
|----|---------|------|------|
| L45-47 | Career CSV：`Id,Name_zh-Hans,Name_zh-Hant,Name_en,Name_ja,SanMax,HpMax,RoleDataId,CardAsset,CardList,RelicList,PartnerList,Attribute,PackBelong` | `[真Mod]`/`[源码]` 真实 Career 表头 = `Id,SanMax,SkillScript,Animation,Vocal,Skill1,Skill2,ChoiceIcon,DollIcon,Character,Avatar,CareerImage,ActionImage1,ActionImage2,Dialogue,EmojiPath,AttackEffect,SkillEffect,HitEffect,DefendEffect`。无名称列（名字在 Text CSV）、无 HpMax、无 CardList/RelicList/PartnerList（初始卡/随从经 SkillScript 的 AddBuff + 卡包 PackBelong 处理）、职业技能是 `Skill1`/`Skill2` 两列填**卡牌运行时 ID**（如 `Mokou_cardsample_mokou_tail`） | **整段按真实表头重写**；补 Skill1/Skill2 技能机制 |
| L64-66 | RoleData CSV：`Id,Name,AnimationLib,Avatar,CareerImage,Character,AttackEffect,SkillEffect,HitEffect,DefendEffect` | `[真Mod]` 模板 `RoleData` 表头 = `Id,Avatar,CharacterImage,HouseAvatar` | 改真实表头 |
| L86 | `ScriptExecutor.PlayerInfo.ChangePower(2)` | `[源码]` PlayerInfo 无 `ChangePower`（在 ScriptExecutor 实例上）；且缺 `CS.` 前缀 | 改 `self:ChangePower("2")` |
| L90 | `StatusManager:GetStatus("buff_corruption")` | 不存在 | 换 `GetBuff` |
| L104-121 | 动画 config.json（AnimationPerFrame/isLoop/Direction:"row"）、300×300 | `[真Mod]` Mokou/EdictOfStars 确有 `AnimationLib/CareerName/{Idle,Attack,Defend,Hit,Skill}/config.json` + 帧图；但 `Direction` 取值（08-06 报告 §18.4）应为 "Right"/"Left" 等，"row" 不符；300×300 未验证（rdl 实为图集） | Direction 改枚举值；帧尺寸标注需实测 |
| L126-134 | 学习表含 "Defect（故障机器人）"，建议"先从 Defect 示例开始" | `[实况]` Mods 目录 22 个 Mod 中无 Defect | 换成本机已装的职业 Mod（如 Mokou/Plantago）做参考 |
| L14-38 | 必需文件清单（Career/Card/CardPack/Buff/RoleData/Relic + Text + KeyWordsDic + ModResource） | 与 `[真Mod]` Mokou/EdictOfStars 目录结构一致 | ✓ 正确 |

---

## 全局问题汇总

### 高危（会直接误导 AI 写出跑不通的 mod）

1. **code-patterns 四个文件的 Lua API 大面积虚构**：`StatusManager:GetStatus/AddStatus/RemoveStatus`、`FightManager.Inst:FightAddCard`、`self.Vars.SkillTime`、`self:Defend()`、`RoleTable.Inst.Lucky`、`SaveSpecialVars()`、`Dice.Roll()` 返回 0~1 浮点——全部经 `[源码]`/`[真Mod]` 证伪。正确 API 对照：Buff 读写删=`status:GetBuff(id)`/`AddBuff(id,level)`/`RemoveBuff(id)`（层数=`buff.buffConfig.Level`）；加卡到手上=`self:AddCard(id)` 或 `FightUI:CreateCardItem(dataConfig)`；防御=`ChangeDefence`；冷却=`CS.ScriptExecutor.PlayerInfo.SkillTime` 字典；幸运=`CS.ScriptExecutor.PlayerInfo.Lucky`；单例=`XXX.Instance`。**建议整轮重写 buff-as-resource / cooldown-dice / card-transform 的代码块，并照抄真实 Mod（BlackMage/Mokou/EdictOfStars/Plantago）的 Entry.lua。**
2. **entry-patterns.md 「常用 Hook 点」表 4/5 为虚构方法**（`FightManager.StartPlayerTurn/EndPlayerTurn/OnFightStart/OnFightEnd` 均不存在）。hook 能否挂上是 mod 生死线，此表必须替换为：`Fight_PlayerTurn.Init`（玩家回合）、`Fight_Start.Init`/`FightInit.Init`（战斗开始）、`Fight_Win.ResetStates`/`Fight_Escape.ResetStates`/`Fight_Loss.Init`（战斗结束）、`SettingUI.OnEnable`（设置界面）。
3. **career-mod.md Career / RoleData CSV 表头整段虚构**（`HpMax/CardList/RelicList/PartnerList/Attribute/PackBelong` 等不存在；真实有 `Skill1/Skill2` 技能列）。AI 按它写 CSV 必加载失败。
4. **buff-as-resource「负值=消耗」语义错误**：`AddBuff` 负值被钳 0 后直接 `ClearBuff`，不是扣减。

### 中危

5. **hook 回调首参语义**：所有 hook 回调第一个参数 = 被 Hook 方法的 `this`（实例），不是 ModHookContext。`card-transform` 的 `ctx.Arguments[1]`、`buff-as-resource` 的 `ctx.Target` 都会取到 nil。对照 `[真Mod]`：`GameEntryUI.ShowCareer`→`function(ui)`、`ScriptExecutor.ChangeHp`→`function(exe, amount)`、`SafeBoxItem.Init`→`function(item, dataConfig)`。
6. **3c 混淆 hook**：`$Rougamo_CheckRayToEnemy` 需带类型前缀 `SkillItem.`，且 NanaSkillTracker 先试混淆名再试原名，文档顺序反了。
7. **diagnostics 数据规模过期**：DataConfigCache 2355（文档 ~2183/~2180）、NativeIds 1825（文档 ~1723）；"34 tables" 实为 35 个 DataType。
8. **tools 表缺字段**：diagnostics 的 `click_ui`（缺 instanceId）、`scan_ui`（elements 缺 instanceId）；combat 的 `get_fight_state` player/enemies 缺 instanceId——而 instanceId 恰是官方推荐参数。

### 低危（风格/校对）

9. diagnostics L301 "game画面" 中英混杂；L368 句末多余反引号。
10. `give_item` type 列表未穷举（漏 timecount/randombless/goodbless/def/live/AllBuff/ench/slot/escape/unlimitsafe 等）。
11. 各处示例 ID（`buff_mp`/`buff_fuel`/`DataId.buff_witch_form` 等）不是真实运行时 ID；Mod 数据 ID 格式为 `{ModFolder}_{CsvFile}_{RawId}`。

### 建议复核项

- `Buff` 的 `UpperBound` 语义、动画帧尺寸（300×300 vs 图集）未逐帧验证，建议以 FateGambler `Lib/DataConfigs` 模板 + 真实 mod 资源为准。
- `get_deck_selection` 的 `cost` 字段读取 `"Cost"` 键而卡 CSV 用 `Expend`，实况可能恒 0——工具侧问题，文档可备注。

---

## 附：与 2026-08-06 报告交集说明

- `FightManager.*` hook 虚构：本次 §3 是**首次**在该文件发现（08-06 报告未覆盖 code-patterns）。
- Buff CSV `Type` 中文化、`UpperBound`、`MaxLayer` 错名、`Vars:set_Item`、`AddCardByCardList` tag 语义：与 08-06 报告 §18 patterns/SKILL.md 结论一致，可合并修复。
- `DataConfigCache/NativeIds` 数量：08-06 报告未核（diagnostics 不在当时范围），本次给出实况值。

*本报告仅审查，未修改任何文件。*
