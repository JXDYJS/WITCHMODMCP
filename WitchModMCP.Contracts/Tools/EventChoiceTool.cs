using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class EventChoiceTool : IMcpTool
    {
        public string Name => "event_choose_option";
        public string Description => "在事件UI中选取一个选项。index 从1开始(对应事件中第N个可选按钮)。自动定位 EventUI 下的可交互按钮。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["index"] = new JObject { ["type"] = "integer", ["description"] = "选项索引(1-based)" }
            },
            ["required"] = new JArray { "index" }
        };

        public async Task<JToken> Execute(JToken args)
        {
            int? index = args?["index"]?.Value<int>();
            if (!index.HasValue || index.Value < 1)
                throw new ArgumentException("index 必须 >= 1");

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                var eventUI = UIManager.Instance?.GetUI<EventUI>("EventUI");
                if (eventUI == null || !eventUI.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "当前没有打开的事件UI";
                    return (JToken)result;
                }

                var buttons = eventUI.GetComponentsInChildren<Button>(false)
                    .Where(b => b.interactable && b.gameObject.activeInHierarchy)
                    .ToList();

                if (buttons.Count == 0)
                {
                    result["result"] = "error";
                    result["message"] = "事件中没有找到可交互的按钮";
                    return (JToken)result;
                }

                if (index.Value > buttons.Count)
                {
                    result["result"] = "error";
                    result["message"] = $"索引 {index.Value} 超出范围，共有 {buttons.Count} 个选项";
                    result["totalChoices"] = buttons.Count;
                    return (JToken)result;
                }

                var target = buttons[index.Value - 1];
                var btnText = target.GetComponentInChildren<Text>(true)?.text ?? target.name;

                try
                {
                    target.onClick.Invoke();
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"点击选项失败: {ex.Message}";
                    return (JToken)result;
                }

                result["result"] = "success";
                result["message"] = $"已选择选项 {index.Value}: {btnText}";
                result["choice"] = btnText;
                result["choiceIndex"] = index.Value;
                result["totalChoices"] = buttons.Count;

                return (JToken)result;
            });
        }
    }

    public class EventAdvanceDialogueTool : IMcpTool
    {
        public string Name => "event_advance_dialogue";
        public string Description => "结束当前事件并返回地图。当事件无选项、已选择选项、或属于古老者对话时，调用此工具关闭事件并继续流程。";
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

                var eventUI = UIManager.Instance?.GetUI<EventUI>("EventUI");
                if (eventUI != null && eventUI.gameObject.activeInHierarchy)
                {
                    eventUI.TryChangeMap();
                    result["result"] = "success";
                    result["message"] = "已触发事件结束 / 返回地图";
                    result["action"] = "TryChangeMap";
                }
                else
                {
                    result["result"] = "no_event";
                    result["message"] = "当前没有打开的事件UI";
                }

                return (JToken)result;
            });
        }
    }
}
