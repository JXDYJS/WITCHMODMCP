using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class SceneTreeTool : IMcpTool
    {
        public string Name => "get_scene_tree";
        public string Description => "获取当前场景的 GameObject 层级树，包括名称、active状态、组件列表。可选指定根节点名字来只看子树。用于 Mod 开发者排查场景问题。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["rootName"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "根节点名称过滤。只返回以此名字命名的 GameObject 及其子树。留空返回完整场景树"
                },
                ["maxDepth"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "最大递归深度，默认 10",
                    ["default"] = 10
                },
                ["maxChildren"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "每层最多显示的子节点数，默认 50",
                    ["default"] = 50
                },
                ["includeComponents"] = new JObject
                {
                    ["type"] = "boolean",
                    ["description"] = "是否包含组件列表，默认 true",
                    ["default"] = true
                },
                ["includeInactive"] = new JObject
                {
                    ["type"] = "boolean",
                    ["description"] = "是否包含非 active 对象，默认 false",
                    ["default"] = false
                }
            }
        };

        public Task<JToken> Execute(JToken args)
        {
            string rootName = args?["rootName"]?.Value<string>();
            int maxDepth = args?["maxDepth"]?.Value<int>() ?? 10;
            int maxChildren = args?["maxChildren"]?.Value<int>() ?? 50;
            bool includeComponents = args?["includeComponents"]?.Value<bool>() ?? true;
            bool includeInactive = args?["includeInactive"]?.Value<bool>() ?? false;

            return GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                try
                {
                    Scene scene = SceneManager.GetActiveScene();
                    result["sceneName"] = scene.name;
                    result["scenePath"] = scene.path;
                    result["sceneBuildIndex"] = scene.buildIndex;

                    var rootObjects = scene.GetRootGameObjects();
                    var hierarchy = new JArray();

                    bool foundRoot = false;

                    if (!string.IsNullOrEmpty(rootName))
                    {
                        foreach (var root in rootObjects)
                        {
                            if (root.name == rootName)
                            {
                                hierarchy.Add(BuildNode(root, 0, maxDepth, maxChildren, includeComponents, includeInactive));
                                foundRoot = true;
                                break;
                            }

                            var found = root.transform.Find(rootName);
                            if (found != null)
                            {
                                hierarchy.Add(BuildNode(found.gameObject, 0, maxDepth, maxChildren, includeComponents, includeInactive));
                                foundRoot = true;
                                break;
                            }
                        }

                        if (!foundRoot)
                        {
                            result["warning"] = $"没有找到名为 '{rootName}' 的根对象，返回完整场景树";
                            foreach (var root in rootObjects)
                                hierarchy.Add(BuildNode(root, 0, maxDepth, maxChildren, includeComponents, includeInactive));
                        }
                    }
                    else
                    {
                        foreach (var root in rootObjects)
                            hierarchy.Add(BuildNode(root, 0, maxDepth, maxChildren, includeComponents, includeInactive));
                    }

                    result["rootCount"] = rootObjects.Length;
                    result["hierarchy"] = hierarchy;
                }
                catch (Exception ex)
                {
                    result["error"] = $"获取场景树失败: {ex.Message}";

                    try
                    {
                        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                        var sceneObjects = allObjects.Where(go =>
                            go != null && go.hideFlags == HideFlags.None && go.scene.IsValid()).ToList();

                        result["fallbackCount"] = sceneObjects.Count;
                        var names = new JArray();
                        foreach (var go in sceneObjects.OrderBy(x => x.name).Take(100))
                            names.Add(go.name);
                        result["fallbackObjects"] = names;
                    }
                    catch { }
                }

                return (JToken)result;
            });
        }

        private static JObject BuildNode(GameObject go, int depth, int maxDepth, int maxChildren,
            bool includeComponents, bool includeInactive)
        {
            var node = new JObject
            {
                ["name"] = go.name,
                ["activeSelf"] = go.activeSelf,
                ["activeInHierarchy"] = go.activeInHierarchy,
                ["tag"] = go.tag,
                ["layer"] = LayerMask.LayerToName(go.layer),
                ["layerIndex"] = go.layer,
                ["instanceId"] = go.GetInstanceID()
            };

            if (includeComponents)
            {
                var comps = new JArray();
                foreach (var comp in go.GetComponents<UnityEngine.Component>())
                {
                    if (comp == null) continue;
                    comps.Add(comp.GetType().Name);
                }
                node["components"] = comps;
            }

            var transform = go.transform;
            var pos = transform.localPosition;
            node["localPosition"] = new JObject
            {
                ["x"] = Math.Round(pos.x, 3),
                ["y"] = Math.Round(pos.y, 3),
                ["z"] = Math.Round(pos.z, 3)
            };

            int childCount = transform.childCount;
            node["childCount"] = childCount;

            if (childCount > 0 && depth < maxDepth)
            {
                var children = new JArray();
                int displayed = 0;

                for (int i = 0; i < childCount && displayed < maxChildren; i++)
                {
                    var child = transform.GetChild(i).gameObject;
                    if (!includeInactive && !child.activeSelf && !child.activeInHierarchy)
                        continue;

                    children.Add(BuildNode(child, depth + 1, maxDepth, maxChildren, includeComponents, includeInactive));
                    displayed++;
                }

                node["children"] = children;

                if (displayed == 0 && childCount > 0)
                    node["childrenNote"] = $"{childCount} 个子对象全部未激活（includeInactive=false）";
                else if (childCount > maxChildren)
                    node["childrenTruncated"] = $"显示 {displayed}/{childCount}";
            }

            return node;
        }
    }
}
