# 用 Buff 做资源系统

多个大型 Mod 使用 Buff 来模拟游戏不直接支持的次级资源系统。
核心思路：**Buff 的层数 = 资源数量**。

---

## 模式 1：MP/能量系统（BlackMage）

**适用场景：** 需要一个独立的法力/能量条，不和游戏原有的 Power 系统冲突。

**实现方式：** 创建多个不可见的 Buff 来跟踪资源。

```lua
-- Entry.lua：Hook 战斗初始化，确保资源系统启用
function ModConfig:Setup()
    self:AddMethodHookAfter("Fight_PlayerTurn.Init", function(_)
        EnsurePlayerResources()
    end)
end

function EnsurePlayerResources()
    -- 初始化 MP（最大 100）
    local mp = StatusManager:GetStatus("buff_mp")
    if mp == nil or mp == 0 then
        StatusManager:AddStatus("buff_mp", 100, source, target)
    end
    -- 初始化元素状态（冰/火/雷）
    local fire = StatusManager:GetStatus("buff_astral_fire")
    if fire == nil then
        StatusManager:AddStatus("buff_astral_fire", 0, source, target)
    end
end
```

**卡牌中用 MP：**
```lua
-- 消耗 20 MP 造成 15 伤害
self.Vars.DesVal1 = tostring(15)
local mp = StatusManager:GetStatus("buff_mp")
if mp and mp >= 20 then
    StatusManager:AddStatus("buff_mp", -20, source, target) -- 负值 = 消耗
    self:Damage(15)
end
```

**关键点：**
- MP Buff 在战斗开始时初始化（`Fight_PlayerTurn.Init` Hook）
- 消耗 MP = 加负数层数
- 元素系统：三种 Buff（astral_fire / umbral_ice / thunder）分别跟踪层数
- 叠满 3 层可触发终极技能

---

## 模式 2：燃料/消耗系统（Mokou）

**适用场景：** 资源会随时间衰减或被消耗，用完后有惩罚或转换效果。

```lua
-- 卡牌效果：给自己加燃料
self:AddBuff(DataId.buff_fuel, "3")

-- 另一张卡：消耗 3 燃料转换
local fuel = StatusManager:GetStatus("buff_fuel")
if fuel and fuel >= 3 then
    StatusManager:RemoveStatus("buff_fuel", 3, source)
    -- 转换成回复 Buff
    StatusManager:AddStatus("buff_evergreen", "2", source, source)
end
```

**拓展：焚毁机制**
Mokou 的 "Fuel" 关键字系统让特定卡牌在弃置时有额外效果：

```lua
-- Entry.lua 中 Hook 卡牌弃置事件
self:AddMethodHookBefore("CardItem.OnDiscard", function(ctx)
    local card = ctx.Target
    if card and card.data["Tag"] == "Fuel" then
        -- 焚毁：执行特殊效果
        StatusManager:AddStatus("buff_rebirth", "1", source, source)
    end
end)
```

---

## 模式 3：麻将牌系统（PW_Mahjong）

**适用场景：** 大量同类型资源且每个资源需要有独立含义。

```lua
-- 每张卡牌打出时添加对应 Buff
-- 万子牌：
self:AddBuff(DataId.mahjong_wan_1, "1")  -- buff 层数 = 1
self:AddBuff(DataId.mahjong_majo, "1")    -- +1 摸牌
self:DrawCount(1)

-- C# 端胡牌检测（纯 Lua 对 34 种 Buff 扫描太慢）
-- CS.MJ.CardScripts.ScanAndCheck(self) 返回胡牌类型
local result = _G.MJ_ScanAndCheck(self)
-- result: -1=没有听牌, 0=标准胡, 1=七对子, 2=十三幺, 3=九莲宝灯
```

**关键点：**
- 34 种 Buff 对应 34 种麻将牌，Buff 存不存在比层数更重要
- 胡牌检测逻辑在 C# 端（性能考量）
- Python 脚本生成卡牌 CSV（手写 241 张不现实）

---

## 通用原则

1. **Buff 可见性**：如果 Buff 不需要在 UI 上显示，可以在 Text CSV 中把名称设为空格或不可见
2. **层数范围**：Buff 的 `MaxLayer` 控制最大堆叠数，超过后无效
3. **持续性**：通过 `AddEvent("EndRound")` 或 `AddEvent("StartRound")` 实现每回合自动变化
4. **跨战斗持久化**：使用 `SpecialVars`（见 cooldown-dice.md）

```lua
-- 在战斗结束时保存资源到跨战斗存储
self:AddEvent("Win", function()
    local mp = StatusManager:GetStatus("buff_mp")
    if mp then
        SpecialVars["stored_mp"] = mp
    end
end)

-- 在战斗开始时恢复
self:AddEvent("StartRound", function()
    local stored = SpecialVars["stored_mp"]
    if stored then
        StatusManager:AddStatus("buff_mp", stored, source, target)
    end
end)
```
