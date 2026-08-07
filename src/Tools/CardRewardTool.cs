using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class PickCardRewardTool : IMcpTool
    {
        public string Name => "pick_card_reward";
        public string Description => "在 CardChoiceUI 中按索引选择一张卡牌奖励。index 从 0 开始。常用于战斗胜利后的三选一。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["index"] = new JObject { ["type"] = "integer", ["description"] = "卡牌索引(0-based)，通常0/1/2对应三张卡" }
            },
            ["required"] = new JArray { "index" }
        };

        public async Task<JToken> Execute(JToken args)
        {
            int? index = args?["index"]?.Value<int>();
            if (!index.HasValue || index.Value < 0)
                throw new ArgumentException("index 必须 >= 0");

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                var ccUI = UIManager.Instance?.GetUI<CardChoiceUI>("CardChoiceUI");
                if (ccUI == null || !ccUI.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "当前没有 CardChoiceUI";
                    return (JToken)result;
                }

                var cardItems = ccUI.GetComponentsInChildren<CardChoiceItem>(true)
                    .Where(c => c.gameObject.activeInHierarchy)
                    .ToList();

                if (cardItems.Count == 0)
                {
                    result["result"] = "error";
                    result["message"] = "CardChoiceUI 中没有找到卡牌选项";
                    return (JToken)result;
                }

                if (index.Value >= cardItems.Count)
                {
                    result["result"] = "error";
                    result["message"] = $"索引 {index.Value} 超出范围，共有 {cardItems.Count} 张卡";
                    result["totalCards"] = cardItems.Count;
                    return (JToken)result;
                }

                var chosen = cardItems[index.Value];
                var btn = chosen.GetComponent<UnityEngine.UI.Button>();
                if (btn == null || !btn.interactable)
                {
                    result["result"] = "error";
                    result["message"] = $"卡牌 {index.Value} 不可点击";
                    return (JToken)result;
                }

                btn.onClick.Invoke();
                result["result"] = "success";
                result["message"] = $"已选择卡牌 {index.Value}";
                result["cardIndex"] = index.Value;

                return (JToken)result;
            });
        }
    }

    public class SkipCardRewardTool : IMcpTool
    {
        public string Name => "skip_card_reward";
        public string Description => "跳过当前的卡牌奖励选择，关闭 CardChoiceUI。";
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

                var ccUI = UIManager.Instance?.GetUI<CardChoiceUI>("CardChoiceUI");
                if (ccUI != null && ccUI.gameObject.activeInHierarchy)
                {
                    ccUI.Close();
                    result["result"] = "success";
                    result["message"] = "已跳过卡牌奖励";
                }
                else
                {
                    result["result"] = "no_card_choice";
                    result["message"] = "当前没有 CardChoiceUI";
                }

                return (JToken)result;
            });
        }
    }
}
