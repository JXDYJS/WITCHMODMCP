using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Save;
using Newtonsoft.Json.Linq;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class ListGameModesTool : IMcpTool
    {
        public string Name => "list_game_modes";
        public string Description => "列出所有可用游戏模式（包括Mod注册的额外模式）及每个模式的存档情况。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
        };

        public async Task<JToken> Execute(JToken args)
        {
            return await GameDispatcher.RunOnMainThread(() =>
            {
                var modes = new JArray();

                var allSaves = Singleton<GameRuntimeData>.Instance?.Saves;
                var extraModes = new HashSet<string>();

                if (allSaves != null)
                {
                    foreach (var s in allSaves)
                    {
                        if (!string.IsNullOrEmpty(s.modeType))
                            extraModes.Add(s.modeType);
                    }
                }

                var knownModes = ModeChoiceUI.beforeSave.Keys.ToList();
                foreach (var m in knownModes) extraModes.Add(m);

                foreach (var modeType in extraModes.OrderBy(x => x))
                {
                    var entry = new JObject
                    {
                        ["mode"] = modeType
                    };

                    var modeSaves = allSaves?
                        .Where(s => s.modeType == modeType && ModeChoiceUI.CheckSave(s))
                        .ToList();

                    bool hasValidSave = modeSaves != null && modeSaves.Count > 0;
                    entry["hasSave"] = hasValidSave;

                    if (hasValidSave && modeSaves.Count > 0)
                    {
                        var best = modeSaves.OrderByDescending(s => s.Level).First();
                        var saveInfo = new JObject();
                        try { saveInfo["name"] = best.Name; } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[ListGameModesTool] name: {ex.Message}"); }
                        try { saveInfo["level"] = best.Level; } catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[ListGameModesTool] level: {ex.Message}"); }
                        try
                        {
                            var rt = best.roleTable?.FirstOrDefault().Value;
                            if (rt != null)
                            {
                                if (rt.Career?.data != null && rt.Career.data.ContainsKey("Id"))
                                    saveInfo["career"] = rt.Career.data["Id"];
                                saveInfo["cardCount"] = rt.cardList?.Count ?? 0;
                                saveInfo["relicCount"] = rt.relicList?.Count ?? 0;
                            }
                }
                catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[ListGameModesTool] save info: {ex.Message}"); }
                        entry["save"] = saveInfo;
                        entry["saveCount"] = modeSaves.Count;
                    }
                    else
                    {
                        entry["saveCount"] = 0;
                    }

                    modes.Add(entry);
                }

                return (JToken)new JObject
                {
                    ["modes"] = modes
                };
            });
        }
    }
}
