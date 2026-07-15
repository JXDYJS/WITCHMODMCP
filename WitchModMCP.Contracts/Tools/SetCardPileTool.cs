using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Witch;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class SetCardPileTool : IMcpTool
    {
        public string Name => "set_card_pile";
        public string Description => "控制战斗中手牌/抽牌堆/弃牌堆/消耗堆。pile: hand/draw/discard/exhaust。action: add/remove/clear/set。支持批量操作。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["pile"] = new JObject { ["type"] = "string", ["description"] = "hand / draw / discard / exhaust" },
                ["action"] = new JObject { ["type"] = "string", ["description"] = "add / remove / clear / set" },
                ["cards"] = new JObject
                {
                    ["type"] = "array",
                    ["description"] = "cardId 列表，用于 add / set",
                    ["items"] = new JObject { ["type"] = "string" }
                },
                ["indices"] = new JObject
                {
                    ["type"] = "array",
                    ["description"] = "手牌中的索引列表，用于从 hand 移除",
                    ["items"] = new JObject { ["type"] = "integer" }
                },
                ["shuffle"] = new JObject { ["type"] = "boolean", ["description"] = "add 到 draw 后是否洗牌（默认 false）" }
            },
            ["required"] = new JArray { "pile", "action" }
        };

        public async Task<JToken> Execute(JToken args)
        {
            string pile = args?["pile"]?.Value<string>();
            string action = args?["action"]?.Value<string>();
            if (string.IsNullOrEmpty(pile)) throw new ArgumentException("pile 不能为空");
            if (string.IsNullOrEmpty(action)) throw new ArgumentException("action 不能为空");

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();
                var changes = new JArray();

                bool inFight = FightManager.Instance != null
                    && FightManager.Instance.fightType != FightType.None;
                if (!inFight)
                {
                    result["result"] = "error";
                    result["message"] = "当前不在战斗中";
                    return (JToken)result;
                }

                var fm = FightCardManager.Instance;
                if (fm == null)
                {
                    result["result"] = "error";
                    result["message"] = "FightCardManager 不可用";
                    return (JToken)result;
                }

                List<string> cards = (args["cards"] as JArray)?
                    .Select(c => c.Value<string>())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToList() ?? new List<string>();

                List<int> indices = (args["indices"] as JArray)?
                    .Select(c => c.Value<int>())
                    .ToList() ?? new List<int>();

                bool shuffle = args["shuffle"] is JToken sh && sh.Type == JTokenType.Boolean && sh.Value<bool>();

                switch (pile.ToLower())
                {
                    // ========== HAND ==========
                    case "hand":
                        switch (action.ToLower())
                        {
                            case "clear":
                            {
                                int count = FightUI.cardItemList?.Count ?? 0;
                                for (int i = FightUI.cardItemList.Count - 1; i >= 0; i--)
                                {
                                    var ci = FightUI.cardItemList[i];
                                    if (ci != null && ci.gameObject != null)
                                        GameObject.Destroy(ci.gameObject);
                                }
                                FightUI.cardItemList.Clear();
                                changes.Add($"hand clear: removed {count} cards");
                                break;
                            }
                            case "remove":
                            {
                                if (indices.Count > 0)
                                {
                                    var sorted = indices.OrderByDescending(i => i).Distinct().ToList();
                                    foreach (var idx in sorted)
                                    {
                                        if (idx >= 0 && idx < FightUI.cardItemList.Count)
                                        {
                                            var ci = FightUI.cardItemList[idx];
                                            if (ci?.dataConfig != null)
                                                fm.usedCardList.Add(ci.dataConfig);
                                            if (ci != null && ci.gameObject != null)
                                                GameObject.Destroy(ci.gameObject);
                                            FightUI.cardItemList.RemoveAt(idx);
                                            changes.Add($"hand remove index {idx}");
                                        }
                                    }
                                }
                                else if (cards.Count > 0)
                                {
                                    foreach (var cid in cards)
                                    {
                                        var match = FightUI.cardItemList
                                            .FirstOrDefault(ci => ci?.dataConfig?.data != null
                                                && ci.dataConfig.data.TryGetValue("Id", out var id) && id == cid);
                                        if (match != null)
                                        {
                                            if (match.dataConfig != null)
                                                fm.usedCardList.Add(match.dataConfig);
                                            if (match.gameObject != null)
                                                GameObject.Destroy(match.gameObject);
                                            FightUI.cardItemList.Remove(match);
                                            changes.Add($"hand remove cardId {cid}");
                                        }
                                    }
                                }
                                break;
                            }
                            case "add":
                            {
                                var fightUI = UIManager.Instance?.GetUI<FightUI>("FightUI");
                                // 先把卡加到抽牌堆
                                foreach (var cid in cards)
                                    fm.cardList.Add(new DataConfig(cid, DataType.Card));
                                // 再用游戏自身的抽牌机制抽到手上
                                if (fightUI != null)
                                    DrawIntoHand(fightUI, cards.Count);
                                changes.Add($"hand add: draw {cards.Count} cards into hand");
                                break;
                            }
                            case "set":
                            {
                                var fightUI = UIManager.Instance?.GetUI<FightUI>("FightUI");
                                // 清空手牌
                                for (int i = FightUI.cardItemList.Count - 1; i >= 0; i--)
                                {
                                    var ci = FightUI.cardItemList[i];
                                    if (ci != null && ci.gameObject != null)
                                        GameObject.Destroy(ci.gameObject);
                                }
                                FightUI.cardItemList.Clear();
                                changes.Add("hand cleared for set");

                                // 把目标卡加到抽牌堆顶部
                                for (int i = cards.Count - 1; i >= 0; i--)
                                    fm.cardList.Add(new DataConfig(cards[i], DataType.Card));
                                // 抽到手上
                                if (fightUI != null)
                                    DrawIntoHand(fightUI, cards.Count);
                                changes.Add($"hand set: {cards.Count} cards");
                                break;
                            }
                        }
                        break;

                    // ========== DRAW PILE ==========
                    case "draw":
                        switch (action.ToLower())
                        {
                            case "clear":
                            {
                                int n = fm.cardList.Count;
                                fm.cardList.Clear();
                                changes.Add($"draw clear: removed {n} cards");
                                break;
                            }
                            case "remove":
                            {
                                if (cards.Count > 0)
                                {
                                    foreach (var cid in cards)
                                    {
                                        var match = fm.cardList
                                            .FirstOrDefault(c => c?.data != null
                                                && c.data.TryGetValue("Id", out var id) && id == cid);
                                        if (match != null)
                                        {
                                            fm.cardList.Remove(match);
                                            changes.Add($"draw remove {cid}");
                                        }
                                    }
                                }
                                break;
                            }
                            case "add":
                            case "set":
                            {
                                if (action.ToLower() == "set")
                                {
                                    fm.cardList.Clear();
                                    changes.Add("draw cleared for set");
                                }
                                foreach (var cid in cards)
                                {
                                    fm.cardList.Add(new DataConfig(cid, DataType.Card));
                                }
                                if (shuffle && fm.cardList.Count > 0)
                                    DoShuffle(fm);
                                changes.Add($"draw {action}: added {cards.Count} cards{(shuffle ? " (shuffled)" : "")}");
                                break;
                            }
                        }
                        break;

                    // ========== DISCARD PILE ==========
                    case "discard":
                        switch (action.ToLower())
                        {
                            case "clear":
                            {
                                int n = fm.usedCardList.Count;
                                fm.usedCardList.Clear();
                                changes.Add($"discard clear: removed {n} cards");
                                break;
                            }
                            case "remove":
                            {
                                if (cards.Count > 0)
                                {
                                    foreach (var cid in cards)
                                    {
                                        var match = fm.usedCardList
                                            .FirstOrDefault(c => c?.data != null
                                                && c.data.TryGetValue("Id", out var id) && id == cid);
                                        if (match != null)
                                        {
                                            fm.usedCardList.Remove(match);
                                            changes.Add($"discard remove {cid}");
                                        }
                                    }
                                }
                                break;
                            }
                            case "add":
                            case "set":
                            {
                                if (action.ToLower() == "set")
                                {
                                    fm.usedCardList.Clear();
                                    changes.Add("discard cleared for set");
                                }
                                foreach (var cid in cards)
                                {
                                    fm.usedCardList.Add(new DataConfig(cid, DataType.Card));
                                }
                                changes.Add($"discard {action}: added {cards.Count} cards");
                                break;
                            }
                        }
                        break;

                    // ========== EXHAUST PILE ==========
                    case "exhaust":
                        switch (action.ToLower())
                        {
                            case "clear":
                            {
                                // Re-add all exhausted cards back to master deck
                                int n = 0;
                                for (int i = fm.FightcardList.Count - 1; i >= 0; i--)
                                {
                                    var c = fm.FightcardList[i];
                                    if (c?.data == null) continue;
                                    bool inDraw = fm.cardList.Any(x => x.InstanceID == c.InstanceID);
                                    bool inDiscard = fm.usedCardList.Any(x => x.InstanceID == c.InstanceID);
                                    bool inHand = FightUI.cardItemList?.Any(
                                        x => x?.dataConfig?.InstanceID == c.InstanceID) ?? false;
                                    if (!inDraw && !inDiscard && !inHand)
                                    {
                                        fm.cardList.Add(c);
                                        n++;
                                    }
                                }
                                changes.Add($"exhaust clear: restored {n} cards to draw pile");
                                break;
                            }
                            case "add":
                            {
                                // Exhaust specific cards - remove them from all piles and master deck
                                foreach (var cid in cards)
                                {
                                    // Remove from hand
                                    if (FightUI.cardItemList != null)
                                    {
                                        var inHand = FightUI.cardItemList
                                            .FirstOrDefault(ci => ci?.dataConfig?.data != null
                                                && ci.dataConfig.data.TryGetValue("Id", out var id) && id == cid);
                                        if (inHand != null)
                                        {
                                            if (inHand.gameObject != null)
                                                GameObject.Destroy(inHand.gameObject);
                                            FightUI.cardItemList.Remove(inHand);
                                        }
                                    }
                                    // Remove from draw
                                    var inDraw = fm.cardList
                                        .FirstOrDefault(c => c?.data != null
                                            && c.data.TryGetValue("Id", out var id) && id == cid);
                                    if (inDraw != null) fm.cardList.Remove(inDraw);
                                    // Remove from discard
                                    var inDiscard = fm.usedCardList
                                        .FirstOrDefault(c => c?.data != null
                                            && c.data.TryGetValue("Id", out var id) && id == cid);
                                    if (inDiscard != null) fm.usedCardList.Remove(inDiscard);
                                    // Remove from master deck
                                    var inMaster = fm.FightcardList
                                        .FirstOrDefault(c => c?.data != null
                                            && c.data.TryGetValue("Id", out var id) && id == cid);
                                    if (inMaster != null) fm.FightcardList.Remove(inMaster);
                                    changes.Add($"exhaust add {cid}");
                                }
                                break;
                            }
                            case "remove":
                            {
                                // Un-exhaust: add cards back to master + draw
                                foreach (var cid in cards)
                                {
                                    var dc = new DataConfig(cid, DataType.Card);
                                    if (!fm.FightcardList.Any(c => c?.data != null
                                        && c.data.TryGetValue("Id", out var id) && id == cid))
                                    {
                                        fm.FightcardList.Add(dc);
                                    }
                                    if (!fm.cardList.Any(c => c?.data != null
                                        && c.data.TryGetValue("Id", out var id) && id == cid))
                                    {
                                        fm.cardList.Add(new DataConfig(cid, DataType.Card));
                                    }
                                    changes.Add($"exhaust remove (restore) {cid}");
                                }
                                break;
                            }
                        }
                        break;

                    default:
                        result["result"] = "error";
                        result["message"] = $"未知 pile: {pile}";
                        return (JToken)result;
                }

                result["result"] = "success";
                result["changes"] = changes;
                return (JToken)result;
            });
        }

        private static void DrawIntoHand(FightUI fightUI, int count)
        {
            fightUI.CreateCardItem(count);
        }

        private static int GetHandLimit()
        {
            try
            {
                var fi = typeof(FightUI).GetField("CardTopCount",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Public);
                if (fi != null)
                {
                    var fightUI = UIManager.Instance?.GetUI<FightUI>("FightUI");
                    if (fightUI != null) return (int)fi.GetValue(fightUI);
                }
            }
            catch { }
            return 10;
        }

        private static void DoShuffle(FightCardManager fm)
        {
            try
            {
                var list = fm.cardList.ToList();
                var rng = new System.Random();
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(0, i + 1);
                    var tmp = list[i];
                    list[i] = list[j];
                    list[j] = tmp;
                }
                fm.cardList.Clear();
                foreach (var c in list) fm.cardList.Add(c);
            }
            catch { }
        }
    }
}
