# 卡牌转换与动态生成

多个 Mod 实现了"卡牌 A 打出后变成卡牌 B"或"根据条件生成动态卡牌"的机制。

---

## 模式 1：直接卡牌转换（Mokou）

**适用场景：** 卡牌使用后有副作用，或需要切换到另一种形态。

```lua
-- 在 UseScript 中：
self:SetStatus("AllEnemy")
self:Damage(10)

-- 移除燃料后转换成另一张牌
StatusManager:RemoveStatus("buff_fuel", 3, source)
FightManager.Inst:FightAddCard("Mokou_Card_102")
```

FightAddCard 把手牌外的卡牌直接加入手牌，
常用于"打出后生成"或"回合结束时获得衍生卡"。

---

## 模式 2：转换形态（JogasakiNoah 巫女形态）

**适用场景：** 角色进入/退出某种强化形态。

```lua
-- Entry.lua 中 Hook 职业选择界面，隐藏基础职业
self:AddMethodHookBefore("GameEntryUI.ShowCareer", function(ctx)
    -- 从可用职业列表中过滤掉基础职业
    local careers = ctx.Arguments[1]
    for i = #careers, 1, -1 do
        if careers[i] == "jobasakinoah_base" then
            table.remove(careers, i)
            break
        end
    end
end)
```

卡牌中使用：

```lua
-- 进入巫女形态：应用形态 Buff
self:AddBuff(DataId.buff_witch_form, "1")

-- 形态 Buff 的 ApplyScript 中：
-- 1. HP/能量提升
-- 2. BGM 切换到战斗音乐
-- 3. 播放变身动画（CG 覆盖层）
-- 4. 切换角色立绘
```

---

## 模式 3：伴星系统（EdictOfStars）

**适用场景：** 有一个"随从"或"分身"会在战斗中反复出现。

```
Astral Companion（伴星）机制：
- 每回合生成一张伴星卡到手牌
- 伴星卡打出后消失（Exhaust）
- 伴星卡效果受当前"星象"Buff 影响
```

```lua
-- Entry.lua 中每回合添加伴星
self:AddMethodHookAfter("FightManager.StartPlayerTurn", function(ctx)
    -- 检查是否有伴星技能
    local hasCompanion = StatusManager:GetStatus("buff_astral_companion")
    if hasCompanion and hasCompanion > 0 then
        -- 添加伴星卡到手牌
        FightManager.Inst:FightAddCard("EdictOfStars_starcards_companion_attack")
    end
end)
```

伴星卡牌的 UseScript：

```lua
self:SetStatus("Target")       -- 指定目标
-- 根据当前星象 Buff 类型决定效果
local star = StatusManager:GetStatus("buff_blooming")
if star == 1 then
    self:Damage(8)
elseif star == 2 then
    self:Defend(5)
    self:Damage(4)
else
    self:Damage(6)
    self:AddBuff(DataId.buff_hui_ke, "2")
end
```

---

## 模式 4：Python 生成大量卡牌（PW_Mahjong）

**适用场景：** 有大量同质卡牌（170+ 张），手写 CSV 不现实。

```python
# gen_all.py — 生成 170+ 张麻将牌
tiles = ["wan_1", "wan_2", ..., "tiao_1", ..., "tong_1", ...]
magic_types = ["fire", "ice", "thunder", "light", "dark"]

for tile in tiles:
    for magic in magic_types:
        card_id = f"mahjong_{tile}_{magic}"
        use_script = f'self:AddBuff(DataId.{tile}, "1"); self:AddBuff(DataId.majo, "1"); self:DrawCount(1)'
        # 写 CSV 行
        ...

# gen_text.py — 生成 4 语言文本
for tile in tiles:
    for lang in ["en", "zh-Hans", "zh-Hant", "ja"]:
        # 写 Text CSV 行
        ...
```

**优点：**
- 修改生成逻辑后重新跑脚本即可更新全部 CSV
- 保证 ID 命名一致
- 容易做排列组合

---

## 卡牌 ID 管理

衍生卡和生成卡建议用统一的 ID 规则：

```
# 战斗内生成/转换的卡用 * 前缀排除随机池
*MyMod_Card_transform_001
*MyMod_Card_companion_attack

# Python 生成的卡用顺序 ID
MyMod_Card_1001 ~ MyMod_Card_1240
```
