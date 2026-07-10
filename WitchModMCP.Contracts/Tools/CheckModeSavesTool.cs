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
    public class CheckModeSavesTool : IMcpTool
    {
        public string Name => "check_mode_saves";
        public string Description => "检查指定游戏模式的存档详情。不传 mode 则返回所有模式的存档。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["mode"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "游戏模式，如 Normal / Sublimation / Slot / Teach / Story。不传则返回所有"
                }
            }
        };

        public async Task<JToken> Execute(JToken args)
        {
            string mode = args?["mode"]?.Value<string>();

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                var allSaves = Singleton<GameRuntimeData>.Instance?.Saves;
                if (allSaves == null || allSaves.Count == 0)
                {
                    result["hasSaves"] = false;
                    result["saves"] = new JArray();
                    return (JToken)result;
                }

                var filtered = string.IsNullOrEmpty(mode)
                    ? allSaves
                    : allSaves.Where(s => s.modeType == mode).ToList();

                var valid = filtered.Where(ModeChoiceUI.CheckSave).ToList();

                result["mode"] = mode ?? "all";
                result["hasSaves"] = valid.Count > 0;
                result["totalSaves"] = filtered.Count;
                result["validSaves"] = valid.Count;

                var savesArr = new JArray();
                foreach (var s in valid)
                {
                    var entry = new JObject();
                    try { entry["name"] = s.Name; } catch { }
                    try { entry["mode"] = s.modeType; } catch { }
                    try { entry["level"] = s.Level; } catch { }
                    try { entry["createdTime"] = s.CreatedTime; } catch { }
                    try { entry["seed"] = s.Seed; } catch { }
                    try { entry["hardLevel"] = s.HardLevel; } catch { }
                    try
                    {
                        var rt = s.roleTable?.FirstOrDefault().Value;
                        if (rt != null)
                        {
                            if (rt.Career?.data != null && rt.Career.data.ContainsKey("Id"))
                                entry["career"] = rt.Career.data["Id"];
                            entry["cardCount"] = rt.cardList?.Count ?? 0;
                            entry["relicCount"] = rt.relicList?.Count ?? 0;
                        }
                    }
                    catch { }
                    savesArr.Add(entry);
                }

                result["saves"] = savesArr;
                return (JToken)result;
            });
        }
    }
}
