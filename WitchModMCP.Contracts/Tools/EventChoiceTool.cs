using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Events;
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

        private static readonly BindingFlags _publicInstance = BindingFlags.Public | BindingFlags.Instance;

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

                // 反射找 ButtonManager（不依赖 Plugins.dll），找不到再 fallback 到标准 Button
                var allButtons = new List<(UnityEngine.Component comp, string name)>();
                CollectClickableOptions(eventUI.transform, allButtons);

                if (allButtons.Count == 0)
                {
                    // Fallback: 找标准的 Button
                    var stdButtons = eventUI.GetComponentsInChildren<Button>(false)
                        .Where(b => b.interactable && b.gameObject.activeInHierarchy)
                        .ToList();
                    foreach (var b in stdButtons)
                        allButtons.Add((b, b.GetComponentInChildren<Text>(true)?.text ?? b.name));
                }

                if (allButtons.Count == 0)
                {
                    result["result"] = "error";
                    result["message"] = "事件中没有找到可交互的按钮";
                    return (JToken)result;
                }

                if (index.Value > allButtons.Count)
                {
                    result["result"] = "error";
                    result["message"] = $"索引 {index.Value} 超出范围，共有 {allButtons.Count} 个选项";
                    result["totalChoices"] = allButtons.Count;
                    return (JToken)result;
                }

                var (target, btnText) = allButtons[index.Value - 1];

                try
                {
                    // 尝试 ButtonManager.onClick（反射）
                    if (TryInvokeButtonManagerClick(target))
                    {
                        // success
                    }
                    // Fallback: 标准 Button.onClick
                    else if (target is Button btn)
                    {
                        btn.onClick.Invoke();
                    }
                    else
                    {
                        result["result"] = "error";
                        result["message"] = "选项组件无法触发点击";
                        return (JToken)result;
                    }
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
                result["totalChoices"] = allButtons.Count;

                return (JToken)result;
            });
        }

        private static void CollectClickableOptions(Transform root, List<(UnityEngine.Component, string)> results)
        {
            var monos = root.GetComponentsInChildren<MonoBehaviour>(false);
            foreach (var comp in monos)
            {
                if (comp == null) continue;
                var type = comp.GetType();
                if (type.Name != "ButtonManager") continue;

                // 检查 isInteractable
                var interactField = type.GetField("isInteractable", _publicInstance);
                bool interactable = interactField == null || (bool)interactField.GetValue(comp);
                if (!interactable) continue;

                var text = comp.GetComponentInChildren<Text>(true)?.text ?? comp.name;
                results.Add((comp, text));
            }
        }

        private static bool TryInvokeButtonManagerClick(UnityEngine.Component comp)
        {
            var type = comp.GetType();
            if (type.Name != "ButtonManager") return false;

            var onClickField = type.GetField("onClick", _publicInstance);
            if (onClickField?.GetValue(comp) is UnityEvent onClick)
            {
                onClick.Invoke();
                return true;
            }
            return false;
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
                    // 改为调 Entry() 而不是 TryChangeMap()，确保 EntryScript 执行
                    eventUI.Entry();
                    result["result"] = "success";
                    result["message"] = "已触发事件结束 / 返回地图";
                    result["action"] = "Entry";
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
