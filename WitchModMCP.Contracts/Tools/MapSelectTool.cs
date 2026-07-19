using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
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

                result["isSelecting"] = true;

                var mapManager = MapManager.Instance;
                var mapTree = mapManager?.MapTree;

                // 1. Selectable nodes from MapTree.SelectNode (backend — stable, no index issues)
                // Filter out nodes already placed in slots (by checking backend mapData)
                var placedNodeIds = mapManager?.mapData != null
                    ? new HashSet<string>(mapManager.mapData.Where(id => !string.IsNullOrEmpty(id)))
                    : new HashSet<string>();

                var selectableNodes = new JArray();
                if (mapTree?.SelectNode != null)
                {
                    foreach (var node in mapTree.SelectNode)
                    {
                        if (node?.data == null) continue;
                        var nid = GetData(node, "NodeId");
                        if (placedNodeIds.Contains(nid)) continue;
                        selectableNodes.Add(new JObject
                        {
                            ["nodeId"] = nid,
                            ["id"] = GetData(node, "Id"),
                            ["type"] = node.type ?? GetData(node, "Type"),
                            ["note"] = GetData(node, "Note"),
                            ["name"] = GetData(node, "Name")
                        });
                    }
                }
                result["selectableNodes"] = selectableNodes;

                // 2. Slots — read from MapManager.mapList/mapData (backend — source of truth)
                // Also cross-reference with UI for slot names.
                string[] slotNames = { "Start", "Node1", "Node2", "Node3", "Node4", "End" };
                var slots = new JArray();
                var nodeContent = mapUI.transform.Find("Map/NodeContent");

                // Build node info lookup from MapTree (all reachable nodes)
                var nodeInfoLookup = new Dictionary<string, MapTree.Node>();
                if (mapTree != null)
                {
                    void CollectNodes(MapTree.Node n)
                    {
                        if (n?.data == null) return;
                        var nid = GetData(n, "NodeId");
                        if (!string.IsNullOrEmpty(nid) && !nodeInfoLookup.ContainsKey(nid))
                            nodeInfoLookup[nid] = n;
                        if (n.childrens != null)
                            foreach (var c in n.childrens)
                                CollectNodes(c);
                    }
                    if (mapTree.SelectNode != null)
                        foreach (var n in mapTree.SelectNode)
                        {
                            var nid = GetData(n, "NodeId");
                            if (!string.IsNullOrEmpty(nid) && !nodeInfoLookup.ContainsKey(nid))
                                nodeInfoLookup[nid] = n;
                        }
                    if (mapTree.root != null)
                        CollectNodes(mapTree.root);
                }

                int slotCount = Math.Min(mapManager?.mapList?.Length ?? 0, 6);
                if (nodeContent != null)
                    slotCount = Math.Min(nodeContent.childCount, 6);

                for (int i = 0; i < slotCount; i++)
                {
                    string slotName = (nodeContent != null && i < nodeContent.childCount)
                        ? nodeContent.GetChild(i).name
                        : slotNames[i];

                    var slotObj = new JObject
                    {
                        ["index"] = i,
                        ["name"] = slotName
                    };

                    // Check backend first, then UI as fallback
                    string backendId = (mapManager?.mapList != null && i < mapManager.mapList.Length)
                        ? mapManager.mapList[i] : null;
                    string backendNodeId = (mapManager?.mapData != null && i < mapManager.mapData.Length)
                        ? mapManager.mapData[i] : null;

                    bool backendFilled = !string.IsNullOrEmpty(backendNodeId);

                    // Try UI for more detailed node info
                    string uiId = null, uiNodeId = null, uiType = null, uiNote = null, uiName = null;
                    MapItem slotItem = null;
                    if (nodeContent != null && i < nodeContent.childCount)
                    {
                        var slot = nodeContent.GetChild(i);
                        var contentChild = slot.Find("Content");
                        slotItem = contentChild?.GetComponentInChildren<MapItem>(true);
                        if (slotItem?.node?.data != null)
                        {
                            uiId = GetData(slotItem.node, "Id");
                            uiNodeId = GetData(slotItem.node, "NodeId");
                            uiType = slotItem.node.type ?? GetData(slotItem.node, "Type");
                            uiNote = GetData(slotItem.node, "Note");
                            uiName = GetData(slotItem.node, "Name");
                        }
                    }

                    bool filled = backendFilled || (slotItem != null && slotItem.node?.data != null);
                    slotObj["filled"] = filled;

                    if (filled)
                    {
                        var useId = backendId ?? uiId;
                        var useNodeId = backendNodeId ?? uiNodeId;
                        string nodeType = uiType;
                        string nodeNote = uiNote;
                        string nodeName = uiName;

                        // Look up missing info from node lookup
                        if ((string.IsNullOrEmpty(nodeType) || string.IsNullOrEmpty(nodeName))
                            && !string.IsNullOrEmpty(useNodeId)
                            && nodeInfoLookup.TryGetValue(useNodeId, out var infoNode))
                        {
                            if (string.IsNullOrEmpty(nodeType))
                                nodeType = infoNode.type ?? GetData(infoNode, "Type");
                            if (string.IsNullOrEmpty(nodeNote))
                                nodeNote = GetData(infoNode, "Note");
                            if (string.IsNullOrEmpty(nodeName))
                                nodeName = GetData(infoNode, "Name");
                        }

                        slotObj["node"] = new JObject
                        {
                            ["id"] = useId ?? "",
                            ["nodeId"] = useNodeId ?? "",
                            ["type"] = nodeType ?? "",
                            ["note"] = nodeNote ?? "",
                            ["name"] = nodeName ?? ""
                        };
                    }

                    slots.Add(slotObj);
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
        public string Description => "将可选节点放置到指定槽位。支持批量操作。使用 nodeId（稳定 ID，从 map_select_state 的 selectableNodes 中获得）。后端驱动，直接操作 MapManager 数据而非 UI。";
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

                var mapManager = MapManager.Instance;
                if (mapManager == null)
                {
                    result["result"] = "error";
                    result["message"] = "MapManager 不可用";
                    return (JToken)result;
                }

                var mapTree = mapManager.MapTree;

                // Build lookup: NodeId → MapTree.Node from selectable list
                var selectableLookup = new Dictionary<string, MapTree.Node>();
                if (mapTree?.SelectNode != null)
                {
                    foreach (var n in mapTree.SelectNode)
                    {
                        if (n?.data == null) continue;
                        var nid = GetData(n, "NodeId");
                        if (!string.IsNullOrEmpty(nid) && !selectableLookup.ContainsKey(nid))
                            selectableLookup[nid] = n;
                    }
                }

                // Only allow single placement per call
                if (mappings.Count != 1)
                {
                    result["result"] = "error";
                    result["message"] = "每次只能放置 1 个节点，请传入 1 组 slotIndex/nodeId";
                    return (JToken)result;
                }

                var placed = new JArray();
                var errors = new JArray();

                foreach (var (slotIndex, nodeId) in mappings)
                {
                    if (!selectableLookup.TryGetValue(nodeId, out var sourceNode))
                    {
                        errors.Add(new JObject
                        {
                            ["slotIndex"] = slotIndex,
                            ["nodeId"] = nodeId,
                            ["error"] = "未找到此 nodeId 的可选节点（可能已被放置或不存在）"
                        });
                        continue;
                    }

                    if (slotIndex < 0 || slotIndex >= 6)
                    {
                        errors.Add(new JObject
                        {
                            ["slotIndex"] = slotIndex,
                            ["nodeId"] = nodeId,
                            ["error"] = $"槽位索引 {slotIndex} 超出范围"
                        });
                        continue;
                    }

                    placed.Add(new JObject
                    {
                        ["slotIndex"] = slotIndex,
                        ["nodeId"] = nodeId
                    });
                }

                if (placed.Count > 0)
                {
                    // Only support single placement — batch is removed.
                    // Take the first (and only) mapping.
                    var (slotIndex, nodeId) = mappings[0];

                    var selectContainer = mapUI.transform.Find("MapSelect");
                    var nodeContent = mapUI.transform.Find("Map/NodeContent");
                    if (selectContainer == null || nodeContent == null
                        || slotIndex >= nodeContent.childCount)
                    {
                        result["result"] = "error";
                        result["message"] = "找不到手牌容器或目标槽位";
                        return (JToken)result;
                    }

                    // Find the hand card
                    Transform handCard = null;
                    foreach (Transform child in selectContainer)
                    {
                        var item = child.GetComponent<MapItem>();
                        if (item == null || item.node?.data == null) continue;
                        if (GetData(item.node, "NodeId") == nodeId)
                        {
                            handCard = child;
                            break;
                        }
                    }
                    if (handCard == null)
                    {
                        result["result"] = "error";
                        result["message"] = $"在手牌中未找到 NodeId={nodeId} 的卡片";
                        return (JToken)result;
                    }

                    var targetContent = nodeContent.GetChild(slotIndex).Find("Content");
                    if (targetContent == null)
                    {
                        result["result"] = "error";
                        result["message"] = "目标槽位没有 Content 子对象";
                        return (JToken)result;
                    }

                    var mapItem = handCard.GetComponent<MapItem>();
                    if (mapItem == null)
                    {
                        result["result"] = "error";
                        result["message"] = "卡片缺少 MapItem 组件";
                        return (JToken)result;
                    }

                    // Clear existing card in slot (if overwriting)
                    var existingItem = targetContent.GetComponentInChildren<MapItem>(true);
                    if (existingItem != null)
                        GameObject.Destroy(existingItem.gameObject);

                    // Save map instance ID for backend update
                    var mapId = GetData(mapItem.node, "Id");
                    placed[0]["mapId"] = mapId;

                    // === Let the game's own RemoveFromParent handle Null activation ===
                    // Set lastParent to selectContainer so RemoveFromParent finds
                    // the last Null child and activates it naturally.
                    mapItem.lastParent = selectContainer;
                    mapItem.lastPos = handCard.localPosition;

                    // === SetParent triggers OnTransformParentChanged ===
                    // RemoveFromParent: activates Null in hand (deferred via UniTask)
                    // AddToParent: deactivates Null in slot, positions card
                    handCard.SetParent(targetContent, false);

                    // Match game's RayCheck final state after first placement
                    mapItem.hasSelected = true;
                    mapItem.initAngle = Vector3.zero;
                    float cardScale = mapItem.initScale;
                    handCard.localScale = new Vector3(cardScale, cardScale, cardScale);
                    var objGroup = handCard.GetComponent<ObjectGroup>();
                    if (objGroup != null) objGroup.blocksRaycasts = true;
                    var sortingGroup = handCard.GetComponent<SortingGroup>();
                    if (sortingGroup != null) sortingGroup.sortingOrder = -20;

                    // Game calls SetNodes() AFTER placement
                    try { mapUI.SetNodes(); } catch { }

                    // Game defers UpdateCardItemPos to next frame via UniTask
                    UniTask.WaitForEndOfFrame().ContinueWith(() =>
                    {
                        try { mapUI.UpdateCardItemPos(); } catch { }
                    }).Forget();

                    result["movedCount"] = 1;
                    result["nullActivatedCount"] = 1; // handled by RemoveFromParent
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

        private static string GetData(MapTree.Node node, string key)
        {
            if (node?.data == null) return "";
            return node.data.TryGetValue(key, out var val) ? val ?? "" : "";
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
                    result["message"] = "找不到目标槽位";
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

                // Find the card in the slot
                var slotCard = targetContent.GetComponentInChildren<MapItem>(true);
                if (slotCard == null)
                {
                    result["result"] = "error";
                    result["message"] = "槽位中没有卡片";
                    return (JToken)result;
                }

                var selectContainer = mapUI.transform.Find("MapSelect");
                if (selectContainer == null)
                {
                    result["result"] = "error";
                    result["message"] = "找不到手牌容器";
                    return (JToken)result;
                }

                // === Physically return card to hand, matching game's RayCheck ===
                // Set lastParent to slot Content so RemoveFromParent activates Null in slot
                slotCard.lastParent = targetContent;
                slotCard.lastPos = slotCard.transform.localPosition;
                slotCard.transform.SetParent(selectContainer, false);

                // === Game's flow after parent changed to MapSelect ===
                mapUI.SetNodes();

                // hasSelected = true → returning to hand branch
                slotCard.hasSelected = false;
                var rt = slotCard.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                slotCard.transform.localScale = new Vector3(slotCard.initScale, slotCard.initScale, slotCard.initScale);
                var objGroup = slotCard.GetComponent<ObjectGroup>();
                if (objGroup != null) objGroup.blocksRaycasts = true;

                // Defer UpdateCardItemPos to next frame (matching game)
                UniTask.WaitForEndOfFrame().ContinueWith(() =>
                {
                    try { mapUI.UpdateCardItemPos(); } catch { }
                }).Forget();

                result["result"] = "success";
                result["message"] = $"已清空槽位 {slotIndex}";

                return (JToken)result;
            });
        }

        private static string GetData(MapTree.Node node, string key)
        {
            if (node?.data == null) return "";
            return node.data.TryGetValue(key, out var val) ? val ?? "" : "";
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
