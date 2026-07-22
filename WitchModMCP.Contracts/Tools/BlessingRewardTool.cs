using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using Witch;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class GetBlessingStateTool : IMcpTool
    {
        public string Name => "get_blessing_state";
        public string Description => "获取祝福选择(BlessingChoiceGenerator)当前状态：三个祝福选项的详细信息，包括每项包含的祝福ID、名称、稀有度、描述等。";
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

                var blessObj = GameObject.Find("BlessChoice(Clone)");
                if (blessObj == null || !blessObj.activeInHierarchy)
                {
                    result["isOpen"] = false;
                    result["message"] = "当前没有 BlessChoice 界面";
                    return (JToken)result;
                }

                result["isOpen"] = true;
                result["isHighTide"] = false;
                try { result["isHighTide"] = RoleTable.Instance?.InHighTide ?? false; } catch { }

                var listRoot = blessObj.transform.Find("Window Manager/Windows/牌堆/Content/List View Custom/List");
                if (listRoot == null)
                {
                    result["message"] = "找不到祝福列表容器";
                    return (JToken)result;
                }

                // Build blessing lookup from game config table (covers ALL blessings)
                var blessLookup = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var allBlessLines = Singleton<GameConfigManager>.Instance.GetTable(DataType.Bless)?.Getlines();
                    if (allBlessLines != null)
                    {
                        foreach (var line in allBlessLines)
                        {
                            if (line == null) continue;
                            var id = line.TryGetValue("Id", out var idv) ? idv : "";
                            var name = line.TryGetValue("Name", out var nv) ? nv : "";
                            if (!string.IsNullOrEmpty(id))
                            {
                                blessLookup[id.ToLower()] = line;
                                if (!string.IsNullOrEmpty(name) && !blessLookup.ContainsKey(name))
                                    blessLookup[name] = line;
                            }
                        }
                    }
                }
                catch { }

                var choices = new JArray();
                for (int i = 1; i <= 3; i++)
                {
                    var choiceObj = listRoot.Find("Blessing" + i);
                    if (choiceObj == null) continue;

                    var choiceInfo = new JObject();
                    choiceInfo["index"] = i - 1;
                    choiceInfo["name"] = choiceObj.name;

                    // Read PointUse.DesList (combined description)
                    var pointUse = choiceObj.GetComponent<PointUse>();
                    if (pointUse != null)
                    {
                        choiceInfo["description"] = pointUse.DesList ?? "";
                        choiceInfo["rewardType"] = pointUse.RewardType ?? "";
                    }

                    // Read KeywordDisplay title
                    var kwDisplay = choiceObj.GetComponent<KeywordDisplay>();
                    if (kwDisplay != null)
                    {
                        try
                        {
                            var titleProp = kwDisplay.GetType().GetProperty("title");
                            if (titleProp != null)
                                choiceInfo["title"] = (string)titleProp.GetValue(kwDisplay, null) ?? "";
                        }
                        catch { }
                    }

                    // Scan children of BlessingList for skill and var blessing items
                    var blessList = choiceObj.transform.Find("BlessingList");
                    var skillItems = new JArray();
                    var varItems = new JArray();

                    if (blessList != null)
                    {
                        foreach (Transform child in blessList)
                        {
                            if (child == null || !child.gameObject.activeInHierarchy) continue;
                            var childName = child.name;
                            if (childName == "SampleItem" || childName == "TextItem" || childName == "Null")
                                continue;

                            var itemInfo = new JObject();
                            itemInfo["name"] = childName;

                            // Read text from text/text
                            try
                            {
                                var textTf = child.Find("text/text");
                                if (textTf != null)
                                {
                                    var tmp = textTf.GetComponent("TMP_Text");
                                    if (tmp != null)
                                    {
                                        var textProp = tmp.GetType().GetProperty("text");
                                        if (textProp != null)
                                            itemInfo["text"] = (string)textProp.GetValue(tmp, null) ?? "";
                                    }
                                }
                            }
                            catch { }

                            // Read icon path from Icon/Icon Image sprite
                            try
                            {
                                var iconTf = child.Find("Icon/Icon");
                                if (iconTf != null)
                                {
                                    var img = iconTf.GetComponent<Image>();
                                    if (img != null && img.sprite != null)
                                        itemInfo["icon"] = img.sprite.name;
                                }
                            }
                            catch { }

                            // Try to match text to a blessing from game config table
                            var itemText = itemInfo["text"]?.Value<string>() ?? "";
                            if (!string.IsNullOrEmpty(itemText))
                            {
                                // Extract blessing name (text before the first colon)
                                var colonIdx = itemText.IndexOf(':');
                                var rawName = colonIdx > 0 ? itemText.Substring(0, colonIdx).Trim() : itemText.Trim();
                                // Strip HTML color tags for matching
                                rawName = System.Text.RegularExpressions.Regex.Replace(rawName, "<[^>]+>", "").Trim();

                                // Try matching by exact name
                                if (!string.IsNullOrEmpty(rawName) && blessLookup.TryGetValue(rawName, out var blessLine))
                                {
                                    itemInfo["blessId"] = blessLine.TryGetValue("Id", out var id) ? id : "";
                                    itemInfo["blessName"] = blessLine.TryGetValue("Name", out var bn) ? bn : "";
                                    itemInfo["rarity"] = blessLine.TryGetValue("Rarity", out var r) ? r : "";
                                    itemInfo["source"] = blessLine.TryGetValue("Source", out var s) ? s : "";
                                    itemInfo["description"] = blessLine.TryGetValue("Description", out var d) ? d : "";
                                }
                                else
                                {
                                    // Try fuzzy match: find blessing whose name appears at start of itemText
                                    foreach (var kv in blessLookup)
                                    {
                                        if (itemText.StartsWith(kv.Key) || itemText.StartsWith(kv.Key + ":"))
                                        {
                                            var line = kv.Value;
                                            itemInfo["blessId"] = line.TryGetValue("Id", out var id) ? id : "";
                                            itemInfo["blessName"] = line.TryGetValue("Name", out var bn) ? bn : "";
                                            itemInfo["rarity"] = line.TryGetValue("Rarity", out var r) ? r : "";
                                            itemInfo["source"] = line.TryGetValue("Source", out var s) ? s : "";
                                            itemInfo["description"] = line.TryGetValue("Description", out var d) ? d : "";
                                            break;
                                        }
                                    }
                                }
                            }

                            // Determine type: SampleItem clones are skill, TextItem clones are var
                            if (child.name.Contains("SampleItem") || child.Find("Icon") != null)
                                skillItems.Add(itemInfo);
                            else
                                varItems.Add(itemInfo);
                        }
                    }

                    choiceInfo["skillBlessings"] = skillItems;
                    choiceInfo["varBlessings"] = varItems;
                    choiceInfo["hasSkillBlessings"] = skillItems.Count > 0;
                    choiceInfo["hasVarBlessings"] = varItems.Count > 0;

                    // Also try to get button instanceId from scan_ui compatible info
                    var btnTransform = choiceObj.Find("button");
                    if (btnTransform != null)
                    {
                        choiceInfo["buttonPath"] = "Canvas/BattleRewardsUI/BlessChoice(Clone)/Window Manager/Windows/牌堆/Content/List View Custom/List/" + choiceObj.name + "/button";
                    }

                    choices.Add(choiceInfo);
                }

                result["choices"] = choices;
                result["choiceCount"] = choices.Count;
                result["message"] = $"找到 {choices.Count} 个祝福选项";

                return (JToken)result;
            });
        }
    }

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

                var blessObj = GameObject.Find("BlessChoice(Clone)");
                if (blessObj == null || !blessObj.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "当前没有 BlessChoice 界面";
                    return (JToken)result;
                }

                var listRoot = blessObj.transform.Find("Window Manager/Windows/牌堆/Content/List View Custom/List");
                if (listRoot == null)
                {
                    result["result"] = "error";
                    result["message"] = "找不到祝福列表容器";
                    return (JToken)result;
                }

                var choiceName = "Blessing" + (index.Value + 1);
                var choiceObj = listRoot.Find(choiceName);
                if (choiceObj == null)
                {
                    result["result"] = "error";
                    result["message"] = $"找不到选项 {choiceName}";
                    result["totalChoices"] = 3;
                    return (JToken)result;
                }

                var btnTransform = choiceObj.Find("button");
                if (btnTransform == null)
                {
                    result["result"] = "error";
                    result["message"] = "找不到按钮组件";
                    return (JToken)result;
                }

                var btnManager = btnTransform.GetComponent("ButtonManager");
                if (btnManager == null)
                {
                    result["result"] = "error";
                    result["message"] = "找不到 ButtonManager 组件";
                    return (JToken)result;
                }

                try
                {
                    var onClickField = btnManager.GetType().GetField("onClick", BindingFlags.Public | BindingFlags.Instance);
                    if (onClickField?.GetValue(btnManager) is UnityEngine.Events.UnityEvent onClick)
                    {
                        onClick.Invoke();
                        result["result"] = "success";
                        result["message"] = $"已选择祝福选项 {index.Value}";
                        result["choiceIndex"] = index.Value;
                    }
                    else
                    {
                        result["result"] = "error";
                        result["message"] = "ButtonManager 没有 onClick 事件";
                    }
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"点击祝福选项失败: {ex.Message}";
                }

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

                var blessObj = GameObject.Find("BlessChoice(Clone)");
                if (blessObj != null && blessObj.activeInHierarchy)
                {
                    UnityEngine.Object.Destroy(blessObj);

                    // Also try to re-show the BattleRewardsUI window manager
                    try
                    {
                        var rewardsUI = UIManager.Instance?.GetUI<BattleRewardsUI>("BattleRewardsUI");
                        if (rewardsUI != null)
                        {
                            var wm = rewardsUI.transform.Find("Window Manager");
                            if (wm != null) wm.gameObject.SetActive(true);
                        }
                    }
                    catch { }

                    result["result"] = "success";
                    result["message"] = "已跳过祝福奖励";
                }
                else
                {
                    result["result"] = "no_blessing";
                    result["message"] = "当前没有 BlessChoice 界面";
                }

                return (JToken)result;
            });
        }
    }
}
