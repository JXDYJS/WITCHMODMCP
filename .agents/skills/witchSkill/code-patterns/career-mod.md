# 完整职业 Mod 架构

从多个已有职业 Mod（Mokou、MoonRite、SunExp、JogasakiNoah、Defect）提炼的完整职业创建指南。

---

## 必需文件清单

一个完整的职业 Mod 至少需要：

```
YourCareer/
├── ModConfig.json
├── Scripts/Entry.lua
├── Data/
│   ├── Career/career.csv            ← 职业定义
│   ├── Card/cards.csv               ← 卡牌定义
│   ├── CardPack/cardpack.csv        ← 卡包定义
│   ├── Buff/buffs.csv               ← Buff 定义（至少职业特色 Buff）
│   ├── RoleData/roledata.csv        ← 角色数据
│   └── Relic/relics.csv             ← 圣物（可选）
├── Text/
│   ├── Career/career.csv
│   ├── Card/cards.csv
│   ├── CardPack/cardpack.csv
│   ├── Buff/buffs.csv
│   ├── RoleData/roledata.csv
│   ├── Relic/relics.csv
│   └── KeyWordsDic/keywords.csv     ← 关键词词典
└── ModResource/
    ├── AnimationLib/CareerName/     ← 战斗动画
    │   ├── Idle/
    │   ├── Attack/
    │   ├── Defend/
    │   ├── Hit/
    │   └── Skill/
    └── Images/                      ← 立绘/头像
```

---

## Career CSV 配置

```csv
Id,Name_zh-Hans,Name_zh-Hant,Name_en,Name_ja,SanMax,HpMax,RoleDataId,CardAsset,CardList,RelicList,PartnerList,Attribute,PackBelong
# id,名称,繁中,英文,日文,理智,血量,角色数据id,卡背,初始卡组,初始圣物,初始随从,属性模板,所属卡包
1001,灾祸术士,災禍術士,Warlock,ウォーロック,100,75,1001,CardAsset_Warlock,*1001,*1001,,Strength,Pack_Warlock
```

**关键列：**
- `SanMax` / `HpMax` — 角色基础属性
- `RoleDataId` — 指向 RoleData CSV 中的条目（定义角色图像资源路径）
- `CardAsset` — 卡背图像（在 ModResource/Images/ 中）
- `CardList` — 初始卡组 ID 列表，用 `*` 前缀的卡不会进入随机池
- `RelicList` — 初始圣物
- `Attribute` — 属性模板（Strength / Lucky / Perceive / Wisdom 的组合）
- `PackBelong` — 关联的卡包 ID

---

## RoleData CSV

```csv
Id,Name,AnimationLib,Avatar,CareerImage,Character,AttackEffect,SkillEffect,HitEffect,DefendEffect
# id,名称,动画库,头像,职业图片,角色图片,攻击特效,技能特效,受击特效,防御特效
1001,灾祸术士,AnimationLib/Warlock,Avatar_Warlock,CareerImage_Warlock,Character_Warlock,AttackEffect,SkillEffect,HitEffect,DefendEffect
```

所有图片路径指向 `ModResource/Images/` 下的文件（不带扩展名）。

---

## 角色技能（Career SkillScript）

职业的 `SkillScript` 列可以包含 Lua 代码，用于实现角色的被动技能：

```lua
-- Career CSV 的 SkillScript 列中：
-- 初始化代码块（战斗开始时执行）
if self.Vars.SkillTime == nil then
    self.Vars.SkillTime = 0
    -- 添加事件监听
    self:AddEvent("StartRound", function()
        self.Vars.SkillTime = self.Vars.SkillTime + 1
        -- 每回合回复 2 点能量
        ScriptExecutor.PlayerInfo.ChangePower(2)
    end)
    self:AddEvent("EndRound", function()
        -- 回合结束时如果有 Buff 则触发额外效果
        local stacks = StatusManager:GetStatus("buff_corruption")
        if stacks and stacks >= 5 then
            self:SetStatus("AllEnemy")
            self:Damage(stacks * 3)
        end
    end)
end
```

---

## 动画资源规范

```
AnimationLib/CareerName/
├── Idle/
│   ├── config.json     ← {"AnimationPerFrame": 0.1, "isLoop": true, "Direction": "row"}
│   ├── frame_0.png     ← 300×300 PNG
│   ├── frame_1.png
│   └── ...
├── Attack/
│   ├── config.json
│   └── frame_0.png
├── Defend/
├── Hit/
└── Skill/
```

- 帧尺寸：300×300 像素
- `AnimationPerFrame`：每帧停留秒数
- `isLoop`：是否循环

---

## 从现有职业 Mod 学习

| Mod | 职业 | 特色机制 | 复杂度 |
|-----|------|---------|--------|
| Defect | 故障机器人 | 球位（冰/电/等离子） | ★★★ 中等 |
| Mokou | 妹红 | 灼烧/燃料/重生 | ★★★ 中等 |
| MoonRite | 月神 | 月光/月相/蚀刻 | ★★★★ 丰富 |
| SunExp | 多个角色 | 星能/日炎 多系统 | ★★★★★ 大型 |
| JogasakiNoah | Noah | 巫女形态/技能CG | ★★★★★ 含 BGM+CG |

建议先从 **Defect** 示例开始学习，复制它的结构再逐步替换内容。
