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
    public class GetLobbyStateTool : IMcpTool
    {
        public string Name => "get_lobby_state";
        public string Description => "获取职业选择大厅（GameEntryUI）的当前配置：已选职业、随从、属性加点、卡包启用状态，以及所有可用的职业/随从/卡包列表。只有在 LOBBY 页面才有效。";
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

                var ge = UIManager.Instance?.GetUI<GameEntryUI>("GameEntryUI");
                bool inLobby = ge != null;
                result["inLobby"] = inLobby;

                if (!inLobby)
                {
                    result["message"] = "当前不在职业选择大厅中，请先调用 start_new_game";
                    return (JToken)result;
                }

                var career = GameEntryUI.career;
                var partner = GameEntryUI.partner;

                // --- 当前职业 ---
                if (career?.data != null)
                {
                    var c = new JObject();
                    foreach (var kv in career.data)
                        c[kv.Key] = kv.Value;
                    result["career"] = c;
                }
                else
                {
                    result["career"] = null;
                }

                // --- 当前随从 ---
                if (partner?.data != null)
                {
                    var p = new JObject();
                    foreach (var kv in partner.data)
                        p[kv.Key] = kv.Value;
                    result["partner"] = p;
                }
                else
                {
                    result["partner"] = null;
                }

                // --- 属性加点 ---
                var attrs = new JObject();
                var chosenVars = RoleTable.Instance?.ChooseVars;
                if (chosenVars != null && chosenVars.Count > 0)
                {
                    attrs["main"] = chosenVars[0];
                    attrs["second"] = chosenVars.Count > 1 ? chosenVars[1] : null;
                }
                else
                {
                    var mainParent = ge.MainParent;
                    var secondParent = ge.SecondParent;
                    if (mainParent != null)
                    {
                        foreach (Transform c in mainParent)
                        {
                            var sw = c.GetComponent<SwitchButton>();
                            if (sw != null && sw.isOn)
                                attrs["main"] = c.name;
                        }
                    }
                    if (secondParent != null)
                    {
                        foreach (Transform c in secondParent)
                        {
                            var sw = c.GetComponent<SwitchButton>();
                            if (sw != null && sw.isOn)
                                attrs["second"] = c.name;
                        }
                    }
                }
                result["attributes"] = attrs;

                // --- 卡包 ---
                var packResult = new JObject();
                try
                {
                    var rt = Singleton<GameRuntimeData>.Instance;
                    var activePackIds = rt?.UseCardPack ?? new HashSet<string>();
                    packResult["activeIds"] = new JArray(activePackIds);

                    var allPackRows = Singleton<GameConfigManager>.Instance
                        .GetTable(DataType.CardPack).Getlines()
                        .Where(x => !Singleton<GameRuntimeData>.Instance.IsLocked(x["Id"]));
                    
                    var avail = new JArray();
                    foreach (var row in allPackRows)
                    {
                        var entry = new JObject();
                        entry["id"] = row["Id"];
                        entry["type"] = row.ContainsKey("Type") ? row["Type"] : "Expansion";
                        if (row.ContainsKey("Name")) entry["name"] = row["Name"];
                        if (row.ContainsKey("Description")) entry["description"] = row["Description"];
                        if (row.ContainsKey("Icon")) entry["icon"] = row["Icon"];
                        entry["active"] = activePackIds.Contains(row["Id"]);

                        var packId = row["Id"];
                        try
                        {
                            var items = Singleton<GameConfigManager>.Instance.GetPackItems(packId);
                            if (items.TryGetValue(DataType.Card, out var cards))
                                entry["cardCount"] = cards.Count;
                            if (items.TryGetValue(DataType.Relic, out var relics))
                                entry["relicCount"] = relics.Count;
                            if (items.TryGetValue(DataType.Bless, out var blesses))
                                entry["blessCount"] = blesses.Count;
                        }
                        catch { }

                        avail.Add(entry);
                    }
                    packResult["available"] = avail;
                }
                catch (Exception ex)
                {
                    packResult["error"] = ex.Message;
                }
                result["cardPacks"] = packResult;

                // --- 可用职业 ---
                try
                {
                    var careerRows = Singleton<GameConfigManager>.Instance
                        .GetTable(DataType.Career).Getlines()
                        .Where(x => !Singleton<GameRuntimeData>.Instance.IsLocked(x["Id"]));
                    var carrArr = new JArray();
                    foreach (var row in careerRows)
                    {
                        var entry = new JObject();
                        foreach (var kv in row)
                        {
                            if (kv.Key == "Id" || kv.Key == "SanMax" || kv.Key == "Name")
                                entry[kv.Key] = kv.Value;
                        }
                        if (!entry.ContainsKey("Id")) entry["id"] = row["Id"];
                        carrArr.Add(entry);
                    }
                    result["availableCareers"] = carrArr;
                }
                catch (Exception ex)
                {
                    result["availableCareersError"] = ex.Message;
                }

                // --- 可用随从 ---
                try
                {
                    var partnerRows = Singleton<GameConfigManager>.Instance
                        .GetTable(DataType.Partner).Getlines()
                        .Where(x => !Singleton<GameRuntimeData>.Instance.IsLocked(x["Id"]));
                    var partArr = new JArray();
                    foreach (var row in partnerRows)
                    {
                        var entry = new JObject();
                        foreach (var kv in row)
                        {
                            if (kv.Key == "Id" || kv.Key == "Bless" || kv.Key == "Name" 
                                || kv.Key == "Attack" || kv.Key == "Defend" || kv.Key == "Hp"
                                || kv.Key == "CardList" || kv.Key == "ActionCount")
                                entry[kv.Key] = kv.Value;
                        }
                        if (!entry.ContainsKey("Id")) entry["id"] = row["Id"];
                        partArr.Add(entry);
                    }
                    result["availablePartners"] = partArr;
                }
                catch (Exception ex)
                {
                    result["availablePartnersError"] = ex.Message;
                }

                return (JToken)result;
            });
        }
    }
}
