# 完整职业 Mod 架构

从多个已有职业 Mod（Mokou、MoonRite、SunExp、JogasakiNoah、Plantago、EdictOfStars）提炼的完整职业创建指南。
所有 CSV 表头以本 skill 附带的 `references/csv-schemas.md`（官方模板表头，MIT）为准；需要完整模板时 `git clone https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git`。

---

## 必需文件清单

一个完整的职业 Mod 至少需要：

```
YourCareer/
├── ModConfig.json
├── Scripts/Entry.lua            ← 必填（可空函数体）
├── Data/
│   ├── Career/career.csv        ← 职业定义（含技能、动画、立绘路径）
│   ├── Card/cards.csv           ← 卡牌定义（含职业技能 Skill1/Skill2 指向的卡）
│   ├── CardPack/cardpack.csv    ← 卡包定义
│   ├── Buff/buffs.csv           ← Buff 定义（至少职业特色 Buff）
│   ├── RoleData/roledata.csv    ← 角色立绘/头像资源路径
│   └── Relic/relics.csv         ← 圣物（可选）
├── Text/
│   ├── Career/career.csv
│   ├── Card/cards.csv
│   ├── CardPack/cardpack.csv
│   ├── Buff/buffs.csv
│   ├── RoleData/roledata.csv
│   ├── Relic/relics.csv
│   └── KeyWordsDic/keywords.csv ← 关键词词典
└── ModResource/
    ├── AnimationLib/CareerName/ ← 战斗动画
    │   ├── Idle/
    │   ├── Attack/
    │   ├── Defend/
    │   ├── Hit/
    │   └── Skill/
    └── Images/                  ← 立绘/头像/卡包图
```

---

## Career CSV 配置

**真实表头**（`Id,SanMax,SkillScript,Animation,Vocal,Skill1,Skill2,ChoiceIcon,DollIcon,Character,Avatar,CareerImage,ActionImage1,ActionImage2,Dialogue,EmojiPath,AttackEffect,SkillEffect,HitEffect,DefendEffect`）：

```csv
Id,SanMax,SkillScript,Animation,Vocal,Skill1,Skill2,ChoiceIcon,DollIcon,Character,Avatar,CareerImage,ActionImage1,ActionImage2,Dialogue,EmojiPath,AttackEffect,SkillEffect,HitEffect,DefendEffect
# id,最大SAN,技能/初始化脚本,战斗动画目录,语音目录,技能1卡ID,技能2卡ID,选择图标,立绘,角色图,头像图,职业图,行动图1,行动图2,对话图,表情,攻,技,受,防
YourMod_YourCsv_your_career,90,"local key = ""YourMod_YourCsv_skill_guard""; local st = CS.ScriptExecutor.PlayerInfo.SkillTime; if not st:ContainsKey(key) then st:set_Item(key, 0) end; self:AddEvent(""StartRound"", function() local s = CS.ScriptExecutor.PlayerInfo.SkillTime; if s ~= nil and s:ContainsKey(key) then local cd = tonumber(s:get_Item(key)) or 0; if cd > 0 then s:set_Item(key, cd - 1) end end end);",Mods/YourMod/ModResource/AnimationLib/YourCareer,,YourMod_YourCsv_skill_guard,YourMod_YourCsv_skill_fang,Mods/YourMod/ModResource/Images/Icon/YourCareer,Mods/YourMod/ModResource/Images/Character/YourCareer,Mods/YourMod/ModResource/Images/Avatar/YourCareer,Mods/YourMod/ModResource/Images/CareerImage/YourCareer,Mods/YourMod/ModResource/Images/Icon/YourCareer,Mods/YourMod/ModResource/Images/Icon/YourCareer,Mods/YourMod/ModResource/Images/Dialogue/Character/YourCareer,YourCareer 大招词,YourCareer-Attack,YourCareer-Skill,YourCareer-Hit,YourCareer-Defend
```

**关键列：**
- `Id` — 运行时 ID 前缀，卡牌/Buff 引用它
- `SanMax` — 角色最大理智（唯一的数值属性列；**没有 HpMax 列**）
- `SkillScript` — 战斗开始时执行的 Lua（初始化冷却、加初始 Buff、注册 `AddEvent("StartRound")` 等）。**没有 CardList/RelicList 列**——初始卡/随从由 SkillScript 的 `AddBuff` 与卡包 `PackBelong` 处理
- `Skill1` / `Skill2` — **职业技能卡牌的运行时 ID**（不是代码，是指向卡 CSV 里的卡，如 `YourMod_YourCsv_skill_guard`）；技能冷却用 `CS.ScriptExecutor.PlayerInfo.SkillTime` 字典驱动，游戏 UI 会显示
- `Animation` / `Vocal` / `ChoiceIcon` / `DollIcon` / `Character` / `Avatar` / `CareerImage` / `ActionImage1/2` / `Dialogue` / `EmojiPath` — 各类资源路径（`Mods/` 开头，不带扩展名）
- `AttackEffect` / `SkillEffect` / `HitEffect` / `DefendEffect` — 各动作特效（可留空）
- **没有** `Name_zh-Hans` 等名称列——名称/描述在 Text CSV

> ⚠️ **常见错误**：Career CSV **没有** `HpMax / RoleDataId / CardAsset / CardList / RelicList / PartnerList / Attribute / PackBelong` 这些列。初始卡组通过卡包 `PackBelong` + 卡 CSV 实现，初始 Buff 通过 `SkillScript` 实现，属性加点由大厅（lobby）选择。

---

## RoleData CSV

**真实表头**（`Id,Avatar,CharacterImage,HouseAvatar`）：

```csv
Id,Avatar,CharacterImage,HouseAvatar
# id,头像路径,角色立绘路径,小屋对话头像路径
YourMod_YourCsv_your_career,Mods/YourMod/ModResource/Images/Avatar/YourCareer,Mods/YourMod/ModResource/Images/Character/YourCareer,Images/Avatar/YourCareer
```

> ⚠️ 不要用 `Id,Name,AnimationLib,CareerImage,Character,AttackEffect,...` 那套——真实只有 3 个图片路径列。

---

## 角色技能（SkillScript / Skill1 / Skill2）

职业技能由两处配合：
1. **Career 的 `Skill1`/`Skill2` 列**指向卡牌运行时 ID（技能即卡），游戏战斗 UI 的 Skill1/Skill2 按钮就放这张卡
2. **`SkillScript` 列**写 Lua 初始化：注册每回合递减、初始 Buff、冷却字典等

```lua
-- Career CSV 的 SkillScript 列中（战斗开始执行）：
local key = "YourMod_YourCsv_skill_guard"
local st = CS.ScriptExecutor.PlayerInfo.SkillTime
if not st:ContainsKey(key) then st:set_Item(key, 0) end
self:AddEvent("StartRound", function()
    local s = CS.ScriptExecutor.PlayerInfo.SkillTime
    if s ~= nil and s:ContainsKey(key) then
        local cd = tonumber(s:get_Item(key)) or 0
        if cd > 0 then s:set_Item(key, cd - 1) end
    end
    self:SetStatus("Self")
    self:ChangePower("2")        -- 每回合回能量（ChangePower 在 ScriptExecutor 上，不是 PlayerInfo）
end)
self:AddEvent("EndRound", function()
    self:SetStatus("Self")
    local stacks = self.Self:GetBuff("buff_corruption")
    if stacks ~= nil and stacks.buffConfig.Level >= 5 then
        self:SetStatus("AllEnemy")
        self:Damage(tostring(stacks.buffConfig.Level * 3))
    end
end)
```

> ⚠️ 在 SkillScript 里 `self` 就是 ScriptExecutor：`self:AddEvent`、`self:ChangePower`、`self.Self:GetBuff`。
> 不要写 `ScriptExecutor.PlayerInfo.ChangePower(2)`（缺 `CS.` 且 `ChangePower` 不在 PlayerInfo 上）；能量恢复用 `self:ChangePower("2")`。

---

## 动画资源规范

```
AnimationLib/CareerName/
├── Idle/
│   ├── config.json     ← {"AnimationPerFrame": 0.1, "isLoop": true, "Direction": "Right"}
│   ├── Idle_00.png
│   ├── Idle_01.png
│   └── ...
├── Attack/
│   ├── config.json
│   └── Attack_00.png
├── Defend/
├── Hit/
└── Skill/
```

- `AnimationPerFrame`：每帧停留秒数
- `isLoop`：是否循环（Idle 循环，Attack/Skill 通常 false）
- `Direction`：**"Right" / "Left"**（不是 "row"），表示动画播放方向
- 帧图命名带动作前缀（`Idle_00.png`、`Attack_00.png`…），帧尺寸以实际资源为准

---

## 从现有职业 Mod 学习

| Mod | 职业 | 特色机制 | 复杂度 |
|-----|------|---------|--------|
| Mokou | 妹红 | 灼烧/回复/不死 | ★★★ 中等 |
| MoonRite | 月神 | 月光/月相/蚀刻 | ★★★★ 丰富 |
| SunExp | 多个角色 | 星能/日炎 多系统 | ★★★★★ 大型 |
| JogasakiNoah | Noah | 巫女形态/技能CG | ★★★★★ 含 BGM+CG |
| Plantago | 守卫 | 冷却/反伤/骰子 | ★★ 入门 |

建议先从 **Plantago** 或 **Mokou** 这类较简单的职业 Mod 目录结构开始学习，复制结构再逐步替换内容。
