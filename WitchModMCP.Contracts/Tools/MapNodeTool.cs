using System;
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
    public class MapListNodeTool : IMcpTool
    {
        public string Name => "map_list_nodes";
        public string Description => "列出当前地图上可到达的节点。返回每个节点的索引、类型和名称。需在地图页面。";
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

                var mapUI = UIManager.Instance?.GetUI<MapSelectUI>("MapSelectUI");
                if (mapUI == null || !mapUI.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "当前不在地图页面";
                    return (JToken)result;
                }

                result["level"] = MapManager.Instance?.Level ?? -1;

                var nodes = new JArray();
                int index = 0;

                var allChildren = mapUI.GetComponentsInChildren<Button>(false)
                    .Where(b => b.gameObject.activeInHierarchy)
                    .ToList();

                foreach (var btn in allChildren)
                {
                    var entry = new JObject
                    {
                        ["index"] = index,
                        ["name"] = btn.name,
                        ["interactable"] = btn.interactable
                    };
                    var img = btn.GetComponent<Image>();
                    if (img != null && img.sprite != null)
                        entry["sprite"] = img.sprite.name;
                    nodes.Add(entry);
                    index++;
                }

                result["nodes"] = nodes;
                result["totalNodes"] = index;
                result["inRun"] = true;

                return (JToken)result;
            });
        }
    }

    public class MapChooseNodeTool : IMcpTool
    {
        public string Name => "map_choose_node";
        public string Description => "在地图上选择并前往一个节点。index 从 0 开始，对应 map_list_nodes 返回的节点索引。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["index"] = new JObject { ["type"] = "integer", ["description"] = "节点索引(0-based)" }
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

                var mapUI = UIManager.Instance?.GetUI<MapSelectUI>("MapSelectUI");
                if (mapUI == null || !mapUI.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "当前不在地图页面";
                    return (JToken)result;
                }

                var buttons = mapUI.GetComponentsInChildren<Button>(false)
                    .Where(b => b.interactable && b.gameObject.activeInHierarchy)
                    .ToList();

                if (index.Value >= buttons.Count)
                {
                    result["result"] = "error";
                    result["message"] = $"索引 {index.Value} 超出范围，共有 {buttons.Count} 个可交互节点";
                    result["totalNodes"] = buttons.Count;
                    return (JToken)result;
                }

                var target = buttons[index.Value];
                var nodeName = target.name;
                var nodeText = target.GetComponentInChildren<Text>(true)?.text ?? nodeName;

                try
                {
                    target.onClick.Invoke();
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"点击节点失败: {ex.Message}";
                    return (JToken)result;
                }

                result["result"] = "success";
                result["message"] = $"已选择节点 {index.Value}: {nodeText}";
                result["nodeIndex"] = index.Value;
                result["nodeName"] = nodeName;
                result["totalNodes"] = buttons.Count;

                return (JToken)result;
            });
        }
    }
}
