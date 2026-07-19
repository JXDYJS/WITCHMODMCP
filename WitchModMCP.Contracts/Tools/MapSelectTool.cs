using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class MapSelectStateTool : IMcpTool
    {
        public string Name => "map_select_state";
        public string Description => "获取地图节点编排界面(MapSelectUI)的当前状态：可选节点列表('手牌')、槽位填充情况、是否可以继续。每个节点包含 type(Fight/Event/Build)、note(普通/精英/首领等)、name 等信息。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
        };

        private static MapSelectUI TryGetMapSelectUI()
        {
            var ui = UIManager.Instance?.GetUI<MapSelectUI>("MapSelectUI");
            if (ui == null || !ui.gameObject.activeInHierarchy)
                return null;
            return ui;
        }

        public async Task<JToken> Execute(JToken args)
        {
            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();
                var mapUI = TryGetMapSelectUI();
                if (mapUI == null)
                {
                    result["result"] = "error";
                    result["message"] = "当前不在节点编排界面(MapSelectUI)";
                    return (JToken)result;
                }

                result["isSelecting"] = mapUI.transform.Find("MapSelect") != null;

                // 1. Selectable nodes (cards in "MapSelect" container)
                var selectableNodes = new JArray();
                var selectContainer = mapUI.transform.Find("MapSelect");
                if (selectContainer != null)
                {
                    int idx = 0;
                    foreach (Transform child in selectContainer)
                    {
                        var item = child.GetComponent<MapItem>();
                        if (item == null || item.node == null || item.node.data == null)
                        {
                            idx++;
                            continue;
                        }

                        var nd = new JObject
                        {
                            ["index"] = idx,
                            ["id"] = GetData(item.node, "Id"),
                            ["nodeId"] = GetData(item.node, "NodeId"),
                            ["type"] = item.node.type ?? GetData(item.node, "Type"),
                            ["note"] = GetData(item.node, "Note"),
                            ["name"] = GetData(item.node, "Name")
                        };
                        selectableNodes.Add(nd);
                        idx++;
                    }
                }
                result["selectableNodes"] = selectableNodes;

                // 2. Slots
                var slots = new JArray();
                var nodeContent = mapUI.transform.Find("Map/NodeContent");
                if (nodeContent != null)
                {
                    for (int i = 0; i < nodeContent.childCount; i++)
                    {
                        var slot = nodeContent.GetChild(i);
                        var slotObj = new JObject
                        {
                            ["index"] = i,
                            ["name"] = slot.name
                        };

                        var contentChild = slot.Find("Content");
                        MapItem slotItem = contentChild != null
                            ? contentChild.GetComponentInChildren<MapItem>(true)
                            : null;

                        if (slotItem != null && slotItem.node != null && slotItem.node.data != null)
                        {
                            slotObj["filled"] = true;
                            slotObj["node"] = new JObject
                            {
                                ["id"] = GetData(slotItem.node, "Id"),
                                ["nodeId"] = GetData(slotItem.node, "NodeId"),
                                ["type"] = slotItem.node.type ?? GetData(slotItem.node, "Type"),
                                ["note"] = GetData(slotItem.node, "Note"),
                                ["name"] = GetData(slotItem.node, "Name")
                            };
                        }
                        else
                        {
                            slotObj["filled"] = false;
                        }

                        slots.Add(slotObj);
                    }
                }
                result["slots"] = slots;

                bool allFilled = slots.All(s => s["filled"]?.Value<bool>() == true);
                result["canContinue"] = allFilled;
                result["slotCount"] = slots.Count;
                result["selectableCount"] = selectableNodes.Count;
                result["result"] = "success";

                return (JToken)result;
            });
        }

        private static string GetData(MapTree.Node node, string key)
        {
            if (node?.data == null) return "";
            return node.data.TryGetValue(key, out var val) ? val ?? "" : "";
        }
    }

    public class MapSelectAssignTool : IMcpTool
    {
        public string Name => "map_select_assign";
        public string Description => "将可选节点放置到指定槽位。支持批量操作。使用 nodeId（稳定 ID，从 map_select_state 的 selectableNodes 中获得）而非容易变化的 index。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["slotIndex"] = new JObject { ["type"] = "integer", ["description"] = "（可选，与 nodeId 搭配使用）单次放置时的槽位索引 (0-5)" },
                ["nodeId"] = new JObject { ["type"] = "string", ["description"] = "（可选，与 slotIndex 搭配使用）单次放置时的节点 NodeId" },
                ["mappings"] = new JObject
                {
                    ["type"] = "array",
                    ["description"] = "批量放置映射列表，每个元素指定槽位和节点 ID",
                    ["items"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["slotIndex"] = new JObject { ["type"] = "integer", ["description"] = "槽位索引 (0-5)" },
                            ["nodeId"] = new JObject { ["type"] = "string", ["description"] = "节点 NodeId（稳定 ID，从 map_select_state 获取）" }
                        },
                        ["required"] = new JArray { "slotIndex", "nodeId" }
                    }
                }
            }
        };

        public async Task<JToken> Execute(JToken args)
        {
            var mappings = new List<(int slotIndex, string nodeId)>();

            var mappingsArr = args?["mappings"];
            if (mappingsArr != null && mappingsArr.Type == JTokenType.Array && mappingsArr.HasValues)
            {
                foreach (var m in mappingsArr)
                {
                    int si = m["slotIndex"]?.Value<int>() ?? -1;
                    string nid = m["nodeId"]?.Value<string>();
                    if (si < 0 || si > 5)
                        throw new ArgumentException("每个 mapping 的 slotIndex 必须在 0-5 之间");
                    if (string.IsNullOrEmpty(nid))
                        throw new ArgumentException("每个 mapping 必须有 nodeId");
                    mappings.Add((si, nid));
                }
            }
            else
            {
                int slotIndex = args?["slotIndex"]?.Value<int>() ?? -1;
                string nodeId = args?["nodeId"]?.Value<string>();
                if (slotIndex < 0 || slotIndex > 5)
                    throw new ArgumentException("slotIndex 必须在 0-5 之间，或使用 mappings 数组进行批量放置");
                if (string.IsNullOrEmpty(nodeId))
                    throw new ArgumentException("需要提供 nodeId，或使用 mappings 数组进行批量放置");
                mappings.Add((slotIndex, nodeId));
            }

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                if (mappings.Count == 0)
                {
                    result["result"] = "error";
                    result["message"] = "未提供任何放置映射";
                    return (JToken)result;
                }

                var mapUI = UIManager.Instance?.GetUI<MapSelectUI>("MapSelectUI");
                if (mapUI == null || !mapUI.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "当前不在节点编排界面(MapSelectUI)";
                    return (JToken)result;
                }

                var selectContainer = mapUI.transform.Find("MapSelect");
                if (selectContainer == null)
                {
                    result["result"] = "error";
                    result["message"] = "未找到可选节点容器(MapSelect)";
                    return (JToken)result;
                }

                var nodeContent = mapUI.transform.Find("Map/NodeContent");
                if (nodeContent == null)
                {
                    result["result"] = "error";
                    result["message"] = "未找到槽位容器(Map/NodeContent)";
                    return (JToken)result;
                }

                var placed = new JArray();
                var errors = new JArray();

                foreach (var (slotIndex, nodeId) in mappings)
                {
                    // Find source by stable nodeId (not index)
                    MapItem sourceItem = null;
                    foreach (Transform child in selectContainer)
                    {
                        var item = child.GetComponent<MapItem>();
                        if (item == null || item.node?.data == null) continue;

                        if (item.node.data.TryGetValue("NodeId", out var val)
                            && val is string s && s == nodeId)
                        {
                            sourceItem = item;
                            break;
                        }
                    }

                    if (sourceItem == null)
                    {
                        errors.Add(new JObject
                        {
                            ["slotIndex"] = slotIndex,
                            ["nodeId"] = nodeId,
                            ["error"] = "未找到此 nodeId 的可选节点（可能已被放置或不存在）"
                        });
                        continue;
                    }

                    if (slotIndex >= nodeContent.childCount)
                    {
                        errors.Add(new JObject
                        {
                            ["slotIndex"] = slotIndex,
                            ["nodeId"] = nodeId,
                            ["error"] = $"槽位索引 {slotIndex} 超出范围"
                        });
                        continue;
                    }

                    var targetSlot = nodeContent.GetChild(slotIndex);
                    var targetContent = targetSlot.Find("Content");
                    if (targetContent == null)
                    {
                        errors.Add(new JObject
                        {
                            ["slotIndex"] = slotIndex,
                            ["nodeId"] = nodeId,
                            ["error"] = "槽位缺少 Content 子对象"
                        });
                        continue;
                    }

                    // Clear existing card in slot
                    var existingItem = targetContent.GetComponentInChildren<MapItem>(true);
                    if (existingItem != null)
                        GameObject.Destroy(existingItem.gameObject);

                    var nullObj = targetContent.Find("Null");
                    if (nullObj != null)
                        nullObj.gameObject.SetActive(false);

                    // Move MapItem to slot
                    sourceItem.transform.SetParent(targetContent, false);
                    sourceItem.transform.localPosition = Vector3.zero;
                    sourceItem.transform.localRotation = Quaternion.identity;
                    sourceItem.transform.localScale = Vector3.one;

                    placed.Add(new JObject
                    {
                        ["slotIndex"] = slotIndex,
                        ["nodeId"] = nodeId
                    });
                }

                // Sync once after all placements
                if (placed.Count > 0)
                {
                    try
                    {
                        var setNodesMethod = typeof(MapSelectUI).GetMethod("SetNodes",
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (setNodesMethod != null)
                            setNodesMethod.Invoke(mapUI, null);
                        else
                            FallbackSync(mapUI);
                    }
                    catch
                    {
                        FallbackSync(mapUI);
                    }
                }

                string status = errors.Count > 0
                    ? (placed.Count > 0 ? "partial" : "error")
                    : "success";

                result["result"] = status;
                result["placed"] = placed;
                result["placedCount"] = placed.Count;

                if (errors.Count > 0)
                {
                    result["errors"] = errors;
                    result["errorCount"] = errors.Count;
                }

                result["message"] = placed.Count > 0
                    ? $"成功放置 {placed.Count} 个节点" + (errors.Count > 0 ? $"，{errors.Count} 个失败" : "")
                    : "未放置任何节点";

                return (JToken)result;
            });
        }

        private static void FallbackSync(MapSelectUI mapUI)
        {
            var nodeContent = mapUI.transform.Find("Map/NodeContent");
            if (nodeContent == null) return;

            int count = nodeContent.childCount;
            var ids = new string[count];
            var nodeIds = new string[count];

            for (int i = 0; i < count; i++)
            {
                var slot = nodeContent.GetChild(i);
                var contentChild = slot.Find("Content");
                var item = contentChild != null
                    ? contentChild.GetComponentInChildren<MapItem>(true)
                    : null;
                if (item?.node?.data != null)
                {
                    item.node.data.TryGetValue("Id", out var id);
                    item.node.data.TryGetValue("NodeId", out var nid);
                    ids[i] = id;
                    nodeIds[i] = nid;
                }
                else
                {
                    ids[i] = null;
                    nodeIds[i] = null;
                }
            }

            MapManager.Instance.CmdSelectMap(ids, nodeIds);
        }
    }

    public class MapSelectClearTool : IMcpTool
    {
        public string Name => "map_select_clear";
        public string Description => "清空指定槽位的节点。slotIndex: 槽位索引(0-5)。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["slotIndex"] = new JObject { ["type"] = "integer", ["description"] = "槽位索引 (0-5)" }
            },
            ["required"] = new JArray { "slotIndex" }
        };

        public async Task<JToken> Execute(JToken args)
        {
            int slotIndex = args?["slotIndex"]?.Value<int>() ?? -1;
            if (slotIndex < 0 || slotIndex > 5)
                throw new ArgumentException("slotIndex 必须在 0-5 之间");

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();
                var mapUI = UIManager.Instance?.GetUI<MapSelectUI>("MapSelectUI");
                if (mapUI == null || !mapUI.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "当前不在节点编排界面(MapSelectUI)";
                    return (JToken)result;
                }

                var nodeContent = mapUI.transform.Find("Map/NodeContent");
                if (nodeContent == null || slotIndex >= nodeContent.childCount)
                {
                    result["result"] = "error";
                    result["message"] = $"未找到 slotIndex={slotIndex} 的槽位";
                    return (JToken)result;
                }

                var targetSlot = nodeContent.GetChild(slotIndex);
                var targetContent = targetSlot.Find("Content");
                if (targetContent == null)
                {
                    result["result"] = "error";
                    result["message"] = "槽位缺少 Content 子对象";
                    return (JToken)result;
                }

                // Destroy existing MapItem
                var existingItem = targetContent.GetComponentInChildren<MapItem>(true);
                if (existingItem != null)
                {
                    GameObject.Destroy(existingItem.gameObject);
                }

                // Show the Null placeholder
                var nullObj = targetContent.Find("Null");
                if (nullObj != null)
                    nullObj.gameObject.SetActive(true);

                // Sync via SetNodes()
                SyncMap(mapUI);

                result["result"] = "success";
                result["message"] = $"已清空槽位 {slotIndex}";

                return (JToken)result;
            });
        }

        private static void SyncMap(MapSelectUI mapUI)
        {
            try
            {
                var setNodesMethod = typeof(MapSelectUI).GetMethod("SetNodes",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (setNodesMethod != null)
                {
                    setNodesMethod.Invoke(mapUI, null);
                    return;
                }
            }
            catch { }

            // Fallback
            var nodeContent = mapUI.transform.Find("Map/NodeContent");
            if (nodeContent == null) return;

            int count = nodeContent.childCount;
            var ids = new string[count];
            var nodeIds = new string[count];

            for (int i = 0; i < count; i++)
            {
                var slot = nodeContent.GetChild(i);
                var contentChild = slot.Find("Content");
                var item = contentChild != null
                    ? contentChild.GetComponentInChildren<MapItem>(true)
                    : null;
                if (item?.node?.data != null)
                {
                    item.node.data.TryGetValue("Id", out var id);
                    item.node.data.TryGetValue("NodeId", out var nodeId);
                    ids[i] = id;
                    nodeIds[i] = nodeId;
                }
            }
            MapManager.Instance.CmdSelectMap(ids, nodeIds);
        }
    }

    public class MapSelectConfirmTool : IMcpTool
    {
        public string Name => "map_select_confirm";
        public string Description => "确认当前地图节点编排并继续前进。所有6个槽位都必须已填充。";
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
                    result["message"] = "当前不在节点编排界面(MapSelectUI)";
                    return (JToken)result;
                }

                // Call TryContinue via reflection (likely private)
                try
                {
                    var tryContinueMethod = typeof(MapSelectUI).GetMethod("TryContinue",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (tryContinueMethod != null)
                    {
                        tryContinueMethod.Invoke(mapUI, null);
                        result["result"] = "success";
                        result["message"] = "已确认节点编排";
                    }
                    else
                    {
                        result["result"] = "error";
                        result["message"] = "找不到 MapSelectUI.TryContinue 方法";
                    }
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"调用 TryContinue 失败: {ex.Message}";
                }

                return (JToken)result;
            });
        }
    }
}
