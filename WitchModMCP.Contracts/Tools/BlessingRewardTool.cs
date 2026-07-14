using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using Witch.UI;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class PickBlessingRewardTool : IMcpTool
    {
        public string Name => "pick_blessing_reward";
        public string Description => "在 BlessingChoiceGenerator 中选择一个祝福奖励。index 从 0 开始。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["index"] = new JObject { ["type"] = "integer", ["description"] = "祝福选项索引(0-based)" }
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

                var blessUI = UIManager.Instance?.Find("BlessingChoiceGenerator");
                if (blessUI == null || !blessUI.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "当前没有 BlessingChoiceGenerator";
                    return (JToken)result;
                }

                var buttons = blessUI.GetComponentsInChildren<Button>(false)
                    .Where(b => b.interactable && b.gameObject.activeInHierarchy)
                    .ToList();

                if (buttons.Count == 0)
                {
                    result["result"] = "error";
                    result["message"] = "Blessing UI 中没有找到可交互的按钮";
                    return (JToken)result;
                }

                if (index.Value >= buttons.Count)
                {
                    result["result"] = "error";
                    result["message"] = $"索引 {index.Value} 超出范围，共有 {buttons.Count} 个按钮";
                    result["totalChoices"] = buttons.Count;
                    return (JToken)result;
                }

                var target = buttons[index.Value];
                var btnText = target.GetComponentInChildren<Text>(true)?.text ?? target.name;

                try
                {
                    target.onClick.Invoke();
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"点击祝福选项失败: {ex.Message}";
                    return (JToken)result;
                }

                result["result"] = "success";
                result["message"] = $"已选择祝福 {index.Value}: {btnText}";
                result["choiceIndex"] = index.Value;
                result["choice"] = btnText;
                result["totalChoices"] = buttons.Count;

                return (JToken)result;
            });
        }
    }

    public class SkipBlessingRewardTool : IMcpTool
    {
        public string Name => "skip_blessing_reward";
        public string Description => "跳过当前的祝福奖励选择，关闭 BlessingChoiceGenerator。";
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

                var blessUI = UIManager.Instance?.Find("BlessingChoiceGenerator");
                if (blessUI != null && blessUI.gameObject.activeInHierarchy)
                {
                    blessUI.Close();
                    result["result"] = "success";
                    result["message"] = "已跳过祝福奖励";
                }
                else
                {
                    result["result"] = "no_blessing";
                    result["message"] = "当前没有 BlessingChoiceGenerator";
                }

                return (JToken)result;
            });
        }
    }
}
