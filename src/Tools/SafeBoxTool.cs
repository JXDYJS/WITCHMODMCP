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
    public class GetSafeBoxStateTool : IMcpTool
    {
        public string Name => "get_safebox_state";
        public string Description => "获取保险箱(SafeBox)界面当前状态：保险箱内物品、背包物品、金钱、存入/取出次数等。";
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
                var safeBox = UIManager.Instance?.GetUI<SafeBoxUI>("SafeBoxUI");
                if (safeBox == null || !safeBox.gameObject.activeInHierarchy)
                {
                    result["isOpen"] = false;
                    result["message"] = "保险箱界面未打开";
                    return (JToken)result;
                }

                result["isOpen"] = true;

                var allItems = safeBox.GetComponentsInChildren<SafeBoxItem>(false)
                    .Where(i => i != null && i.dataConfig?.data != null && i.gameObject.activeInHierarchy)
                    .ToList();

                var vaultCards = new JArray();
                var vaultRelics = new JArray();
                var backpackCards = new JArray();
                var backpackRelics = new JArray();

                foreach (var item in allItems)
                {
                    var entry = new JObject();
                    TryAddData(entry, item.dataConfig.data, "Id", "id");
                    TryAddData(entry, item.dataConfig.data, "Name", "name");
                    TryAddData(entry, item.dataConfig.data, "Rarity", "rarity");
                    entry["ifEquipped"] = item.ifEquipped;
                    entry["hasInBack"] = item.hasInBack;

                    if (!item.InBackPack)
                    {
                        if (item.ItemType == "Card")
                            vaultCards.Add(entry);
                        else if (item.ItemType == "Relic")
                            vaultRelics.Add(entry);
                    }
                    else
                    {
                        if (item.ItemType == "Card")
                            backpackCards.Add(entry);
                        else if (item.ItemType == "Relic")
                            backpackRelics.Add(entry);
                    }
                }

                result["vaultCards"] = vaultCards;
                result["vaultRelics"] = vaultRelics;
                result["backpackCards"] = backpackCards;
                result["backpackRelics"] = backpackRelics;

                try
                {
                    var rt = Singleton<GameRuntimeData>.Instance;
                    result["vaultMoney"] = rt?.Money ?? 0;
                }
                catch { result["vaultMoney"] = 0; }

                try { result["currentMoney"] = (int)(RoleTable.Instance?.Money ?? 0); }
                catch { result["currentMoney"] = 0; }

                try
                {
                    var rt = RoleTable.Instance;
                    result["saveMoneyCountRemaining"] = rt?.SafeBoxSaveMoneyCount ?? 0;
                    result["getMoneyCountRemaining"] = rt?.SafeBoxGetMoneyCount ?? 0;
                    result["cardCountInVault"] = rt?.SafeBoxCardCount ?? 0;
                    result["relicCountInVault"] = rt?.SafeBoxRelicCount ?? 0;
                    result["cardBottomCount"] = rt?.CardBottomCount ?? 0;
                    result["cardTopCount"] = rt?.CardTopCount ?? 0;
                    result["cardListCount"] = rt?.cardList?.Count ?? 0;
                    result["unCardListCount"] = rt?.UnCardList?.Count ?? 0;
                    result["relicListCount"] = rt?.relicList?.Count ?? 0;
                }
                catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetSafeboxStateTool] roleTable: {ex.Message}"); }

                return (JToken)result;
            });
        }

        private static void TryAddData(JObject target, IDictionary<string, string> source, string key, string targetKey)
        {
            if (source != null && source.TryGetValue(key, out var val))
                target[targetKey] = val;
        }
    }

    public class SafeBoxDepositTool : IMcpTool
    {
        public string Name => "safebox_deposit";
        public string Description => "将背包中的一张卡牌或遗物存入保险箱。type: card/relic, index: 背包中物品的索引(0-based)。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["type"] = new JObject { ["type"] = "string", ["description"] = "物品类型: card 或 relic" },
                ["index"] = new JObject { ["type"] = "integer", ["description"] = "背包中物品的索引(0-based)" }
            },
            ["required"] = new JArray { "type", "index" }
        };

        public async Task<JToken> Execute(JToken args)
        {
            var rawType = args?["type"]?.Value<string>();
            int? index = args?["index"]?.Value<int>();

            if (string.IsNullOrEmpty(rawType))
                throw new ArgumentException("type 不能为空");
            var typeLower = rawType.ToLower();
            if (typeLower != "card" && typeLower != "relic")
                throw new ArgumentException("type 必须为 'card' 或 'relic'");
            if (!index.HasValue || index.Value < 0)
                throw new ArgumentException("index 必须 >= 0");

            string itemType = typeLower == "card" ? "Card" : "Relic";

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();
                var safeBox = UIManager.Instance?.GetUI<SafeBoxUI>("SafeBoxUI");
                if (safeBox == null || !safeBox.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "保险箱界面未打开";
                    return (JToken)result;
                }

                var items = safeBox.GetComponentsInChildren<SafeBoxItem>(false)
                    .Where(i => i != null && i.dataConfig?.data != null
                        && i.gameObject.activeInHierarchy
                        && i.InBackPack && i.ItemType == itemType)
                    .ToList();

                if (index.Value >= items.Count)
                {
                    result["result"] = "error";
                    result["message"] = $"背包中未找到第 {index.Value} 个 {(typeLower == "card" ? "卡牌" : "遗物")}，共有 {items.Count} 个";
                    result["totalAvailable"] = items.Count;
                    return (JToken)result;
                }

                var item = items[index.Value];
                try
                {
                    safeBox.PutIntoStore(item.gameObject);
                    result["result"] = "success";
                    TryAddResultItem(result, item);
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"存入失败: {ex.Message}";
                }

                return (JToken)result;
            });
        }

        private static void TryAddResultItem(JObject target, SafeBoxItem item)
        {
            if (item.dataConfig?.data == null) return;
            if (item.dataConfig.data.TryGetValue("Id", out var id))
                target["itemId"] = id;
            if (item.dataConfig.data.TryGetValue("Name", out var name))
                target["itemName"] = name;
        }
    }

    public class SafeBoxWithdrawTool : IMcpTool
    {
        public string Name => "safebox_withdraw";
        public string Description => "从保险箱中取出一张卡牌或遗物。type: card/relic, index: 保险箱中物品的索引(0-based)。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["type"] = new JObject { ["type"] = "string", ["description"] = "物品类型: card 或 relic" },
                ["index"] = new JObject { ["type"] = "integer", ["description"] = "保险箱中物品的索引(0-based)" }
            },
            ["required"] = new JArray { "type", "index" }
        };

        public async Task<JToken> Execute(JToken args)
        {
            var rawType = args?["type"]?.Value<string>();
            int? index = args?["index"]?.Value<int>();

            if (string.IsNullOrEmpty(rawType))
                throw new ArgumentException("type 不能为空");
            var typeLower = rawType.ToLower();
            if (typeLower != "card" && typeLower != "relic")
                throw new ArgumentException("type 必须为 'card' 或 'relic'");
            if (!index.HasValue || index.Value < 0)
                throw new ArgumentException("index 必须 >= 0");

            string itemType = typeLower == "card" ? "Card" : "Relic";

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();
                var safeBox = UIManager.Instance?.GetUI<SafeBoxUI>("SafeBoxUI");
                if (safeBox == null || !safeBox.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "保险箱界面未打开";
                    return (JToken)result;
                }

                var items = safeBox.GetComponentsInChildren<SafeBoxItem>(false)
                    .Where(i => i != null && i.dataConfig?.data != null
                        && i.gameObject.activeInHierarchy
                        && !i.InBackPack && i.ItemType == itemType)
                    .ToList();

                if (index.Value >= items.Count)
                {
                    result["result"] = "error";
                    result["message"] = $"保险箱中未找到第 {index.Value} 个 {(typeLower == "card" ? "卡牌" : "遗物")}，共有 {items.Count} 个";
                    result["totalAvailable"] = items.Count;
                    return (JToken)result;
                }

                var item = items[index.Value];
                try
                {
                    safeBox.PutItBack(item.gameObject);
                    result["result"] = "success";
                    var entry = new JObject();
                    if (item.dataConfig.data.TryGetValue("Id", out var id))
                        entry["itemId"] = id;
                    if (item.dataConfig.data.TryGetValue("Name", out var name))
                        entry["itemName"] = name;
                    result["item"] = entry;
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"取出失败: {ex.Message}";
                }

                return (JToken)result;
            });
        }
    }

    public class SafeBoxDepositMoneyTool : IMcpTool
    {
        public string Name => "safebox_deposit_money";
        public string Description => "向保险箱内存入金钱（每次最多100金，消耗一次存入次数）。";
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
                var safeBox = UIManager.Instance?.GetUI<SafeBoxUI>("SafeBoxUI");
                if (safeBox == null || !safeBox.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "保险箱界面未打开";
                    return (JToken)result;
                }

                try
                {
                    safeBox.RetainMoney();
                    result["result"] = "success";
                    result["message"] = "已执行存入金钱操作";
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"存入金钱失败: {ex.Message}";
                }

                return (JToken)result;
            });
        }
    }

    public class SafeBoxWithdrawMoneyTool : IMcpTool
    {
        public string Name => "safebox_withdraw_money";
        public string Description => "从保险箱中取出金钱（每次最多取200金，消耗一次取出次数）。";
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
                var safeBox = UIManager.Instance?.GetUI<SafeBoxUI>("SafeBoxUI");
                if (safeBox == null || !safeBox.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "保险箱界面未打开";
                    return (JToken)result;
                }

                try
                {
                    safeBox.ChangeMoney();
                    result["result"] = "success";
                    result["message"] = "已执行取出金钱操作";
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"取出金钱失败: {ex.Message}";
                }

                return (JToken)result;
            });
        }
    }

    public class SafeBoxOpenTool : IMcpTool
    {
        public string Name => "safebox_open";
        public string Description => "打开保险箱界面。调用 UIManager.ShowUI 打开 SafeBoxUI 窗口。";
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
                var existing = UIManager.Instance?.GetUI<SafeBoxUI>("SafeBoxUI");
                if (existing != null && existing.gameObject.activeInHierarchy)
                {
                    result["result"] = "success";
                    result["message"] = "保险箱界面已经打开";
                    return (JToken)result;
                }

                try
                {
                    UIManager.Instance.ShowUI<SafeBoxUI>("SafeBoxUI");
                    result["result"] = "success";
                    result["message"] = "保险箱界面已打开";
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"打开保险箱失败: {ex.Message}";
                }

                return (JToken)result;
            });
        }
    }

    public class SafeBoxCloseTool : IMcpTool
    {
        public string Name => "safebox_close";
        public string Description => "保存并关闭保险箱界面。自动调用 SafeboxSave 持久化变更。";
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
                var safeBox = UIManager.Instance?.GetUI<SafeBoxUI>("SafeBoxUI");
                if (safeBox == null || !safeBox.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "保险箱界面未打开";
                    return (JToken)result;
                }

                try
                {
                    SafeBoxUI.SafeboxSave();
                    safeBox.Close();
                    result["result"] = "success";
                    result["message"] = "保险箱已保存并关闭";
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"关闭保险箱失败: {ex.Message}";
                }

                return (JToken)result;
            });
        }
    }
}
