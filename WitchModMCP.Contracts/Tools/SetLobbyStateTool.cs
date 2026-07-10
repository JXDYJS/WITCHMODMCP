using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class SetLobbyStateTool : IMcpTool
    {
        public string Name => "set_lobby_state";
        public string Description => "修改职业选择大厅的配置：职业、随从、属性加点、启用的卡包。每个字段可选，不传则不修改。仅在 LOBBY 页面有效。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["careerId"] = new JObject { ["type"] = "string", ["description"] = "职业 ID，如 Career_1" },
                ["partnerId"] = new JObject { ["type"] = "string", ["description"] = "随从 ID，如 Partner_2" },
                ["attributes"] = new JObject
                {
                    ["type"] = "object",
                    ["description"] = "属性加点，如 {\"main\": \"Strength\", \"second\": \"Wisdom\"}",
                    ["properties"] = new JObject
                    {
                        ["main"] = new JObject { ["type"] = "string" },
                        ["second"] = new JObject { ["type"] = "string" }
                    }
                },
                ["cardPackIds"] = new JObject
                {
                    ["type"] = "array",
                    ["description"] = "启用的卡包 ID 列表，如 [\"cardpack_1\", \"cardpack_5\"]（必须 ≥6 个）",
                    ["items"] = new JObject { ["type"] = "string" }
                }
            }
        };

        public async Task<JToken> Execute(JToken args)
        {
            string careerId = args?["careerId"]?.Value<string>();
            string partnerId = args?["partnerId"]?.Value<string>();
            JToken attrToken = args?["attributes"];
            JArray packIds = args?["cardPackIds"] as JArray;

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();
                var changes = new JArray();

                bool inLobby = UIManager.Instance?.GetUI<GameEntryUI>("GameEntryUI") != null;
                if (!inLobby)
                {
                    result["result"] = "error";
                    result["message"] = "当前不在职业选择大厅中，请先调用 start_new_game";
                    return (JToken)result;
                }

                var ge = UIManager.Instance.GetUI<GameEntryUI>("GameEntryUI");

                // --- 切换职业 ---
                if (!string.IsNullOrEmpty(careerId))
                {
                    var careerRow = Singleton<GameConfigManager>.Instance.GetOne(DataType.Career, careerId);
                    if (careerRow == null)
                    {
                        result["result"] = "error";
                        result["message"] = $"职业 ID '{careerId}' 不存在";
                        return (JToken)result;
                    }

                    var oldCareer = GameEntryUI.career?.data?.ContainsKey("Id") == true
                        ? GameEntryUI.career.data["Id"] : null;

                    if (oldCareer != careerId)
                    {
                        GameEntryUI.career = new DataConfig(careerId, DataType.Career);
                        if (RoleTable.Instance != null)
                            RoleTable.Instance.Career = GameEntryUI.career;
                        changes.Add($"career: {oldCareer ?? "null"} -> {careerId}");
                    }
                }

                // --- 切换随从 ---
                if (!string.IsNullOrEmpty(partnerId))
                {
                    var partnerRow = Singleton<GameConfigManager>.Instance.GetOne(DataType.Partner, partnerId);
                    if (partnerRow == null)
                    {
                        result["result"] = "error";
                        result["message"] = $"随从 ID '{partnerId}' 不存在";
                        return (JToken)result;
                    }

                    var oldPartner = GameEntryUI.partner?.data?.ContainsKey("Id") == true
                        ? GameEntryUI.partner.data["Id"] : null;

                    if (oldPartner != partnerId)
                    {
                        GameEntryUI.partner = new DataConfig(partnerId, DataType.Partner);
                        changes.Add($"partner: {oldPartner ?? "null"} -> {partnerId}");
                    }
                }

                // --- 切换属性加点 ---
                if (attrToken != null)
                {
                    string mainVar = attrToken["main"]?.Value<string>();
                    string secondVar = attrToken["second"]?.Value<string>();

                    var validVars = new HashSet<string> { "Strength", "Lucky", "Perceive", "Wisdom" };

                    if (!string.IsNullOrEmpty(mainVar) && validVars.Contains(mainVar))
                    {
                        try { ge.SelectVar(mainVar, "Main"); changes.Add($"mainVar: -> {mainVar}"); }
                        catch (Exception ex) { changes.Add($"mainVar error: {ex.Message}"); }
                    }

                    if (!string.IsNullOrEmpty(secondVar) && validVars.Contains(secondVar))
                    {
                        try { ge.SelectVar(secondVar, "Second"); changes.Add($"secondVar: -> {secondVar}"); }
                        catch (Exception ex) { changes.Add($"secondVar error: {ex.Message}"); }
                    }
                }

                // --- 切换卡包 ---
                if (packIds != null)
                {
                    var idList = packIds.Select(p => p.Value<string>()).Where(x => !string.IsNullOrEmpty(x)).ToList();
                    if (idList.Count < 6)
                    {
                        result["result"] = "error";
                        result["message"] = "启用的卡包数量不能少于 6 个";
                        return (JToken)result;
                    }

                    var validIds = new HashSet<string>();
                    var allPacks = Singleton<GameConfigManager>.Instance.GetTable(DataType.CardPack).Getlines();
                    foreach (var row in allPacks)
                    {
                        if (!Singleton<GameRuntimeData>.Instance.IsLocked(row["Id"]))
                            validIds.Add(row["Id"]);
                    }

                    var unknown = idList.Where(id => !validIds.Contains(id)).ToList();
                    if (unknown.Count > 0)
                    {
                        result["result"] = "error";
                        result["message"] = $"以下卡包 ID 不存在或已锁定：{string.Join(", ", unknown)}";
                        return (JToken)result;
                    }

                    var rt = Singleton<GameRuntimeData>.Instance;
                    if (rt != null)
                    {
                        rt.UseCardPack = new HashSet<string>(idList);
                        changes.Add($"cardPacks: [{string.Join(", ", idList)}]");
                    }
                }

                result["result"] = "success";
                result["changes"] = changes;
                return (JToken)result;
            });
        }
    }
}
