using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class GetOutDeckStateTool : IMcpTool
    {
        public string Name => "get_outdeck_state";
        public string Description => "获取牌组(OutDeckUI)当前状态：装备中卡牌、备选卡牌、牌组上下限等。";
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

                var outDeck = UIManager.Instance?.GetUI<OutDeckUI>("OutDeckUI");
                if (outDeck == null || !outDeck.gameObject.activeInHierarchy)
                {
                    result["isOpen"] = false;
                    result["message"] = "牌组界面未打开";
                    return (JToken)result;
                }

                result["isOpen"] = true;

                var data = outDeck.OutDeckUIData;
                result["cardBottomCount"] = data.CardBottomCount;
                result["cardTopCount"] = data.CardTopCount;
                result["maxAlCardCount"] = data.MaxAlCardCount;

                var equippedCards = new JArray();
                foreach (var dc in data.cardList)
                {
                    if (dc?.data == null) continue;
                    var entry = new JObject();
                    TryAddData(entry, dc.data, "Id", "cardId");
                    TryAddData(entry, dc.data, "Name", "name");
                    TryAddData(entry, dc.data, "Expend", "cost");
                    TryAddData(entry, dc.data, "Rarity", "rarity");
                    TryAddData(entry, dc.data, "Type", "type");
                    TryAddData(entry, dc.data, "Tag", "tag");
                    entry["instanceId"] = dc.InstanceID ?? "";
                    equippedCards.Add(entry);
                }
                result["equippedCards"] = equippedCards;
                result["equippedCount"] = equippedCards.Count;

                var reserveCards = new JArray();
                foreach (var dc in data.UnCardList)
                {
                    if (dc?.data == null) continue;
                    var entry = new JObject();
                    TryAddData(entry, dc.data, "Id", "cardId");
                    TryAddData(entry, dc.data, "Name", "name");
                    TryAddData(entry, dc.data, "Expend", "cost");
                    TryAddData(entry, dc.data, "Rarity", "rarity");
                    TryAddData(entry, dc.data, "Type", "type");
                    TryAddData(entry, dc.data, "Tag", "tag");
                    entry["instanceId"] = dc.InstanceID ?? "";
                    reserveCards.Add(entry);
                }
                result["reserveCards"] = reserveCards;
                result["reserveCount"] = reserveCards.Count;

                return (JToken)result;
            });
        }

        private static void TryAddData(JObject target, IDictionary<string, string> source, string key, string targetKey)
        {
            if (source != null && source.TryGetValue(key, out var val))
                target[targetKey] = val;
        }
    }

    public class OutDeckMoveCardTool : IMcpTool
    {
        public string Name => "outdeck_move_card";
        public string Description => "移动一张卡牌（装备↔备选）。自动根据 instanceId 检测当前在哪侧并移到另一侧。校验牌组上下限，超限时提示错误。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["instanceId"] = new JObject { ["type"] = "string", ["description"] = "卡牌的运行时实例 ID（从 get_outdeck_state 获取）" }
            },
            ["required"] = new JArray { "instanceId" }
        };

        public async Task<JToken> Execute(JToken args)
        {
            string instanceId = args?["instanceId"]?.Value<string>();
            if (string.IsNullOrEmpty(instanceId))
                throw new ArgumentException("instanceId 不能为空");

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                var outDeck = UIManager.Instance?.GetUI<OutDeckUI>("OutDeckUI");
                if (outDeck == null || !outDeck.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "牌组界面未打开";
                    return (JToken)result;
                }

                var rt = RoleTable.Instance;
                if (rt == null)
                {
                    result["result"] = "error";
                    result["message"] = "RoleTable 不可用";
                    return (JToken)result;
                }

                // === 找到 ShowCard 组件，直接调用 MoveItem() ===
                // 这样会执行和右键菜单→"移动"完全一致的路径，包括：
                // 约束检查、音效、数据操作、CreateItem+Release、Null 激活(延迟一帧)、ChangeCardShow
                var allCards = outDeck.GetComponentsInChildren<ShowCard>(false);
                var targetCard = allCards.FirstOrDefault(c =>
                    c != null && c.dataConfig != null && c.dataConfig.InstanceID == instanceId);

                if (targetCard == null)
                {
                    result["result"] = "error";
                    result["message"] = $"未找到 InstanceID={instanceId} 对应的卡牌 UI 组件";
                    return (JToken)result;
                }

                string beforeSide = "unknown";
                if (rt.cardList.Any(d => d?.InstanceID == instanceId))
                    beforeSide = "equipped";
                else if (rt.UnCardList.Any(d => d?.InstanceID == instanceId))
                    beforeSide = "unequipped";

                // 调游戏源生的 MoveItem()
                targetCard.MoveItem();

                // 检测移动结果
                string afterSide = "unknown";
                if (rt.cardList.Any(d => d?.InstanceID == instanceId))
                    afterSide = "equipped";
                else if (rt.UnCardList.Any(d => d?.InstanceID == instanceId))
                    afterSide = "unequipped";

                if (beforeSide != afterSide && afterSide != "unknown")
                {
                    result["result"] = "success";
                    result["action"] = afterSide == "equipped" ? "equipped" : "unequipped";
                    result["message"] = $"已将卡牌从{(beforeSide == "equipped" ? "牌组" : "备选")}移到{(afterSide == "equipped" ? "牌组" : "备选")}";
                }
                else
                {
                    result["result"] = "error";
                    result["message"] = "移动失败（约束条件不满足，游戏已弹出提示）";
                }

                return (JToken)result;
            });
        }
    }

    public class OutDeckDecomposeTool : IMcpTool
    {
        public string Name => "outdeck_decompose";
        public string Description => "分解一张卡牌（消耗金钱并从牌组/备选中移除）。调用 ShowCard.DecomposeItem()，和右键菜单→[销毁]完全一致的执行路径。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["instanceId"] = new JObject { ["type"] = "string", ["description"] = "卡牌的运行时实例 ID（从 get_outdeck_state 获取）" }
            },
            ["required"] = new JArray { "instanceId" }
        };

        public async Task<JToken> Execute(JToken args)
        {
            string instanceId = args?["instanceId"]?.Value<string>();
            if (string.IsNullOrEmpty(instanceId))
                throw new ArgumentException("instanceId 不能为空");

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                var outDeck = UIManager.Instance?.GetUI<OutDeckUI>("OutDeckUI");
                if (outDeck == null || !outDeck.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "牌组界面未打开";
                    return (JToken)result;
                }

                var rt = RoleTable.Instance;
                if (rt == null)
                {
                    result["result"] = "error";
                    result["message"] = "RoleTable 不可用";
                    return (JToken)result;
                }

                // === 找到 ShowCard 组件，直接调用 DecomposeItem() ===
                // 和右键菜单→"销毁"完全一致的路径，包括：
                // Eternal 检查、金钱检查、下限检查、Wisdom(诅咒)检查、
                // 扣钱、ItemCheck(Remove+GameSaveAnalyser+Release+ChangeCardShow)
                var allCards = outDeck.GetComponentsInChildren<ShowCard>(false);
                var targetCard = allCards.FirstOrDefault(c =>
                    c != null && c.dataConfig != null && c.dataConfig.InstanceID == instanceId);

                if (targetCard == null)
                {
                    result["result"] = "error";
                    result["message"] = $"未找到 InstanceID={instanceId} 对应的卡牌 UI 组件";
                    return (JToken)result;
                }

                // 记录分解前状态
                bool wasInEquipped = rt.cardList.Any(d => d?.InstanceID == instanceId);
                int moneyBefore = (int)rt.Money;

                // 调游戏源生的 DecomposeItem()
                targetCard.DecomposeItem();

                // 检测分解结果
                bool stillExists = rt.cardList.Any(d => d?.InstanceID == instanceId)
                                || rt.UnCardList.Any(d => d?.InstanceID == instanceId);

                if (!stillExists)
                {
                    int cost = moneyBefore - (int)rt.Money;
                    result["result"] = "success";
                    result["action"] = "decomposed";
                    result["cost"] = cost;
                    result["message"] = $"已分解卡牌，消耗 {cost} 金钱";
                }
                else
                {
                    result["result"] = "error";
                    result["message"] = "分解失败（约束条件不满足，游戏已弹出提示）";
                }

                return (JToken)result;
            });
        }
    }
}
