using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class RaycastTool : IMcpTool
    {
        public string Name => "raycast_mouse";
        public string Description => "从鼠标位置发射射线，返回被击中的所有 GameObject（含 UI Canvas 元素和 3D/2D 物理对象）。可用于确定鼠标当前悬停在哪个节点或预制件上。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["screenX"] = new JObject
                {
                    ["type"] = "number",
                    ["description"] = "屏幕 X 坐标（像素），不传则使用当前鼠标位置"
                },
                ["screenY"] = new JObject
                {
                    ["type"] = "number",
                    ["description"] = "屏幕 Y 坐标（像素），不传则使用当前鼠标位置"
                },
                ["maxResults"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "最多返回的命中结果数，默认 30",
                    ["default"] = 30
                }
            }
        };

        public async Task<JToken> Execute(JToken args)
        {
            float? overrideX = args?["screenX"]?.Value<float>();
            float? overrideY = args?["screenY"]?.Value<float>();
            int maxResults = args?["maxResults"]?.Value<int>() ?? 30;

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                try
                {
                    Vector2 mousePos = overrideX.HasValue && overrideY.HasValue
                        ? new Vector2(overrideX.Value, overrideY.Value)
                        : GetMousePosition();

                    result["screenPosition"] = new JObject
                    {
                        ["x"] = mousePos.x,
                        ["y"] = mousePos.y
                    };

                    var hits = new JArray();
                    int hitCount = 0;
                    var seen = new HashSet<int>();

                    // ---- 1. EventSystem RaycastAll (UI + Physics2D + Physics3D) ----
                    try
                    {
                        var es = EventSystem.current;
                        if (es != null)
                        {
                            var pointerData = new PointerEventData(es) { position = mousePos };
                            var results = new List<RaycastResult>();
                            es.RaycastAll(pointerData, results);

                            foreach (var r in results)
                            {
                                if (r.gameObject == null) continue;
                                int id = r.gameObject.GetInstanceID();
                                if (seen.Contains(id)) continue;
                                seen.Add(id);
                                hits.Add(BuildHit(r.gameObject, r.distance, r.depth, r.sortingOrder, GetSourceTypeName(r), mousePos));
                                hitCount++;
                                if (hitCount >= maxResults) break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result["eventSystemError"] = ex.Message;
                    }

                    // ---- 2. Raw Physics.RaycastAll (fallback/supplement for 3D) ----
                    try
                    {
                        var cam = Camera.main;
                        if (cam != null)
                        {
                            Ray ray = cam.ScreenPointToRay(mousePos);
                            var physicsHits = Physics.RaycastAll(ray, Mathf.Infinity);
                            foreach (var ph in physicsHits)
                            {
                                if (ph.collider == null) continue;
                                var go = ph.collider.gameObject;
                                if (go == null) continue;
                                int id = go.GetInstanceID();
                                if (seen.Contains(id)) continue;
                                seen.Add(id);
                                hits.Add(BuildHitFromPhysics(go, ph.distance, "physics3d", mousePos));
                                hitCount++;
                                if (hitCount >= maxResults) break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result["physics3dError"] = ex.Message;
                    }

                    // ---- 3. Raw Physics2D.GetRayIntersectionAll (fallback/supplement for 2D) ----
                    try
                    {
                        var cam = Camera.main;
                        if (cam != null)
                        {
                            Ray ray = cam.ScreenPointToRay(mousePos);
                            var physics2dHits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity);
                            foreach (var ph in physics2dHits)
                            {
                                if (ph.collider == null) continue;
                                var go = ph.collider.gameObject;
                                if (go == null) continue;
                                int id = go.GetInstanceID();
                                if (seen.Contains(id)) continue;
                                seen.Add(id);
                                hits.Add(BuildHitFromPhysics(go, ph.fraction, "physics2d", mousePos));
                                hitCount++;
                                if (hitCount >= maxResults) break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result["physics2dError"] = ex.Message;
                    }

                    result["hitCount"] = hitCount;
                    result["hits"] = hits;
                }
                catch (Exception ex)
                {
                    result["error"] = $"射线检测失败: {ex.Message}";
                }

                return (JToken)result;
            });
        }

        private static Vector2 GetMousePosition()
        {
            // Game uses new Input System. Try Mouse.current first; fall back to legacy.
            if (Mouse.current != null)
            {
                var pos = Mouse.current.position.ReadValue();
                // InputSystem returns screen-space coords with Y starting from bottom-left,
                // which matches what ScreenPointToRay / EventSystem expects.
                return pos;
            }
            // Fallback: legacy Input
            return Input.mousePosition;
        }

        private static string GetSourceTypeName(RaycastResult r)
        {
            if (r.module == null) return "unknown";
            var typeName = r.module.GetType().Name;
            if (typeName.Contains("GraphicRaycaster")) return "ui";
            if (typeName.Contains("Physics2DRaycaster")) return "physics2d";
            if (typeName.Contains("PhysicsRaycaster")) return "physics3d";
            return typeName;
        }

        private static JObject BuildHit(GameObject go, float distance, int depth,
            int sortingOrder, string sourceType, Vector2 screenPos)
        {
            var hit = new JObject();
            hit["gameObjectName"] = go.name;
            hit["hierarchyPath"] = GetHierarchyPath(go.transform);
            hit["instanceId"] = go.GetInstanceID();
            hit["source"] = sourceType;
            hit["distance"] = Math.Round(distance, 3);
            hit["depth"] = depth;
            hit["sortingOrder"] = sortingOrder;

            var rt = go.GetComponent<RectTransform>();
            hit["isUI"] = rt != null;
            var canvas = go.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                hit["isCanvas"] = true;
                hit["canvasName"] = canvas.name;
                hit["canvasRenderMode"] = canvas.renderMode.ToString();
            }
            else
            {
                hit["isCanvas"] = false;
            }

            var comps = new JArray();
            foreach (var comp in go.GetComponents<UnityEngine.Component>())
            {
                if (comp == null) continue;
                comps.Add(comp.GetType().Name);
            }
            hit["components"] = comps;
            hit["activeSelf"] = go.activeSelf;
            hit["activeInHierarchy"] = go.activeInHierarchy;

            return hit;
        }

        private static JObject BuildHitFromPhysics(GameObject go, float distance,
            string sourceType, Vector2 screenPos)
        {
            // same structure as BuildHit, but with default depth/sortingOrder
            return BuildHit(go, distance, 0, 0, sourceType, screenPos);
        }

        private static string GetHierarchyPath(Transform t)
        {
            var sb = new StringBuilder();
            var current = t;
            while (current != null)
            {
                if (sb.Length > 0) sb.Insert(0, "/");
                sb.Insert(0, current.name);
                current = current.parent;
            }
            return sb.ToString();
        }
    }
}
