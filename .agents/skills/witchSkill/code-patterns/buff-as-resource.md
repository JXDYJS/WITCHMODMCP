# 用 Buff 做资源系统

多个大型 Mod 使用 Buff 来模拟游戏不直接支持的次级资源系统。
核心思路：**Buff 的层数 = 资源数量**。

> **⚠️ Buff API（全部为真实方法，`StatusManager.cs` / `ScriptExecutor.cs`）：**
> - 读：`status:GetBuff(id)` → 返回 `IBuffItem` 或 nil，层数 = `buff.buffConfig.Level`
> - 写：`AddBuff(id, level)`——层数**累加**（`Level += level`：正数叠层、负数扣层）。level 类型：**StatusManager 上必须传数字**（`player.Status:AddBuff`、`self.Self:AddBuff`），**ScriptExecutor 上数字/字符串均可**（卡牌 UseScript 的 `self:AddBuff`）
> - 删：`status:RemoveBuff(id)`
> - 精确设置/扣减层数：直接改 `buff.buffConfig.Level = X`（setter 自动钳到 `UpperBound`、`<0→0`；0 层且 `CanZero=false` 时清除该 Buff）
> - **不存在** `StatusManager:GetStatus/AddStatus/RemoveStatus`，别用。
> - Buff 运行时 ID 格式：`{ModFolder}_{CsvFile}_{RawId}`（如 BlackMage 的 `BlackMage_blackmage_mp`）。

---

## 模式 1：MP/能量系统（BlackMage）

**适用场景：** 需要一个独立的法力/能量条，不和游戏原有的 Power 系统冲突。

**实现方式：** 战斗开始时用 Hook 确保资源 Buff 存在。

```lua
-- Entry.lua：Hook 战斗初始化，确保资源系统启用
local MP_BUFF_ID = "BlackMage_blackmage_mp"
local INITIAL_MP = 40

local function EnsurePlayerResources()
    local player = CS.FightPlayer.Instance
    if player == nil or player.Status == nil then return end
    local status = player.Status
    if status:GetBuff(MP_BUFF_ID) == nil then
        status:AddBuff(MP_BUFF_ID, INITIAL_MP)
    end
end

function ModConfig:Setup()
    self:AddMethodHookAfter("Fight_PlayerTurn.Init", function(_)
        EnsurePlayerResources()
    end)
end
```

**卡牌中用 MP：**
```lua
-- 消耗 20 MP 造成 15 伤害（self = ScriptExecutor）
self:SetStatus("Self")
local mp = self.Self:GetBuff(MP_BUFF_ID)
if mp ~= nil and mp.buffConfig.Level >= 20 then
    mp.buffConfig.Level = mp.buffConfig.Level - 20   -- 扣减 = 直接改 Level
    self:SetStatus("AllEnemy")
    self:Damage("15")
end
```

**关键点：**
- MP Buff 在战斗开始时初始化（`Fight_PlayerTurn.Init` Hook）
- 扣减 MP = 直接改 `buff.buffConfig.Level`（精确设置；也可负值 `AddBuff`——对已存在的 Buff 即 `Level += 负值`，扣到 ≤0 会按 `CanZero` 决定保留/清除）
- 元素系统：三种 Buff（如 `BlackMage_blackmage_astral_fire` / `_umbral_ice` / `_thunder`）分别跟踪层数，叠满 3 层可触发终极技能

---

## 模式 2：燃烧/回复资源（Mokou）

**适用场景：** 资源随攻击/回合累积，达到层数后转换或触发效果。

Mokou 的真实资源对：`buff_burn`（燃烧层数，扣血/被使用）与 `buff_evergreen`（回复层数，回血）。

```lua
-- 卡牌 UseScript：根据燃烧层数转化为回复层数
self:SetStatus("Self")
local burn = self.Self:GetBuff("buff_burn")
local amount = 0
if burn ~= nil then amount = burn.buffConfig.Level end
if amount > 0 then
    self:AddBuff("buff_evergreen", tostring(amount))
end
```

**焚毁机制（Fuel 关键字）：** 让特定卡牌在弃置/被燃烧时有额外效果，用 Hook 拦截**真实方法** `CardItem.EffectOfBurnCard`（不是 `OnDiscard`，后者不存在）：

```lua
-- Entry.lua 中 Hook 卡牌焚毁事件（真实方法名，回调首参就是 CardItem 实例）
function ModConfig:Setup()
    self:AddMethodHookBefore("CardItem.EffectOfBurnCard", function(cardItem)
        local id = cardItem.dataConfig.data:get_Item("Id")
        if id == "YourMod_YourCsv_fuel_card" then
            local vars = cardItem.dataConfig.Vars
            if vars ~= nil and vars:ContainsKey("SkipFuel") and vars:get_Item("SkipFuel") == "1" then
                vars:set_Item("SkipFuel", "0")
                return
            end
            -- 焚毁：把这张卡重新生成到手牌（FightUI:CreateCardItem）
            local fightUI = CS.Witch.UI.UIManager.Instance:Find("FightUI")
            if fightUI ~= nil then
                fightUI:CreateCardItem(cardItem.dataConfig)
            end
        end
    end)
end
```

---

## 模式 3：麻将牌系统（PW_Mahjong）

**适用场景：** 大量同类型资源且每个资源需要有独立含义。

```lua
-- 34 种 Buff 对应 34 种麻将牌（运行时 ID 数组，见 PW_Mahjong Entry.lua）
_G.MJ_TILE_IDS = {
    "PW_Mahjong_buff_mahjong_1wan", "PW_Mahjong_buff_mahjong_2wan", -- ...
    "PW_Mahjong_buff_mahjong_dong", "PW_Mahjong_buff_mahjong_nan", -- ...
}

-- 卡牌 UseScript：打出一张牌 → 加对应 Buff + 摸牌
self:AddBuff("PW_Mahjong_buff_mahjong_1wan", "1")     -- buff 存在即视为持有该牌
self:AddBuff("PW_Mahjong_buff_mahjong_majo", "1")     -- +1 摸牌
self:DrawCount("1")

-- 胡牌检测：C# 端（纯 Lua 扫 34 种 Buff 太慢）
-- Entry.lua 里 _G.MJ_ScanAndCheck 封装 CS.MJ.CardScripts.ScanAndCheck(self)
local result = _G.MJ_ScanAndCheck(self)
-- result: -1=没有听牌, 0=标准胡, 1=七对子, 2=十三幺, 3=九莲宝灯
```

**关键点：**
- 34 种 Buff 对应 34 种麻将牌，**Buff 存不存在比层数更重要**
- 胡牌检测逻辑在 C# 端（性能考量）
- Python 脚本生成卡牌 CSV（手写 241 张不现实）
- 不要用 `DataId.mahjong_xxx`——DataId 只含游戏内置 ID，Mod 数据没有常量，直接用运行时 ID 字符串

---

## 通用原则

1. **Buff 可见性**：如果 Buff 不需要在 UI 上显示，可以在 Text CSV 中把名称设为空格或不可见
2. **层数范围**：Buff 的 `UpperBound` 列（不是 MaxLayer）控制最大堆叠数，超过后无效（列名见 `references/csv-schemas.md` 的 `Data/Buff/buff.csv`）
3. **持续性**：通过 `self:AddEvent("EndRound")` 或 `AddEvent("StartRound")` 实现每回合自动变化（事件名来自 `EventType.cs`，真实存在）
4. **跨战斗持久化**：用 `CS.ScriptExecutor.PlayerInfo.SpecialVars`（`Dictionary<string,string>`，必须用 `:set_Item/:get_Item/:ContainsKey`），并在 Entry.lua 的战斗结束 Hook 里保存、战斗开始 Hook 里恢复：

```lua
-- Entry.lua：战斗结束保存
function ModConfig:Setup()
    self:AddMethodHookAfter("Fight_Win.ResetStates", function()
        SaveMP()
    end)
    self:AddMethodHookAfter("Fight_Escape.ResetStates", SaveMP)
    self:AddMethodHookAfter("Fight_Loss.Init", SaveMP)
    self:AddMethodHookAfter("Fight_Start.Init", RestoreMP)
end

local function SaveMP()
    local vars = CS.ScriptExecutor.PlayerInfo.SpecialVars
    if vars == nil then return end
    local player = CS.FightPlayer.Instance
    if player == nil or player.Status == nil then return end
    local mp = player.Status:GetBuff(MP_BUFF_ID)
    if mp ~= nil then
        vars:set_Item("stored_mp", tostring(mp.buffConfig.Level))
    end
end

local function RestoreMP()
    local vars = CS.ScriptExecutor.PlayerInfo.SpecialVars
    if vars == nil or not vars:ContainsKey("stored_mp") then return end
    local player = CS.FightPlayer.Instance
    if player == nil or player.Status == nil then return end
    player.Status:AddBuff(MP_BUFF_ID, tonumber(vars:get_Item("stored_mp")) or 0)
end
```

> ⚠️ `SpecialVars` 没有 `SaveSpecialVars()` 方法；不需要手动保存，字典本身就是存档（写入即持久）。
> ⚠️ 卡牌 executor 上注册的事件（`self:AddEvent`）随卡牌生命周期——弃置/消耗/战斗结束即失效，**不要**用它做跨战斗持久化。跨战斗逻辑一律放 Entry.lua 的 Hook（`Fight_Win.ResetStates` / `Fight_Escape.ResetStates` / `Fight_Loss.Init` 保存、`Fight_Start.Init` 恢复）+ `SpecialVars`。
