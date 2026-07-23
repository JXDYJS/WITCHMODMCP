using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class GetFightStateTool : IMcpTool
    {
        public string Name => "get_fight_state";
        public string Description => "获取战斗中完整快照：玩家/敌人状态、手牌、抽牌堆(顶部)、弃牌堆、Buff列表、敌方意图等。需在战斗中。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
        };

        public async Task<JToken> Execute(JToken args)
        {
            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                bool inFight = FightManager.Instance != null
                    && FightManager.Instance.fightType != FightType.None;
                result["inFight"] = inFight;
                if (!inFight)
                {
                    result["message"] = "当前不在战斗中";
                    return (JToken)result;
                }

                result["phase"] = FightManager.Instance.fightType.ToString();
                result["isFake"] = FightManager.Instance.IsFake;
                result["turn"] = MapManager.Instance?.Level ?? 0;

                // --- Player ---
                if (FightPlayer.Instance != null)
                {
                    var p = new JObject();
                    try
                    {
                        try { p["instanceId"] = FightPlayer.Instance.gameObject.GetInstanceID(); } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] player instanceId: {ex.Message}"); }
                        var st = FightPlayer.Instance.Status;
                        p["hp"] = st.CurHp;
                        p["maxHp"] = st.MaxHp;
                        var sm = st as StatusManager;
                        p["shield"] = sm != null ? sm.defend : 0;
                        p["power"] = FightPlayer.Instance.CurPowerCount;
                        p["maxPower"] = FightPlayer.Instance.MaxPowerCount;
                        p["isDead"] = st.state == IStatusManager.State.Dead;

                        var buffs = new JArray();
                        var allBuffs = st.GetBuffs();
                        if (allBuffs != null)
                        {
                            foreach (var b in allBuffs)
                            {
                                var bj = new JObject();
                                try { bj["id"] = b.buffConfig.BuffId; } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] player buff id: {ex.Message}"); }
                                try { bj["level"] = b.buffConfig.Level; } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] player buff level: {ex.Message}"); }
                                try { bj["type"] = b.buffConfig.Type; } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] player buff type: {ex.Message}"); }
                                buffs.Add(bj);
                            }
                        }
                        p["buffs"] = buffs;
                    }
                    catch (Exception ex) { p["error"] = ex.Message; }
                    result["player"] = p;
                }

                // --- Enemies ---
                if (EnemyManager.Instance?.enemyList != null)
                {
                    var enemies = new JArray();
                    foreach (var e in EnemyManager.Instance.enemyList)
                    {
                        if (e == null) continue;
                        var ej = new JObject();
                        try
                        {
                            try { ej["instanceId"] = e.gameObject.GetInstanceID(); } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] enemy instanceId: {ex.Message}"); }
                            ej["index"] = EnemyManager.Instance.enemyList.IndexOf(e);
                            ej["id"] = e.dataConfig?.data?.GetValueOrDefault("Id", "");
                            ej["name"] = e.gameObject?.name ?? "";
                            var st = e.Status;
                            ej["hp"] = st.CurHp;
                            ej["maxHp"] = st.MaxHp;
                            var sm2 = st as StatusManager;
                            ej["shield"] = sm2 != null ? sm2.defend : 0;
                            ej["isDead"] = st.state == IStatusManager.State.Dead;
                            ej["attack"] = e.Attack;
                            ej["defend"] = e.Defend;

                            var buffs = new JArray();
                            var allBuffs = st.GetBuffs();
                            if (allBuffs != null)
                            {
                                foreach (var b in allBuffs)
                                {
                                    var bj = new JObject();
                                    try { bj["id"] = b.buffConfig.BuffId; } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] enemy buff id: {ex.Message}"); }
                                    try { bj["level"] = b.buffConfig.Level; } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] enemy buff level: {ex.Message}"); }
                                    try { bj["type"] = b.buffConfig.Type; } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] enemy buff type: {ex.Message}"); }
                                    buffs.Add(bj);
                                }
                            }
                            ej["buffs"] = buffs;

                            // Intent: read ActionCards from OtherObj
                            try
                            {
                                if (e.ActionCards != null)
                                {
                                    var intents = new JArray();
                                    foreach (var ac in e.ActionCards)
                                    {
                                        if (ac?.dataConfig?.data != null)
                                        {
                                            var intent = new JObject();
                                            foreach (var kv in ac.dataConfig.data)
                                                intent[kv.Key] = kv.Value;
                                            intents.Add(intent);
                                        }
                                    }
                                    ej["intents"] = intents;
                                }
                            }
                            catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] intents: {ex.Message}"); }
                        }
                        catch (Exception ex) { ej["error"] = ex.Message; }
                        enemies.Add(ej);
                    }
                    result["enemies"] = enemies;
                }

                // --- Hand ---
                if (FightUI.cardItemList != null)
                {
                    var hand = new JArray();
                    for (int i = 0; i < FightUI.cardItemList.Count; i++)
                    {
                        var c = FightUI.cardItemList[i];
                        if (c == null || c.dataConfig?.data == null) continue;
                        var cj = new JObject();
                        cj["index"] = i;
                        try { cj["cardId"] = c.dataConfig.data["Id"]; } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] hand cardId: {ex.Message}"); }
                        try { cj["instanceId"] = c.dataConfig.InstanceID; } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] hand instanceId: {ex.Message}"); }
                        try
                        {
                            if (c.dataConfig.data.TryGetValue("Expend", out var cost))
                                cj["cost"] = cost;
                        }
                        catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] hand cost: {ex.Message}"); }
                        hand.Add(cj);
                    }
                    result["hand"] = hand;
                }

                // --- Draw pile ---
                if (FightCardManager.Instance?.cardList != null)
                {
                    var draw = new JObject();
                    draw["count"] = FightCardManager.Instance.cardList.Count;
                    var top5 = new JArray();
                    for (int i = FightCardManager.Instance.cardList.Count - 1;
                        i >= 0 && top5.Count < 5; i--)
                    {
                        var c = FightCardManager.Instance.cardList[i];
                        if (c?.data != null)
                        {
                            var cj = new JObject();
                            try { cj["cardId"] = c.data["Id"]; } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] draw cardId: {ex.Message}"); }
                            try { cj["instanceId"] = c.InstanceID; } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] draw instanceId: {ex.Message}"); }
                            top5.Add(cj);
                        }
                    }
                    draw["top5"] = top5;
                    result["drawPile"] = draw;
                }

                // --- Discard pile ---
                if (FightCardManager.Instance?.usedCardList != null)
                {
                    var disc = new JObject();
                    var list = FightCardManager.Instance.usedCardList;
                    disc["count"] = list.Count;
                    var last5 = new JArray();
                    for (int i = Math.Max(0, list.Count - 5); i < list.Count; i++)
                    {
                        var c = list[i];
                        if (c?.data != null)
                        {
                            var cj = new JObject();
                            try { cj["cardId"] = c.data["Id"]; } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] discard cardId: {ex.Message}"); }
                            try { cj["instanceId"] = c.InstanceID; } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] discard instanceId: {ex.Message}"); }
                            last5.Add(cj);
                        }
                    }
                    disc["last5"] = last5;
                    result["discardPile"] = disc;
                }

                // --- Exhaust tracking (cards removed from FightcardList) ---
                try
                {
                    var masterList = FightCardManager.Instance?.FightcardList;
                    if (masterList != null)
                    {
                        var allLive = new HashSet<string>();
                        foreach (var c in FightCardManager.Instance.cardList)
                            if (c?.data != null) allLive.Add(c.InstanceID);
                        foreach (var c in FightCardManager.Instance.usedCardList)
                            if (c?.data != null) allLive.Add(c.InstanceID);
                        if (FightUI.cardItemList != null)
                        {
                            foreach (var c in FightUI.cardItemList)
                                if (c?.dataConfig?.data != null) allLive.Add(c.dataConfig.InstanceID);
                        }
                        var exhausted = new JArray();
                        foreach (var c in masterList)
                        {
                            if (c?.data != null && !allLive.Contains(c.InstanceID))
                            {
                                var cj = new JObject();
                                try { cj["cardId"] = c.data["Id"]; } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] exhaust cardId: {ex.Message}"); }
                                try { cj["instanceId"] = c.InstanceID; } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] exhaust instanceId: {ex.Message}"); }
                                exhausted.Add(cj);
                            }
                        }
                        result["exhaustPile"] = new JObject
                        {
                            ["count"] = exhausted.Count,
                            ["cards"] = exhausted
                        };
                    }
                }
                catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] exhaust: {ex.Message}"); }

                // --- Master deck count ---
                try
                {
                    if (FightCardManager.Instance?.FightcardList != null)
                        result["masterDeckCount"] = FightCardManager.Instance.FightcardList.Count;
                }
                catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetFightStateTool] masterDeckCount: {ex.Message}"); }

                // --- Selection mode ---
                result["inSelectionMode"] = FightUI.InIEn;
                result["selectedCardCount"] = FightUI.SelectedCard?.Count ?? 0;

                return (JToken)result;
            });
        }
    }
}
