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
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class ScanUITool : IMcpTool
    {
        public string Name => "scan_ui";
        public string Description => "扫描当前场景中所有可交互的 UI 组件（Button + ButtonManager），返回带层级路径和面板归属的结构化列表。每个元素附带 instanceId（Unity 运行时唯一 ID），传给 click_ui 可稳定定位元素，不怕索引漂移。注意：index 是全局索引（按 hierarchy 排序），panel 过滤只影响返回列表，不影响 index 值。";
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
                // === 1. 收集所有元素（不筛选）===
                var allElements = new List<JObject>();
                var processed = new HashSet<GameObject>();

                var rootObjects = GameObject.FindObjectsOfType<GameObject>()
                    .Where(go => go.transform.parent == null)
                    .ToList();

                foreach (var root in rootObjects)
                {
                    CollectAllElements(root.transform, "", allElements, processed,
                        includeInactive, interactableOnly);
                }

                // === 2. 按 hierarchy 排序并分配全局索引 ===
                var sorted = allElements.OrderBy(e => (string)e["hierarchy"]).ToList();
                for (int i = 0; i < sorted.Count; i++)
                    sorted[i]["index"] = i;

                // === 3. 如果指定了 panel 过滤，对索引做后筛 ===
                // 保留全局 index 不变，只筛选 panel 匹配的元素
                var filtered = sorted;
                if (!string.IsNullOrEmpty(filterPanel))
                {
                    filtered = sorted
                        .Where(e =>
                        {
                            var panel = e["panel"]?.Value<string>() ?? "";
                            return panel.IndexOf(filterPanel, StringComparison.OrdinalIgnoreCase) >= 0;
                        })
                        .ToList();
                }

                var result = new JObject
                {
                    ["result"] = "success",
                    ["totalElements"] = filtered.Count,
                    ["elements"] = JArray.FromObject(filtered),
                    ["message"] = $"找到 {filtered.Count} 个 UI 元素" +
                        (string.IsNullOrEmpty(filterPanel) ? "" : $"（面板: {filterPanel}）")
                };

                return (JToken)result;
            });
        }

        /// <summary>
        /// 收集所有按钮元素，不做 panel 筛选（panel 筛选在 index 分配之后进行）。
        /// 这样 scan_ui 带 panel 过滤时返回的 index 和 click_ui 的全局索引一致。
        /// </summary>
        private static void CollectAllElements(
            Transform t, string parentPath,
            List<JObject> results, HashSet<GameObject> processed,
            bool includeInactive, bool interactableOnly)
        {
            if (t == null) return;
            if (!processed.Add(t.gameObject)) return;

            string myPath = string.IsNullOrEmpty(parentPath) ? t.name : parentPath + "/" + t.name;
            string myPanel = ResolvePanel(t);
            bool active = t.gameObject.activeInHierarchy;

            if (active || includeInactive)
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
                        ["instanceId"] = comp.GetInstanceID(),
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
                                ["instanceId"] = btn.GetInstanceID(),
                                ["hierarchy"] = myPath,
                                ["panel"] = myPanel
                            });
                        }
                    }
                }
            }

            foreach (Transform child in t)
                CollectAllElements(child, myPath, results, processed,
                    includeInactive, interactableOnly);
        }

        private static string ResolvePanel(Transform t)
        {
            var canvasTf = UIManager.Instance?.canvasTf;
            var upperCanvasTf = UIManager.Instance?.upperCanvasTf;

            Transform current = t;
            while (current != null)
            {
                if ((canvasTf != null && current.parent == canvasTf) ||
                    (upperCanvasTf != null && current.parent == upperCanvasTf))
                {
                    return current.name;
                }
                current = current.parent;
            }

            var root = t.root;
            return root != null ? root.name : "Unknown";
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
        public string Description => "按 scan_ui 返回的 instanceId 点击 UI 元素（稳定性高于 index）。支持 ButtonManager 和标准 Button。优先使用专用工具（如 event_choose_option）代替此通用工具。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["instanceId"] = new JObject { ["type"] = "integer", ["description"] = "（推荐）scan_ui 返回的运行时实例 ID（Unity Object.GetInstanceID），不怕索引漂移" },
                ["index"] = new JObject { ["type"] = "integer", ["description"] = "（后备）scan_ui 返回的 0-based 索引，instanceId 不存在时使用" },
                ["allowInactive"] = new JObject { ["type"] = "boolean", ["description"] = "是否允许点击非交互组件（默认 false）" }
            }
        };

        private static readonly BindingFlags _publicInstance = BindingFlags.Public | BindingFlags.Instance;

        public async Task<JToken> Execute(JToken args)
        {
            int? instanceId = args?["instanceId"]?.Value<int>();
            int? index = args?["index"]?.Value<int>();

            if (!instanceId.HasValue && !index.HasValue)
                throw new ArgumentException("需要提供 instanceId 或 index 之一");

            bool allowInactive = args?["allowInactive"]?.Value<bool>() ?? false;

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();
                var elements = new List<(UnityEngine.Component comp, string type, string text, string hierarchy, int instId)>();
                var processed = new HashSet<GameObject>();

                var rootObjects = GameObject.FindObjectsOfType<GameObject>()
                    .Where(go => go.transform.parent == null)
                    .ToList();

                foreach (var root in rootObjects)
                    CollectElements(root.transform, root.name, "", elements, processed, allowInactive);

                (UnityEngine.Component comp, string type, string text, string hierarchy, int instId) target = default;
                bool found = false;

                // 优先按 instanceId 匹配
                if (instanceId.HasValue)
                {
                    foreach (var e in elements)
                    {
                        if (e.instId == instanceId.Value)
                        {
                            target = e;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        result["result"] = "error";
                        result["message"] = $"未找到 instanceId={instanceId} 对应的 UI 元素（可能已被销毁）";
                        return (JToken)result;
                    }
                }
                else
                {
                    // 后备：按 index 匹配
                    elements = elements.OrderBy(e => e.hierarchy).ToList();
                    if (index.Value >= elements.Count)
                    {
                        result["result"] = "error";
                        result["message"] = $"索引 {index.Value} 超出范围，当前只有 {elements.Count} 个元素";
                        result["totalElements"] = elements.Count;
                        return (JToken)result;
                    }
                    target = elements[index.Value];
                    found = true;
                }

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
                        result["message"] = $"元素 ({target.hierarchy}) 无法触发点击";
                        return (JToken)result;
                    }

                    result["result"] = "success";
                    result["message"] = $"已点击元素: {target.text}";
                    result["text"] = target.text;
                    result["hierarchy"] = target.hierarchy;
                    result["type"] = target.type;
                    result["instanceId"] = target.instId;
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"点击元素失败: {ex.Message}";
                    result["hierarchy"] = target.hierarchy;
                }

                return (JToken)result;
            });
        }

        private static void CollectElements(
            Transform t, string panelName, string parentPath,
            List<(UnityEngine.Component, string, string, string, int)> results,
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
                results.Add((comp, "ButtonManager", text, myPath, comp.GetInstanceID()));
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
                        results.Add((btn, "Button", text, myPath, btn.GetInstanceID()));
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
