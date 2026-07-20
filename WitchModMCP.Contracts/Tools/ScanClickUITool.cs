using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class ScanUITool : IMcpTool
    {
        public string Name => "scan_ui";
        public string Description => "扫描当前场景中所有可交互的 UI 组件（Button + ButtonManager），返回带层级路径和面板归属的结构化列表。AI 可用此工具发现页面上所有可点击元素。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["panel"] = new JObject { ["type"] = "string", ["description"] = "可选筛选：只扫描指定面板下的元素（如 EventUI、DeckUI），模糊匹配" },
                ["includeInactive"] = new JObject { ["type"] = "boolean", ["description"] = "是否包含非活跃/不可交互组件（默认 false）" },
                ["interactableOnly"] = new JObject { ["type"] = "boolean", ["description"] = "是否只返回可交互的（默认 true）" }
            }
        };

        private static readonly BindingFlags _publicInstance = BindingFlags.Public | BindingFlags.Instance;

        public async Task<JToken> Execute(JToken args)
        {
            string filterPanel = args?["panel"]?.Value<string>();
            bool includeInactive = args?["includeInactive"]?.Value<bool>() ?? false;
            bool interactableOnly = args?["interactableOnly"]?.Value<bool>() ?? true;

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var elements = new List<JObject>();
                var processed = new HashSet<GameObject>();

                var rootObjects = GameObject.FindObjectsOfType<GameObject>()
                    .Where(go => go.transform.parent == null)
                    .ToList();

                foreach (var root in rootObjects)
                {
                    ScanTransform(root.transform, root.name, "", elements, processed,
                        includeInactive, interactableOnly, filterPanel);
                }

                var sorted = elements.OrderBy(e => (string)e["hierarchy"]).ToList();
                for (int i = 0; i < sorted.Count; i++)
                    sorted[i]["index"] = i;

                var result = new JObject
                {
                    ["result"] = "success",
                    ["totalElements"] = sorted.Count,
                    ["elements"] = JArray.FromObject(sorted),
                    ["message"] = $"找到 {sorted.Count} 个 UI 元素"
                };

                return (JToken)result;
            });
        }

        private static void ScanTransform(
            Transform t, string panelName, string parentPath,
            List<JObject> results, HashSet<GameObject> processed,
            bool includeInactive, bool interactableOnly, string filterPanel)
        {
            if (t == null) return;
            if (!processed.Add(t.gameObject)) return;

            string myPath = string.IsNullOrEmpty(parentPath) ? t.name : parentPath + "/" + t.name;

            string myPanel = panelName;
            if (string.IsNullOrEmpty(parentPath))
                myPanel = t.name == "Canvas" && t.childCount > 0 ? t.GetChild(0).name : t.name;

            bool panelMatch = string.IsNullOrEmpty(filterPanel) ||
                myPanel.IndexOf(filterPanel, StringComparison.OrdinalIgnoreCase) >= 0;

            bool active = t.gameObject.activeInHierarchy;

            if (panelMatch && (active || includeInactive))
            {
                bool added = false;

                var monos = t.GetComponents<MonoBehaviour>();
                foreach (var comp in monos)
                {
                    if (comp == null) continue;
                    var type = comp.GetType();
                    if (type.Name != "ButtonManager") continue;

                    var interactField = type.GetField("isInteractable", _publicInstance);
                    bool interactable = interactField == null || (bool)interactField.GetValue(comp);
                    if (interactableOnly && !interactable) break;

                    var text = GetButtonText(comp.gameObject);

                    results.Add(new JObject
                    {
                        ["text"] = text,
                        ["type"] = "ButtonManager",
                        ["interactable"] = interactable,
                        ["hierarchy"] = myPath,
                        ["panel"] = myPanel
                    });
                    added = true;
                    break;
                }

                if (!added)
                {
                    var btn = t.GetComponent<Button>();
                    if (btn != null)
                    {
                        bool interactable = btn.interactable;
                        if (!interactableOnly || interactable)
                        {
                            var text = GetButtonText(btn.gameObject);
                            results.Add(new JObject
                            {
                                ["text"] = text,
                                ["type"] = "Button",
                                ["interactable"] = interactable,
                                ["hierarchy"] = myPath,
                                ["panel"] = myPanel
                            });
                        }
                    }
                }
            }

            foreach (Transform child in t)
                ScanTransform(child, myPanel, myPath, results, processed,
                    includeInactive, interactableOnly, filterPanel);
        }

        private static string GetButtonText(GameObject go)
        {
            var text = go.GetComponentInChildren<Text>(true);
            if (text != null && !string.IsNullOrEmpty(text.text))
                return text.text;
            return go.name;
        }
    }

    public class ClickUITool : IMcpTool
    {
        public string Name => "click_ui";
        public string Description => "按 scan_ui 返回的索引点击 UI 元素。支持 ButtonManager 和标准 Button。优先使用专用工具（如 event_choose_option）代替此通用工具。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["index"] = new JObject { ["type"] = "integer", ["description"] = "scan_ui 返回的元素索引(0-based)" },
                ["allowInactive"] = new JObject { ["type"] = "boolean", ["description"] = "是否允许点击非交互组件（默认 false）" }
            },
            ["required"] = new JArray { "index" }
        };

        private static readonly BindingFlags _publicInstance = BindingFlags.Public | BindingFlags.Instance;

        public async Task<JToken> Execute(JToken args)
        {
            int? index = args?["index"]?.Value<int>();
            if (!index.HasValue || index.Value < 0)
                throw new ArgumentException("index 必须 >= 0");

            bool allowInactive = args?["allowInactive"]?.Value<bool>() ?? false;

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();
                var elements = new List<(UnityEngine.Component comp, string type, string text, string hierarchy)>();
                var processed = new HashSet<GameObject>();

                var rootObjects = GameObject.FindObjectsOfType<GameObject>()
                    .Where(go => go.transform.parent == null)
                    .ToList();

                foreach (var root in rootObjects)
                    CollectElements(root.transform, root.name, "", elements, processed, allowInactive);

                elements = elements.OrderBy(e => e.hierarchy).ToList();

                if (index.Value >= elements.Count)
                {
                    result["result"] = "error";
                    result["message"] = $"索引 {index.Value} 超出范围，当前只有 {elements.Count} 个元素";
                    result["totalElements"] = elements.Count;
                    return (JToken)result;
                }

                var target = elements[index.Value];

                try
                {
                    bool clicked = false;

                    if (target.type == "ButtonManager")
                        clicked = TryInvokeButtonManagerClick(target.comp);
                    else if (target.type == "Button" && target.comp is Button btn)
                    {
                        btn.onClick.Invoke();
                        clicked = true;
                    }

                    if (!clicked)
                    {
                        result["result"] = "error";
                        result["message"] = $"元素 {index.Value} ({target.hierarchy}) 无法触发点击";
                        return (JToken)result;
                    }

                    result["result"] = "success";
                    result["message"] = $"已点击元素 {index.Value}: {target.text}";
                    result["text"] = target.text;
                    result["hierarchy"] = target.hierarchy;
                    result["type"] = target.type;
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"点击元素 {index.Value} 失败: {ex.Message}";
                    result["hierarchy"] = target.hierarchy;
                }

                return (JToken)result;
            });
        }

        private static void CollectElements(
            Transform t, string panelName, string parentPath,
            List<(UnityEngine.Component, string, string, string)> results,
            HashSet<GameObject> processed, bool allowInactive)
        {
            if (t == null) return;
            if (!processed.Add(t.gameObject)) return;

            string myPath = string.IsNullOrEmpty(parentPath) ? t.name : parentPath + "/" + t.name;

            bool active = t.gameObject.activeInHierarchy;
            if (!active && !allowInactive) goto recurse;

            bool added = false;

            var monos = t.GetComponents<MonoBehaviour>();
            foreach (var comp in monos)
            {
                if (comp == null) continue;
                var type = comp.GetType();
                if (type.Name != "ButtonManager") continue;

                var interactField = type.GetField("isInteractable", _publicInstance);
                bool interactable = interactField == null || (bool)interactField.GetValue(comp);
                if (!interactable && !allowInactive) break;

                var text = comp.GetComponentInChildren<Text>(true)?.text ?? comp.name;
                results.Add((comp, "ButtonManager", text, myPath));
                added = true;
                break;
            }

            if (!added)
            {
                var btn = t.GetComponent<Button>();
                if (btn != null)
                {
                    bool interactable = btn.interactable;
                    if (interactable || allowInactive)
                    {
                        var text = btn.GetComponentInChildren<Text>(true)?.text ?? btn.name;
                        results.Add((btn, "Button", text, myPath));
                    }
                }
            }

            recurse:
            foreach (Transform child in t)
                CollectElements(child, panelName, myPath, results, processed, allowInactive);
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
}
