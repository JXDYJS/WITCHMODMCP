# insights/SKILL.md 审查报告

审查日期：2026-08-07
审查方式：只读。通过 MCP 实时调用（`search_config`、`get_game_info`）+ `decompile_source` 反编译 Witch.dll / Witch.Core.dll + 读取磁盘上真实 Mod CSV（BlackMage / PlagueSpread / Mokou / EdictOfStars / rdl）+ 读取本仓库 MCP 工具源码，逐条核对。
审查对象：`.agents/skills/witchSkill/insights/SKILL.md`（1132 行）。
说明：重点核对了**关键且容易错**的内容（Rougamo 织入、单例名、战斗字段、运行时 ID 规则、内置 Buff ID、CSV 表头、Lua API 签名），未逐行核对所有示例代码。

---

## 一、已验证为准确的关键结论（✅）

### 1. Rougamo 织入 ALL 方法 — 准确（技能文件最核心的断言）
- `Modifiable.cs`（Witch.Core）：`[Pointcut(AccessFlags.All | AccessFlags.Property | AccessFlags.Method)]` + `[Advice(Feature.OnEntry | Feature.OnSuccess)]`，与 skill §5.1（L141）描述完全一致。
- Fody 标记文件 `Witch_ProcessedByFody.cs`：`Rougamo = "1.0.0.0"`。
- `FightManager.cs` 内 81 处 `new Modifiable()` 包装器，属性 getter/setter 也被织入（`$Rougamo_get_selfIndex()` 等）。**协程除外**的说法无法直接验证（反编译看不到），但"几乎全量"成立。

### 2. Rougamo 包装器结构 — 准确
反编译 `FightManager.cs` / `ModConfig.cs` 的包装器与 skill L155-169 示例一致：
`new Modifiable()` → `RougamoPool<MethodContext>.Get()` → `OnEntry` → 传**原始变量**调用 `$Rougamo_xxx(...)` → `OnSuccess` → 归还池。
→ 结论成立：改 `ctx.Arguments` 无效、不能改返回值、只能监听。skill §5.1 的能力边界表准确。

### 3. Hook 注册机制 — 准确
- `ModHookRegistry`：`Before`/`After` 两个字典，key = `类型名.方法名`。
- `ModConfig.AddMethodHookBefore/After` 有 LuaFunction/Action/`(string,string,Action)`/`(Type,string,Action)` 多种重载。
- `ModConfig.ModifyDataConfig(id,key,value)` / `SetDataConfig` / `MergeDataConfig` / `RedirectSourcePath` 均存在（skill §13.6 Entry.lua 示例可用）。
- `[ModInitialize]` / `[ModHook]`（含 HookBefore/HookAfter）注册逻辑在 `ModConfig.Setup()` 中，**且要求方法为 `static`**（`TryPatchMethod` 里 `if (method.IsStatic)`），skill §5.1 的 C# 示例恰好是 static，无冲突。

### 4. Mod 加载顺序 & 依赖拓扑排序 — 准确
`GameConfigManager.$Rougamo_Init()`：Lua 初始化 → `LoadResource("Addressables/DataConfigs/Data/")`+`Text/` → `AddNativeIds()` → 扫描 `ModsPath/ModConfig.json` → `LoadModWithDependencies`（**Kahn 拓扑排序，inDegree 计数 + Queue**，禁用 mod 跳过）→ 逐 mod 加载 Data/Text → `ModConfig.Setup()` → 关键词表（`BuffKeyword_`/`CardKeyword_`/`EnchTag_`）→ `PreCompileScripts()` → `DialogueManager.Init()`。与 skill §4 的 9 步流程一致。

### 5. 运行时 ID 规则 `{ModFolder}_{CsvFileName}_{RawId}` — 准确
- `ExcelTableReader.BuildPrefix`：`父目录名(Data/Text 的上一级=Mod 文件夹) + "_" + 文件名去扩展名`。
- `GameConfigData`：key = `prefix + "_" + Id`，且行内 `Id` 也会被改写为完整运行时 ID；原始 `Id` 含 `*` → 去掉 `*` 并加入 `LockedIds`（锁定，不进随机池）。与 skill §11 的 ID 规则及 `*` 前缀语义完全一致。
- 实时证据：PlagueSpread 卡 `PackBelong=PlagueSpread_plaguepack_pack_plague` ✓。

### 6. 配置系统 — 准确
`Globals.DataConfigCache` 确为 `ConcurrentDictionary<string, IDataConfig>`；`DataConfig` 确含 `Type / data / Vars / IsNative / InstanceID / scriptExecutor`；`.csv` 与 `.xlsx` 均被读取；`*` 前缀锁定。

### 7. 控制台命令系统 — 准确
`ConsoleLogic.Input()` 反射静态 `Commands` 类；`give(string,string)` / `load(string,string)` / `check(string)` 签名及 `[HelpText]` 均与 skill §6 一致。`givepack <packid>` 命令存在。

### 8. ScriptExecutor Lua API — 签名基本全部命中
`SetStatus(string)`、`Damage(string, damagetype="Normal")`、`AddBuff`、`RemoveBuff`、`ChangeHp`、`ChangeMaxHp`、`DrawCount`、`ChangePower`、`ChangeDefence`、`AddCard`、`AddCardById`、`RandomAddCard`、`CreateCard(IDataConfig)`、`BurnCard(string, type="1")`、`AddEvent(string, Action)`、`ForAllStatus`、`RunImmediately`、`AddDescription`、`PlayerInfo.ShowCaption`、`StatusManager.GetBuff` 全部存在。`PlayerInfo.SkillTime`(`Dictionary<string,int>`) / `SpecialVars`(`Dictionary<string,string>`) 存在。

### 9. CSV 表头 — Card/Buff/CardPack/Career/RoleData/Relic/Text 全部核对通过
| 类型 | 真实表头（磁盘实测） | 与 skill 对比 |
|---|---|---|
| Card | `Id,Rarity,Expend,Tag,PackBelong,InitScript,DrawScript,UseScript,DropScript,Icon,Effects,Action` | ✓ 12 列全部命中 |
| Buff | `Id,InitScript,ApplyScript,ClearScript,ReducePerTurn,ReducePerAttacked,ReducePerUse,UpperBound,Icon,Type,Rarity,Effects,SoundEffects,Action` | ✓ 14 列全部命中 |
| CardPack | `Id,Type,Icon` | ✓ |
| Career | `Id,SanMax,SkillScript,Animation,Vocal,Skill1,Skill2,ChoiceIcon,DollIcon,Character,Avatar,CareerImage,ActionImage1,ActionImage2,Dialogue,EmojiPath,AttackEffect,SkillEffect,HitEffect,DefendEffect,FightWidget,Note` | ✓ 22 列全命中（含 Note 示例值 "Ishtar formal character interface..."） |
| RoleData | `Id,Avatar,CharacterImage` | ✓ |
| Relic | `Id,Rarity,OwnScript,FightScript,Icon,PackBelong` | ✓ |
| Text/Card | `Id,是否完成,Type,Note,Name,Name_en,Name_zh-Hant,Name_ja,Description,Description_zh-Hant,Description_en,Description_ja` | ✓（`是否完成` 确认无误） |
- CardPack `Type=="Basic"` 样式判断在 `CardPackItem.cs` 中存在 ✓。
- "Text CSV 缺失 → give 卡报错" 成立：`Commands.cs:2097` 确实读 `GetOne(DataType.Card, arg2)["Name"]` ✓。

### 10. 技术栈 — 基本属实
Managed 目录实测含：Mirror.dll / UniTask.dll / ZString.dll / ZLinq.dll / MemoryPack.Core.dll / Newtonsoft.Json.dll / Rougamo.dll / Loxodon.Framework.Obfuscation.dll / Unity.TextMeshPro.dll。`.NET Framework 4.72` 一项**未直接验证**（反编译看不到目标框架），建议标注低置信。

### 11. 自动化 API — 属实
`Witch.UI.Automation.*`（RuntimeBattleAutomationService / RuntimeUiSnapshot / RuntimePlayCardRequest 等）类全部存在于反编译结果中。

### 12. 单例模式 — 属实，但名字见"二"
`Singleton<T>.Instance`（`SingletonResolver.Resolve<T>`）；UIManager/GameApp/MapManager/EnemyManager/GameRuntimeData/LobbyManager/DialogueManager 全部用 `Instance`。

---

## 二、发现的关键错误（❌ 重点）

### 1. 内置 Buff ID 表有两处错误（§11.3，L444-459）— 高危
| skill 写的 ID | 实际（`search_config` NativeIds 实测） |
|---|---|
| `buff_vulnerable` | **不存在**。真实 ID 是 `buff_vulnerability` |
| `buff_regenerate` | **不存在**。真实回复类 buff 为 `buff_RegenerationPrayer`（NativeId 中可查到） |

其余 10 个（`buff_weak/buff_contagion/buff_evergreen/buff_rebirth/buff_burn/buff_bleeding/buff_extraordinary/buff_resilient/buff_degrade/buff_elements`）均为真实 NativeId ✓。
连带影响：**"完整卡牌示例"（L466）的 UseScript 用了 `buff_vulnerable` 和 `buff_regenerate`，是错误的**。磁盘上真实 PlagueSpread 卡（本 skill 示例的原型）实际写的是 `buff_vulnerability` + `buff_RegenerationPrayer`。示例还用了假路径 `Icon/Card/plague`（真实为 `Mods/PlagueSpread/ModResource/Images/Card/plague`）和假 PackBelong `YourMod_plaguepack_pack_plague`。AI 照抄示例会得到两张不存在的 buff。

### 2. 单例名三个错误（§2，L40-47）— 高危（会直接写错代码）
| skill 写的 | 实际 |
|---|---|
| `FightManager.Inst` | 只有 `FightManager.Instance`（FightManager.cs:52），**无 Inst** |
| `PlayerManager.Inst` | 只有 `PlayerManager.Instance`（PlayerManager.cs:46），**无 Inst** |
| `RoleTable.Instance / RoleTable.Inst` | 只有 `RoleTable.Instance`（RoleTable.cs:128），**无 Inst** |

### 3. §8 战斗系统字段整体错误 — 高危（会直接写错代码）
skill 声称 `FightManager.Inst` 上有 `FightPlayer / AllEnemys / FightCards / DrawCards / DiscardCards / ExhaustCards` 字段。实测：
- `FightManager` 上**没有**这些字段（只有 `roleQueue`/`ActionQueue`/`fightType`/`IsFake` 等）。
- 真实结构（MCP 工具 `GetFightStateTool.cs` 即按此读取）：
  - 玩家：`FightPlayer.Instance`（`.Status` 状态、`.CurPowerCount`/`.MaxPowerCount` 能量）
  - 敌人：`EnemyManager.Instance.enemyList`
  - 牌堆：`FightCardManager.Instance`（`cardList`=抽牌堆、`usedCardList`=弃牌堆、`FightcardList`=手牌）
- §8 的"Phases: Player → Enemy → Player"也不准确（实际 `FightType` 枚举还有 Init/OtherTurn/Partner/Win/Loss/Escape 等）。

### 4. §9 动画命名与分辨率错误（L288-290）— 中危
- 帧命名不是 `frame_N.png`。实测 Mokou：`Idle_00.png`（`{状态名}_NN.png`）；rdl：哈希文件名（`支持魔女1-CAB-...png`）。skill 的"frame sequence follows naming convention `frame_N.png`"错误。
- "Animation resolution: 300×300 for skill animations" 错误。实测 Mokou 帧 256×256、rdl 帧 384×384，无统一 300×300。
- `config.json` 字段 `AnimationPerFrame / isLoop / Direction` 正确 ✓（实测 `{"AnimationPerFrame":0.1,"isLoop":false,"Direction":"Right"}`）。

### 5. UseScript API 表两处参数语义错误（§11.3）— 中危
| skill 写的 | 实际 |
|---|---|
| `AddCardToDeckById(id, toHand)`，"true=hand, false=draw pile" | 参数名是 `toUsed`；`true`→弃牌堆 `usedCardList`，`false`→抽牌堆 `cardList`（ScriptExecutor.cs:3901-3944）。"true=hand" 错误 |
| `AddCardByCardList(count, name)`，"Add random card matching name from card list" | 真实签名 `AddCardByCardList(string count, string tag = "all")`，第二参是**标签过滤**（检索抽牌堆），不是卡名（ScriptExecutor.cs:4391） |

### 6. §12 Step 6 / §13.7 卡包测试命令错误（L803）— 中危
"use `give_item givepack <PackId>`" — `give_item` 工具**没有** `givepack` 类型（支持类型列表里没有）。正确做法是 `eval_command("givepack <packid>")`（`givepack` 是控制台命令，Commands.cs:604）。

### 7. §13.3 Step 7 hook 目标方法名错误（L941-947）— 中危
`GameEntryUI.UpdateState` **不存在**。`GameEntryUI` 有 `Init / StartGame / UpdateLobby / ShowCareer / ShowDetail` 等，无 `UpdateState`。hook 注册到不存在的 key 永远不会触发（静默失效）。

### 8. §3 DataType 枚举名（L59）— 低危
skill 列 `EventList` / `Blessing`，实际枚举值是 `Event` / `Bless`（`EventList`/`Blessing` 是 CSV **子文件夹**名，见 `LoadResource` 的 `path+"EventList"`/`path+"Blessing"`）。"DataType enum values include..."的说法用目录名冒充枚举名。

### 9. "Script 列"判定 — 低危
skill 说"列名 contains `Script`"，实际 `IsScriptColumn` = `key.EndsWith("Script")` 且排除 `Id`（GameConfigManager.cs:1689-1696）。方向一致，表述可改为"以 Script 结尾"。

---

## 三、说了跟没说一样 / 过于含糊（✍️）

1. **§11.5f Partner（L697-701）**："Partners ... follow similar CSV patterns. See the template's `Lib/DataConfigs/` for exact column names." — 未给出任何实际列。实测真实表头是 `Id,Hp,Attack,Defend,ActionCount,Rarity,InitScript,CardList,ChoiceIcon,Model,Animation,Bless,CareerImage`，应直接写出。
2. **§11.5g Blessing（L703-707）**："Blessings can also be added. Set PackBelong to a card pack runtime ID ... See the template's ... for exact columns." — 同样含糊。实测真实表头 `Id,Weight,OwnScript,FightScript,Icon,Type,Source,Rarity,PackBelong`。
3. **§8（L282）** "Phases: `Player` → `Enemy` → `Player` → ..." 作为"知识库"过于简化（见二.3）。

---

## 四、不像精炼 skill、更像日常对话/编辑备注（💬）

1. **L139**："> ⚠️ **重要更新：Rougamo 织入的是 ALL 方法，不是部分方法。** 以下描述已根据反编译源码修正。" — 是变更日志口吻的编辑备注（"重要更新/已修正"），不是精炼的事实陈述。内容本身对，但写法应改为平铺直叙（如"Rougamo 织入 Witch.dll/Witch.Core.dll 的全部方法"），不要保留"重要更新/以下描述已修正"这类过程性话语。
2. **L308-311**（"CSV Schema 仅供参考 ... 唯一可靠的方法是：1. 克隆模板仓库 ... 2. 查看 ... 3. 或使用 decompile_source ..."）、**L409**（"API 仅供参考，不一定完全正确 ..."）、**L579**（"SkillScript API 仅供参考 ..."）—— 三处同模板免责声明，口语化且重复。可精简为一条统一说明，且既然本文档已有实测错误（见二），这类"仅供参考"反而弱化了文档权威性。
3. **L12-13**："Do NOT probe the game runtime with `query_config` or `inspect` to figure out CSV columns — they are documented here." — 禁令本身合理，但上一轮审查已发现 `query_config` 的 tableName 实际无法解析常见表名（连 "Card" 都查不到）。此处把 query_config 当作"探测手段"点名略显错位；且"they are documented here"的前提因本文档存在错误（见二.1/二.5）而变弱——建议保留"先查本 skill"的要求，同时补一句"文档有误时以 search_config / 反编译为准"。

---

## 五、未验证 / 建议后续复核（⚠️）

- **`.NET Framework 4.72`（§1）**：反编译无法确认目标框架，建议标低置信。
- **§5.2 Harmony**：外部库行为，游戏源码无 Harmony 代码可对证；"协程/TargetRpc 是 Rougamo 例外"与"Harmony 能 hook 它们"为合理推断，无法从游戏侧验证。
- **§13 的 Lua 技能模板**：多数 API 已确认存在（SkillTime/SpecialVars/AddEvent/GetBuff/ShowCaption），但 `self:AddEvent` 支持的事件名（StartRound/EndRound/Hurt/BurnCard/Win/Escape/SelectCardEnd 等）未逐一与 `EventType.cs` 核对，建议单独复核一次。
- **§11.3 内置 Buff 的中文名**（易伤/再生/常青…）：只核对了 ID，未逐个核对本地化名称。

---

## 六、结论

- **可靠度较高**：§3-§6（配置系统/加载流程/Hook/控制台）、§10（自动化 API）、§11 的 CSV 表头（Card/Buff/CardPack/Career/RoleData/Relic/Text）、§12 的安装/验证步骤，核心事实经反编译+磁盘+实时 MCP 三路验证均准确。
- **必须修正（会直接误导写代码）**：
  1. 内置 Buff ID：`buff_vulnerable`→`buff_vulnerability`，删 `buff_regenerate`（改 `buff_RegenerationPrayer`）；连带修正 L466 完整卡牌示例。
  2. 单例名：`FightManager.Inst`/`PlayerManager.Inst`/`RoleTable.Inst` → 全部 `.Instance`。
  3. §8 战斗字段：改为 `FightPlayer.Instance` / `EnemyManager.Instance.enemyList` / `FightCardManager.Instance.{cardList,usedCardList,FightcardList}`。
  4. `AddCardToDeckById` 语义（true=弃牌堆）、`AddCardByCardList` 第二参（tag）。
  5. `give_item givepack` → `eval_command givepack`。
  6. §13.3 hook 目标 `GameEntryUI.UpdateState` → 用真实方法名。
- **应精简**：§9 帧命名/分辨率；§11.5f/§11.5g 补上真实表头；四类"日常对话/免责声明"改写为精炼陈述。

---

## 七、修复记录（2026-08-07 追加）

以下修复已直接应用到 `.agents/skills/witchSkill/insights/SKILL.md`：

| # | 位置 | 修复内容 |
|---|------|---------|
| 1 | §2 单例 | `FightManager.Inst` / `PlayerManager.Inst` / `RoleTable.Inst` → 全部改为 `.Instance` |
| 2 | §3 DataType | `EventList`/`Blessing` → `Event`/`Bless`，并注明前者是子文件夹名 |
| 3 | §3/§11 | "列名 contains Script" → "以 `Script` 结尾（不含 `Id`）" |
| 4 | §7 RoleTable | 字段表改为真实成员：`San`/`MaxSan`/`Money`/`cardList`/`relicList`/`blessingConfigs`/`SkillTime`/`SpecialVarMap`；补充战斗实体属性位置说明 |
| 5 | §8 战斗系统 | `FightManager.Inst` + 六个虚构字段 → `FightPlayer.Instance`/`EnemyManager.Instance.enemyList`/`FightCardManager.Instance.{FightcardList,cardList,usedCardList}`；阶段改为 FightType 枚举说明 |
| 6 | §9 动画 | `frame_N.png` → `{State}_NN.png`（部分 mod 用哈希名）；300×300 → 无固定分辨率（实测 256×256 / 384×384） |
| 7 | §11.3 UseScript | `AddCardByCardList(count, name)` → `(count, tag="all")` 标签过滤；`AddCardToDeckById(id, toHand)` → `(id, toUsed)` true=弃牌堆 false=抽牌堆 |
| 8 | §11.3 Buff ID | `buff_vulnerable` → `buff_vulnerability`；`buff_regenerate` → `buff_RegenerationPrayer`；连带修正完整卡牌示例与 Text 描述占位符示例 |
| 9 | §11.5f/11.5g | Partner / Blessing 补上真实表头（替代"看模板"的含糊写法） |
| 10 | §12 Step 6 | `give_item givepack` → `eval_command givepack` |
| 11 | §13.3 | 动画帧命名修正；`GameEntryUI.UpdateState` → `GameEntryUI.ShowCareer`（真实方法） |
| 12 | 免责声明 | 删除 L139"重要更新"编辑口吻；三处"仅供参考"免责声明精简为一句已验证事实 + 验证方法 |

未处理（待后续）：§1 `.NET Framework 4.72` 低置信标注、§5.2 Harmony 外部库行为、§13 `self:AddEvent` 事件名清单核对。

*本报告初稿为纯审查；2026-08-07 依据审查结果对 `insights/SKILL.md` 做了上述修复。*
