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
    public class GetRewardsStateTool : IMcpTool
    {
        public string Name => "get_rewards_state";
        public string Description => "获取战斗奖励(BattleRewardsUI)当前状态：可领取的奖励列表（金钱/卡牌/祝福/遗物），每种奖励的类型和描述。每个奖励附 hierarchy 路径，可以和 scan_ui 的返回对应来找全局 index 后用 click_ui 点击领取。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
        };

        private static readonly BindingFlags _nonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        public async Task<JToken> Execute(JToken args)
        {
            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                var brUI = UIManager.Instance?.GetUI<BattleRewardsUI>("BattleRewardsUI");
                if (brUI == null || !brUI.gameObject.activeInHierarchy)
                {
                    result["isOpen"] = false;
                    result["message"] = "战斗奖励界面未打开";
                    return (JToken)result;
                }

                result["isOpen"] = true;

                // 读取公开字段
                int cardCount = 0;
                try { cardCount = brUI.CardCount; } catch { }
                result["cardRewardCount"] = cardCount;

                // 读取遗物奖励列表
                var relicList = new JArray();
                try
                {
                    foreach (var dc in brUI.RelicRewardList)
                    {
                        if (dc?.data == null) continue;
                        var entry = new JObject();
                        TryAddData(entry, dc.data, "Id", "id");
                        TryAddData(entry, dc.data, "Name", "name");
                        TryAddData(entry, dc.data, "Rarity", "rarity");
                        entry["instanceId"] = dc.InstanceID ?? "";
                        relicList.Add(entry);
                    }
                }
                catch { }
                result["relicRewards"] = relicList;

                // 反射读取私有字段 Money
                int moneyAmount = 0;
                try
                {
                    var moneyField = typeof(BattleRewardsUI).GetField("Money", _nonPublicInstance);
                    if (moneyField != null)
                        moneyAmount = (int)moneyField.GetValue(brUI);
                }
                catch { }
                result["unclaimedMoney"] = moneyAmount;

                // 读取 itemList 中的 PointUse 条目
                var rewards = new JArray();
                if (brUI.itemList != null)
                {
                    foreach (Transform child in brUI.itemList)
                    {
                        if (child == null || !child.gameObject.activeInHierarchy) continue;
                        if (child.name == "Text") continue; // 标题文本
                        if (child.name == "Item" && !child.gameObject.activeSelf) continue; // 模板

                        var pointUse = child.GetComponent<PointUse>();
                        if (pointUse == null) continue;

                        var entry = new JObject();
                        entry["name"] = child.name;
                        entry["rewardType"] = pointUse.RewardType ?? "Unknown";
                        entry["hierarchy"] = $"BattleRewardsUI/Window Manager/Windows/奖励选择/Content/List View Custom/Scroll Area/List/{child.name}";//todo硬编码路径？是否可以变为动态获取？

                        // 读取 title 和 description 文本（通过 TextMeshPro 组件反射读取）
                        try
                        {
                            var titleTf = child.Find("Normal/texts/title");
                            if (titleTf != null)
                            {
                                var tmp = titleTf.GetComponent("TMP_Text");
                                if (tmp != null)
                                {
                                    var textProp = tmp.GetType().GetProperty("text");
                                    if (textProp != null)
                                        entry["title"] = (string)textProp.GetValue(tmp, null) ?? "";
                                }
                            }
                            var descTf = child.Find("Normal/texts/description");
                            if (descTf != null)
                            {
                                var tmp = descTf.GetComponent("TMP_Text");
                                if (tmp != null)
                                {
                                    var textProp = tmp.GetType().GetProperty("text");
                                    if (textProp != null)
                                        entry["description"] = (string)textProp.GetValue(tmp, null) ?? "";
                                }
                            }
                        }
                        catch { }

                        // 如果有 DataConfig，读取卡牌 ID
                        if (pointUse.dataConfig?.data != null)
                        {
                            TryAddData(entry, pointUse.dataConfig.data, "Id", "cardId");
                            TryAddData(entry, pointUse.dataConfig.data, "Name", "cardName");
                        }

                        rewards.Add(entry);
                    }
                }
                result["rewards"] = rewards;
                result["rewardCount"] = rewards.Count;

                return (JToken)result;
            });
        }

        private static void TryAddData(JObject target, IDictionary<string, string> source, string key, string targetKey)
        {
            if (source != null && source.TryGetValue(key, out var val))
                target[targetKey] = val;
        }
    }
}
