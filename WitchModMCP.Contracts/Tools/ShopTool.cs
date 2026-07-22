using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Loxodon.Framework.Obfuscation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class GetShopStateTool : IMcpTool
    {
        public string Name => "get_shop_state";
        public string Description => "获取商店当前状态：在售物品列表、玩家卡牌、金钱、刷新次数等。每个物品/卡牌带 instanceId（运行时唯一标识），后续 shop_buy / shop_sell 通过 instanceId 操作。";
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
                var shop = UIManager.Instance?.GetUI<ShopUI>("ShopUI");
                if (shop == null || !shop.gameObject.activeInHierarchy)
                {
                    result["isOpen"] = false;
                    result["message"] = "商店界面未打开";
                    return (JToken)result;
                }

                result["isOpen"] = true;

                try { result["money"] = (int)(RoleTable.Instance?.Money ?? 0); }
                catch { result["money"] = 0; }

                try
                {
                    result["flushedCount"] = shop.flushedCount;
                    result["maxFlushedCount"] = shop.maxFlushedCount;
                }
                catch { }

                var itemsForSale = new JArray();
                var shopItems = shop.GetComponentsInChildren<ShopItem>(false)
                    .Where(i => i != null && i.dataConfig?.data != null && i.gameObject.activeInHierarchy)
                    .ToList();
                foreach (var item in shopItems)
                {
                    var entry = new JObject();
                    TryAddData(entry, item.dataConfig.data, "Id", "id");
                    TryAddData(entry, item.dataConfig.data, "Name", "name");
                    TryAddData(entry, item.dataConfig.data, "Rarity", "rarity");
                    entry["instanceId"] = item.dataConfig.InstanceID ?? "";
                    entry["itemType"] = item.ItemType ?? "";
                    entry["price"] = item.itemPrice;
                    itemsForSale.Add(entry);
                }
                result["itemsForSale"] = itemsForSale;

                var rt = RoleTable.Instance;
                if (rt != null)
                {
                    var playerCards = new JArray();
                    if (rt.cardList != null)
                    {
                        foreach (var card in rt.cardList)
                        {
                            var entry = new JObject();
                            TryAddData(entry, card.data, "Id", "id");
                            TryAddData(entry, card.data, "Name", "name");
                            TryAddData(entry, card.data, "Rarity", "rarity");
                            TryAddData(entry, card.data, "Expend", "expend");
                            entry["instanceId"] = card.InstanceID ?? "";
                            entry["equipped"] = true;
                            bool canSell = true;
                            if (rt.cardList.Count <= rt.CardBottomCount) canSell = false;
                            entry["canSell"] = canSell;
                            int rarity = 1;
                            int.TryParse(card.data.GetValueOrDefault("Rarity", "1"), out rarity);
                            entry["sellPrice"] = 20 * rarity;
                            playerCards.Add(entry);
                        }
                    }
                    if (rt.UnCardList != null)
                    {
                        foreach (var card in rt.UnCardList)
                        {
                            var entry = new JObject();
                            TryAddData(entry, card.data, "Id", "id");
                            TryAddData(entry, card.data, "Name", "name");
                            TryAddData(entry, card.data, "Rarity", "rarity");
                            TryAddData(entry, card.data, "Expend", "expend");
                            entry["instanceId"] = card.InstanceID ?? "";
                            entry["equipped"] = false;
                            entry["canSell"] = true;
                            int rarity = 1;
                            int.TryParse(card.data.GetValueOrDefault("Rarity", "1"), out rarity);
                            entry["sellPrice"] = 20 * rarity;
                            playerCards.Add(entry);
                        }
                    }
                    result["playerCards"] = playerCards;

                    try
                    {
                        result["cardListCount"] = rt.cardList?.Count ?? 0;
                        result["unCardListCount"] = rt.UnCardList?.Count ?? 0;
                        result["totalCards"] = (rt.cardList?.Count ?? 0) + (rt.UnCardList?.Count ?? 0);
                    }
                    catch { }

                    var playerRelics = new JArray();
                    if (rt.relicList != null)
                    {
                        foreach (var relic in rt.relicList)
                        {
                            var entry = new JObject();
                            TryAddData(entry, relic.data, "Id", "id");
                            TryAddData(entry, relic.data, "Name", "name");
                            TryAddData(entry, relic.data, "Rarity", "rarity");
                            entry["instanceId"] = relic.InstanceID ?? "";
                            playerRelics.Add(entry);
                        }
                    }
                    result["playerRelics"] = playerRelics;
                }

                return (JToken)result;
            });
        }

        private static void TryAddData(JObject target, IDictionary<string, string> source, string key, string targetKey)
        {
            if (source != null && source.TryGetValue(key, out var val))
                target[targetKey] = val;
        }
    }

    public class ShopBuyTool : IMcpTool
    {
        public string Name => "shop_buy";
        public string Description => "从商店购买指定物品。instanceId 取自 get_shop_state 返回的 itemsForSale 中的 instanceId（运行时唯一 GUID，不随购买后列表变化）。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["instanceId"] = new JObject { ["type"] = "string", ["description"] = "目标物品的运行时唯一实例 ID（get_shop_state 返回的 instanceId 字段）" }
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
                var shop = UIManager.Instance?.GetUI<ShopUI>("ShopUI");
                if (shop == null || !shop.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "商店界面未打开";
                    return (JToken)result;
                }

                var allShopItems = shop.GetComponentsInChildren<ShopItem>(false)
                    .Where(i => i != null && i.dataConfig?.data != null && i.gameObject.activeInHierarchy)
                    .ToList();

                var item = allShopItems.FirstOrDefault(i =>
                    string.Equals(i.dataConfig.InstanceID, instanceId, StringComparison.Ordinal));

                if (item == null)
                {
                    result["result"] = "error";
                    result["message"] = $"未找到 instanceId 为 '{instanceId}' 的商品（可能已被购买）";
                    return (JToken)result;
                }

                var name = item.dataConfig.data.TryGetValue("Name", out var n) ? n : "?";
                var price = item.itemPrice;

                try
                {
                    if (RoleTable.Instance != null && (int)RoleTable.Instance.Money < price)
                    {
                        result["result"] = "error";
                        result["message"] = $"金钱不足：需要 {price}，当前 {(int)RoleTable.Instance.Money}";
                        result["needMoney"] = price;
                        result["currentMoney"] = (int)RoleTable.Instance.Money;
                        return (JToken)result;
                    }

                    item.TryBuy();
                    result["result"] = "success";
                    result["itemName"] = name;
                    result["itemType"] = item.ItemType ?? "";
                    result["price"] = price;
                    result["message"] = $"已购买 {name}（{price}金）";
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"购买失败: {ex.Message}";
                }

                return (JToken)result;
            });
        }
    }

    public class ShopSellTool : IMcpTool
    {
        public string Name => "shop_sell";
        public string Description => "在商店卖出卡牌。instanceId 取自 get_shop_state 返回的 playerCards 中的 instanceId（运行时唯一 GUID，两张同名卡也有不同的 instanceId）。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["instanceId"] = new JObject { ["type"] = "string", ["description"] = "目标卡牌的运行时唯一实例 ID（get_shop_state 返回的 playerCards 中的 instanceId）" }
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
                var shop = UIManager.Instance?.GetUI<ShopUI>("ShopUI");
                if (shop == null || !shop.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "商店界面未打开";
                    return (JToken)result;
                }

                var rt = RoleTable.Instance;
                if (rt == null)
                {
                    result["result"] = "error";
                    result["message"] = "无法读取角色数据";
                    return (JToken)result;
                }

                DataConfig target = null;
                bool isEquipped = false;

                if (rt.cardList != null)
                {
                    target = rt.cardList.FirstOrDefault(c =>
                        string.Equals(c.InstanceID, instanceId, StringComparison.Ordinal));
                    if (target != null) isEquipped = true;
                }
                if (target == null && rt.UnCardList != null)
                {
                    target = rt.UnCardList.FirstOrDefault(c =>
                        string.Equals(c.InstanceID, instanceId, StringComparison.Ordinal));
                }

                if (target == null)
                {
                    result["result"] = "error";
                    result["message"] = $"未找到 instanceId 为 '{instanceId}' 的卡牌";
                    return (JToken)result;
                }

                var name = target.data.TryGetValue("Name", out var n) ? n : "?";

                string tag = null;
                target.data.TryGetValue("Tag", out tag);
                if (tag != null && tag.Contains("Eternal"))
                {
                    result["result"] = "error";
                    result["message"] = $"卡牌 {name} 带有 Eternal 标签，不可卖出";
                    return (JToken)result;
                }

                if (isEquipped && rt.cardList.Count <= rt.CardBottomCount)
                {
                    result["result"] = "error";
                    result["message"] = $"卡牌 {name} 是最后一张装备卡，不可卖出（下限 {rt.CardBottomCount}）";
                    result["cardBottomCount"] = rt.CardBottomCount;
                    return (JToken)result;
                }

                int rarity = 1;
                int.TryParse(target.data.GetValueOrDefault("Rarity", "1"), out rarity);
                int sellPrice = 20 * rarity;

                try
                {
                    rt.cardList.Remove(target);
                    rt.UnCardList.Remove(target);
                    rt.Money += (ObfuscatedInt)sellPrice;

                    result["result"] = "success";
                    result["cardName"] = name;
                    result["sellPrice"] = sellPrice;
                    result["currentMoney"] = (int)rt.Money;
                    result["message"] = $"已卖出 {name}，获得 {sellPrice} 金";
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"卖出失败: {ex.Message}";
                }

                return (JToken)result;
            });
        }
    }

    public class ShopRefreshTool : IMcpTool
    {
        public string Name => "shop_refresh";
        public string Description => "刷新商店商品列表（消耗金钱）。";
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
                var shop = UIManager.Instance?.GetUI<ShopUI>("ShopUI");
                if (shop == null || !shop.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "商店界面未打开";
                    return (JToken)result;
                }

                try
                {
                    shop.Flushed();
                    result["result"] = "success";
                    result["flushedCount"] = shop.flushedCount;
                    result["message"] = "已刷新商店";
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"刷新失败: {ex.Message}";
                }

                return (JToken)result;
            });
        }
    }
}
