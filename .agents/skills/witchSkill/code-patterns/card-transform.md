# 卡牌转换与动态生成

多个 Mod 实现了"卡牌 A 打出后变成卡牌 B"或"根据条件生成动态卡牌"的机制。

> **⚠️ 生成/添加卡牌的真实 API：**
> - **加入手牌**：`FightUI:CreateCardItem(dataConfig)`（Mokou/EdictOfStars 用）。拿 FightUI：`CS.Witch.UI.UIManager.Instance:Find("FightUI")`
> - **加入抽牌堆**：`self:AddCard(id)`（`ScriptExecutor.AddCard`，源码里是往 `FightCardManager.cardList` 加）
> - `FightManager.Inst:FightAddCard(...)` **不存在**，单例是 `FightManager.Instance`，别用。

---

## 模式 1：直接卡牌转换（Mokou）

**适用场景：** 卡牌使用后有副作用，或需要切换到另一种形态。

```lua
-- 在 UseScript 中：
self:SetStatus("AllEnemy")
self:Damage("10")

-- 移除燃料后转换成另一张牌（用 Buff API 扣减）
self:SetStatus("Self")
local fuel = self.Self:GetBuff("YourMod_YourCsv_fuel")
if fuel ~= nil and fuel.buffConfig.Level >= 3 then
    fuel.buffConfig.Level = fuel.buffConfig.Level - 3
    local fightUI = CS.Witch.UI.UIManager.Instance:Find("FightUI")
    if fightUI ~= nil then
        fightUI:CreateCardItem(CS.DataConfig("YourMod_YourCsv_card_102", CS.DataType.Card))
    end
end
```

`FightUI:CreateCardItem(dataConfig)` 把一张卡直接做成手牌里的 CardItem，
常用于"打出后生成"或"回合结束时获得衍生卡"。

---

## 模式 2：转换形态（JogasakiNoah 巫女形态）

**适用场景：** 角色进入/退出某种强化形态。

```lua
-- Entry.lua 中 Hook 职业选择界面，隐藏基础职业（回调首参 = GameEntryUI 实例）
function ModConfig:Setup()
    self:AddMethodHookBefore("GameEntryUI.ShowCareer", function(ui)
        -- 从可用职业列表中过滤掉基础职业（真实做法见 JogasakiNoah）
        pcall(hide_base_career, ui)
    end)
end
```

卡牌中使用（`DataId.buff_xxx` 是游戏内置 ID 常量；Mod 自建 Buff 用运行时 ID 字符串）：

```lua
-- 进入巫女形态：应用形态 Buff
self:SetStatus("Self")
self:AddBuff("YourMod_YourCsv_witch_form", "1")

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
-- Entry.lua 中每回合添加伴星（hook 真实方法 Fight_PlayerTurn.Init）
function ModConfig:Setup()
    self:AddMethodHookAfter("Fight_PlayerTurn.Init", function(_)
        local player = CS.FightPlayer.Instance
        if player == nil or player.Status == nil then return end
        local hasCompanion = player.Status:GetBuff("YourMod_YourCsv_astral_companion")
        if hasCompanion ~= nil and hasCompanion.buffConfig.Level > 0 then
            local fightUI = CS.Witch.UI.UIManager.Instance:Find("FightUI")
            if fightUI ~= nil then
                fightUI:CreateCardItem(CS.DataConfig("YourMod_YourCsv_companion_attack", CS.DataType.Card))
            end
        end
    end)
end
```

伴星卡牌的 UseScript（`ChangeDefence` 是加防御的真实方法，`Defend` 不存在）：

```lua
self:SetStatus("Target")       -- 指定目标
-- 根据当前星象 Buff 类型决定效果
self:SetStatus("Self")
local star = self.Self:GetBuff("YourMod_YourCsv_blooming")
local lv = 0
if star ~= nil then lv = star.buffConfig.Level end
if lv == 1 then
    self:SetStatus("Target")
    self:Damage("8")
elseif lv == 2 then
    self:SetStatus("Target")
    self:ChangeDefence("5")
    self:Damage("4")
else
    self:SetStatus("Target")
    self:Damage("6")
    self:AddBuff("YourMod_YourCsv_hui_ke", "2")
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
        use_script = f'self:AddBuff("YourMod_YourCsv_{tile}", "1"); self:AddBuff("YourMod_YourCsv_majo", "1"); self:DrawCount("1")'
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
# 战斗内生成/转换的卡用 * 前缀排除随机池（真实 Mod 通用约定）
*YourMod_Card_transform_001
*YourMod_Card_companion_attack

# Python 生成的卡用顺序 ID
YourMod_Card_1001 ~ YourMod_Card_1240
```
