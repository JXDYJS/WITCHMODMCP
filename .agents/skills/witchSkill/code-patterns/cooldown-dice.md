# 冷却、骰子、里程碑与相位循环

这些模式管理卡牌/技能的触发频率、随机性、长期进度和状态切换。

---

## 模式 1：冷却系统（Plantago）

**适用场景：** 强力的主动技能不应该每回合都能用。

```lua
-- 在卡牌的 InitScript 中初始化：
if self.Vars.SkillTime == nil then
    self.Vars.SkillTime = 0
end

-- 在 Entry.lua 中每回合递增：
function ModConfig:Setup()
    self:AddMethodHookAfter("FightManager.StartPlayerTurn", function(ctx)
        -- 遍历所有 Buff，递增 SkillTime
    end)
end

-- 在卡牌的 UseScript 中检查冷却：
if self.Vars.SkillTime >= 3 then  -- 冷却 3 回合
    self.Vars.SkillTime = 0        -- 重置
    -- 触发强力效果
    self:SetStatus("AllEnemy")
    self:Damage(30)
else
    -- 未冷却完成时效果减半
    self:SetStatus("AllEnemy")
    self:Damage(10)
end
```

**冷却追踪变体：** 用 Visible Buff 显示冷却状态

```lua
-- 使用 Buff 层数 = 剩余冷却回合数
-- InitScript:
local remaining = 3
self.Vars.DesVal1 = tostring(remaining)
self:AddBuff(DataId.buff_cooldown, tostring(remaining))

-- 使用脚本中检查冷却 Buff
local cd = StatusManager:GetStatus("buff_cooldown")
if cd == nil or cd == 0 then
    -- 技能可用
    self:AddBuff(DataId.buff_cooldown, "3")  -- 重置冷却
end
```

---

## 模式 2：骰子系统（Plantago）

**适用场景：** 卡牌效果有概率浮动，或根据运气决定强度。

```lua
-- Entry.lua 中 Hook 战斗初始化
function ModConfig:Setup()
    self:AddMethodHookAfter("Fight_PlayerTurn.Init", function(_)
        -- 初始化幸运值
        local lucky = RoleTable.Inst.Lucky
        SpecialVars["base_lucky"] = lucky
    end)
end

-- 卡牌 UseScript 中的骰子检测：
local roll = Dice.Roll()  -- 返回 0~1 之间的值
if roll > 0.7 then
    -- 大成功：全额效果
    self:Damage(15)
    self:AddBuff(DataId.buff_counterattack, "3")
elseif roll > 0.3 then
    -- 普通成功
    self:Damage(10)
else
    -- 失败：效果减半
    self:Damage(5)
end
```

**用幸运值修正骰子：**

```lua
-- 卡牌 UseScript：
local lucky = RoleTable.Inst.Lucky
local roll = Dice.Roll() + (lucky * 0.02)  -- 每点幸运 +2% 成功率
if roll > 0.6 then
    -- 成功路径
else
    -- 失败路径
end
```

---

## 模式 3：里程碑系统（Muga）

**适用场景：** 资源/计数器跨战斗积累，达到阈值解锁新能力。

```lua
-- 跨战斗持久计数器：使用 SpecialVars
-- Entry.lua 中 Hook 战斗结束
function ModConfig:Setup()
    self:AddMethodHookAfter("FightManager.OnFightEnd", function(ctx)
        -- 战斗结束时保存资源
        local fuel = StatusManager:GetStatus("buff_fuel")
        if fuel then
            SpecialVars["wuwo_counter"] = (SpecialVars["wuwo_counter"] or 0) + fuel
            SaveSpecialVars()
        end
    end)
end

-- 新战斗开始时检查里程碑
self:AddMethodHookAfter("FightManager.OnFightStart", function(ctx)
    local count = SpecialVars["wuwo_counter"] or 0
    if count >= 100 and not SpecialVars["milestone_100"] then
        -- 达到 100 层，授予永久 Buff
        StatusManager:AddStatus("buff_polished_art", "1", player, player)
        SpecialVars["milestone_100"] = true
    elseif count >= 50 and not SpecialVars["milestone_50"] then
        StatusManager:AddStatus("buff_milestone_buff", "1", player, player)
        SpecialVars["milestone_50"] = true
    end
end)
```

**里程碑 Buff 的特殊设计：** 用 `CanZero=True` 的 Buff 作为"已激活"标记

```csv
# Buff CSV 中：
Id,MaxLayer,CanZero,Type,Icon,InitScript
buff_milestone_50,1,TRUE,buff,,self:AddEvent("Win", milestoneHandler)
```

`CanZero=True` 的 Buff 即使在 0 层也会存在并触发事件处理函数，
适合用作"永久已激活"标记。

---

## 模式 4：相位循环系统（MoonRite）

**适用场景：** 资源/状态按照固定顺序循环，每一阶段有不同效果。

```
月相循环：新月(1) → 弦月(2) → 满月(3) → 残月(4) → 新月(1)
```

```lua
-- InitScript 中获取当前月相
local phase = StatusManager:GetStatus("buff_moon_phase")
if phase == nil then
    phase = 1
    StatusManager:AddStatus("buff_moon_phase", "1", source, target)
end

-- UseScript 中根据月相决定效果
local moonlight = StatusManager:GetStatus("buff_moonlight")
if moonlight and moonlight > 0 then
    if phase == 3 then
        -- 满月：伤害加成
        self:Damage(moonlight * 2)
    elseif phase == 1 then
        -- 新月：控制效果
        self:AddBuff(DataId.buff_eclipse_mark, tostring(moonlight))
    end
    -- 消耗月光推进月相
    local nextPhase = (phase % 4) + 1
    StatusManager:AddStatus("buff_moon_phase", tostring(nextPhase - phase))
end
```
