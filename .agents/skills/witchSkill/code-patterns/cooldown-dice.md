# 冷却、骰子、里程碑与相位循环

这些模式管理卡牌/技能的触发频率、随机性、长期进度和状态切换。

> **⚠️ 冷却的真实存储：`CS.ScriptExecutor.PlayerInfo.SkillTime`（`Dictionary<string,int>`，等价于 `RoleTable.Instance.SkillTime`）。**
> xLua 访问 C# 字典必须用 `:ContainsKey(key)` / `:set_Item(key, v)` / `:get_Item(key)`。
> 卡牌自己的 `self.Vars` 是 `IDictionary<string,string>`，**不是**冷却存储。

---

## 模式 1：冷却系统（Plantago / Mokou）

**适用场景：** 强力的主动技能不应该每回合都能用。

**真实做法：** 冷却存在 `PlayerInfo.SkillTime`，用 `AddEvent("StartRound")` 每回合递减，`UseScript` 里检查。

**职业 SkillScript（每场战斗都执行）：初始化冷却 + 每回合递减**

```lua
-- Career CSV 的 SkillScript 列中（key = 技能/卡的运行时 ID）
local key = "YourMod_YourCsv_skill_tail"
local st = CS.ScriptExecutor.PlayerInfo.SkillTime
if not st:ContainsKey(key) then
    st:set_Item(key, 0)
end

-- 每回合递减（AddEvent("StartRound") 事件真实存在）
self:AddEvent("StartRound", function()
    local s = CS.ScriptExecutor.PlayerInfo.SkillTime
    if s == nil then return end
    if s:ContainsKey(key) then
        local cd = tonumber(s:get_Item(key)) or 0
        if cd > 0 then s:set_Item(key, cd - 1) end
    end
end)
```

**卡牌 UseScript：检查冷却、触发效果并重置**

```lua
local key = "YourMod_YourCsv_skill_tail"
local st2 = CS.ScriptExecutor.PlayerInfo.SkillTime
local cd = 0
if st2 ~= nil and st2:ContainsKey(key) then cd = tonumber(st2:get_Item(key)) or 0 end
if cd > 0 then
    CS.ScriptExecutor.PlayerInfo.ShowCaption("还在冷却中")
    return
end
-- 触发强力效果并设置冷却 3 回合
st2:set_Item(key, 3)
self.Vars:set_Item("DesVal1", "3")   -- 刷新卡面显示（Vars 用 :set_Item，不是 self.Vars.DesVal1=）
self:SetStatus("AllEnemy")
self:Damage("30")
```

**冷却追踪变体：** 用可见 Buff 层数 = 剩余冷却回合数

```lua
-- InitScript：初始化冷却 Buff
self:AddBuff("YourMod_YourCsv_cooldown", "3")
self.Vars:set_Item("DesVal1", "3")

-- UseScript 中检查冷却 Buff
self:SetStatus("Self")
local cd = self.Self:GetBuff("YourMod_YourCsv_cooldown")
if cd == nil or cd.buffConfig.Level == 0 then
    -- 技能可用，重置冷却
    self:SetStatus("Self")
    self:AddBuff("YourMod_YourCsv_cooldown", "3")
else
    CS.ScriptExecutor.PlayerInfo.ShowCaption("冷却中")
    return
end
```

---

## 模式 2：骰子系统（Plantago）

**适用场景：** 卡牌效果有概率浮动，或根据运气决定强度。

**真实做法：** 用 `math.random` + 幸运值（Mokou `CheckSuccess`），或游戏内置 `PlayerInfo.DefaultRoll`（int）。

```lua
-- 卡牌 UseScript 中的判定（幸运值修正）
local lucky = tonumber(CS.ScriptExecutor.PlayerInfo.Lucky) or 0
local roll = math.random(1, 100)
if lucky + roll >= 70 then
    -- 大成功：全额效果
    self:SetStatus("AllEnemy")
    self:Damage("15")
    self:SetStatus("Self")
    self:AddBuff("buff_counterattack", "3")
elseif lucky + roll >= 40 then
    -- 普通成功
    self:SetStatus("AllEnemy")
    self:Damage("10")
else
    -- 失败：效果减半
    self:SetStatus("AllEnemy")
    self:Damage("5")
end
```

> ⚠️ 不要用 `Dice.Roll()`——它返回 `Dice.State`（`int Value`），不是 0~1 浮点，而且 Lua 环境里没有全局 `Dice`。随机数用 `math.random` 或 `PlayerInfo.DefaultRoll`。

---

## 模式 3：里程碑系统（Muga）

**适用场景：** 资源/计数器跨战斗积累，达到阈值解锁新能力。

```lua
-- 跨战斗持久计数器：用 PlayerInfo.SpecialVars（Dictionary<string,string>，:set_Item/:get_Item/:ContainsKey）
-- Entry.lua 中 Hook 战斗结束保存
function ModConfig:Setup()
    self:AddMethodHookAfter("Fight_Win.ResetStates", SaveCount)
    self:AddMethodHookAfter("Fight_Escape.ResetStates", SaveCount)
    self:AddMethodHookAfter("Fight_Loss.Init", SaveCount)
    self:AddMethodHookAfter("Fight_Start.Init", CheckMilestone)
end

local function SaveCount()
    local vars = CS.ScriptExecutor.PlayerInfo.SpecialVars
    if vars == nil then return end
    local player = CS.FightPlayer.Instance
    if player == nil or player.Status == nil then return end
    local fuel = player.Status:GetBuff("YourMod_YourCsv_fuel")
    local total = tonumber(vars:get_Item("wuwo_counter") or "0") or 0
    if fuel ~= nil then total = total + fuel.buffConfig.Level end
    vars:set_Item("wuwo_counter", tostring(total))
end

local function CheckMilestone()
    local vars = CS.ScriptExecutor.PlayerInfo.SpecialVars
    if vars == nil then return end
    local count = tonumber(vars:get_Item("wuwo_counter") or "0") or 0
    local player = CS.FightPlayer.Instance
    if player == nil or player.Status == nil then return end
    if count >= 100 and not vars:ContainsKey("milestone_100") then
        player.Status:AddBuff("buff_polished_art", 1)      -- StatusManager.AddBuff 的 level 必须是数字
        vars:set_Item("milestone_100", "1")
    elseif count >= 50 and not vars:ContainsKey("milestone_50") then
        player.Status:AddBuff("YourMod_YourCsv_milestone_buff", 1)
        vars:set_Item("milestone_50", "1")
    end
end
```

> ⚠️ `SpecialVars` 写入即持久，**没有 `SaveSpecialVars()` 方法**。

**里程碑 Buff 的特殊设计：** 用 `CanZero=True` 的 Buff 作为"已激活"标记（真实列名/表头）

```csv
# Buff CSV 中（真实表头含 CanZero；层数上限列是 UpperBound 不是 MaxLayer，全列见 references/csv-schemas.md 的 Data/Buff/buff.csv）
Id,InitScript,ApplyScript,ClearScript,ReducePerTurn,ReducePerAttacked,ReducePerUse,UpperBound,Icon,Type,Rarity,Effects,SoundEffects,Action,CanZero
buff_milestone_50,,,,0,0,0,1,,,3,,,TRUE
```

`CanZero=True` 的 Buff 在 0 层仍会存在（源码 `BuffItemConfig`：`level==0 && !CanZero` 才会 `ClearBuff`），
适合用作"永久已激活"标记。

---

## 模式 4：相位循环系统（MoonRite）

**适用场景：** 资源/状态按照固定顺序循环，每一阶段有不同效果。

```
月相循环：新月(1) → 弦月(2) → 满月(3) → 残月(4) → 新月(1)
```

```lua
-- InitScript 中获取当前月相（Buff API）
self:SetStatus("Self")
local phase = self.Self:GetBuff("YourMod_YourCsv_moon_phase")
if phase == nil then
    self:AddBuff("YourMod_YourCsv_moon_phase", "1")
    phase = self.Self:GetBuff("YourMod_YourCsv_moon_phase")
end

-- UseScript 中根据月相决定效果
self:SetStatus("Self")
local moonlight = self.Self:GetBuff("YourMod_YourCsv_moonlight")
if moonlight ~= nil and moonlight.buffConfig.Level > 0 then
    if phase.buffConfig.Level == 3 then
        -- 满月：伤害加成
        self:SetStatus("Target")
        self:Damage(tostring(moonlight.buffConfig.Level * 2))
    elseif phase.buffConfig.Level == 1 then
        -- 新月：控制效果
        self:SetStatus("Target")
        self:AddBuff("YourMod_YourCsv_eclipse_mark", tostring(moonlight.buffConfig.Level))
    end
    -- 消耗月光推进月相（直接改 Level 扣减）
    self:SetStatus("Self")
    local ph = self.Self:GetBuff("YourMod_YourCsv_moon_phase")
    if ph ~= nil then
        ph.buffConfig.Level = (ph.buffConfig.Level % 4) + 1
    end
end
```
