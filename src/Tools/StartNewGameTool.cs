using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class StartNewGameTool : IMcpTool
    {
        public string Name => "start_new_game";
        public string Description => "从当前状态选择游戏模式并开始新游戏（进入职业选择大厅）。mode必填。useExistingSave为true且该模式有存档时，会继续老存档而非开新档。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["mode"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "游戏模式，如 Normal / Sublimation / Slot / Teach / Story"
                },
                ["useExistingSave"] = new JObject
                {
                    ["type"] = "boolean",
                    ["description"] = "如果有存档，是否使用已有存档继续（默认 false）"
                }
            },
            ["required"] = new JArray { "mode" }
        };

        private static bool IsUIActive_<T>(string name) where T : UIBase
        {
            var ui = UIManager.Instance?.GetUI<T>(name);
            return ui != null && ui.gameObject != null && ui.gameObject.activeInHierarchy;
        }

        private static string DetectPage()
        {
            if (IsUIActive_<MainMenuUI>("MainMenuUI")) return "MAIN_MENU";
            if (IsUIActive_<ModeChoiceUI>("ModeChoiceUI")) return "MODE_SELECT";
            if (IsUIActive_<GameEntryUI>("GameEntryUI")) return "LOBBY";
            if (FightManager.Instance != null && FightManager.Instance.fightType != FightType.None) return "FIGHT";
            if (GameObject.Find("Breaks") != null) return "BREAKS";
            if (IsUIActive_<MapSelectUI>("MapSelectUI")) return "MAP";
            if (GameApp.Instance?.HouseItem?.activeSelf == true) return "HUB";
            if (MapManager.Instance != null) return "MAP";
            return "UNKNOWN";
        }

        public async Task<JToken> Execute(JToken args)
        {
            string mode = args?["mode"]?.Value<string>();
            bool useExisting = args?["useExistingSave"]?.Value<bool>() ?? false;

            if (string.IsNullOrEmpty(mode))
                throw new ArgumentException("mode 参数不能为空");

            // Step 1: 在主线程检测当前页面并触发 ModeChoiceUI
            string initialPage = await GameDispatcher.RunOnMainThread(() =>
            {
                string page = DetectPage();
                if (page == "HUB" || page == "MODE_SELECT" || page == "MAIN_MENU")
                {
                    var mc = UIManager.Instance.ShowUI<ModeChoiceUI>("ModeChoiceUI");
                    mc.Init(startTutorialWhenFirstPlay: false);

                    if (useExisting && ModeChoiceUI.beforeSave.TryGetValue(mode, out var cachedSave)
                        && cachedSave != null && ModeChoiceUI.CheckSave(cachedSave))
                    {
                        mc.ReturnGame(mode);
                        return "RETURN_GAME";
                    }
                    else
                    {
                        mc.CreateNewSave(mode);
                        return "NEW_GAME";
                    }
                }
                if (page == "LOBBY") return "ALREADY_IN_LOBBY";
                return page;
            });

            if (initialPage != "NEW_GAME" && initialPage != "RETURN_GAME" && initialPage != "ALREADY_IN_LOBBY")
            {
                return new JObject
                {
                    ["result"] = "error",
                    ["message"] = $"当前页面状态为 {initialPage}，无法开始新游戏。请先回到小屋(HUB)后再试",
                    ["page"] = initialPage
                };
            }

            if (initialPage == "ALREADY_IN_LOBBY")
            {
                return new JObject
                {
                    ["result"] = "already_in_lobby",
                    ["message"] = "已经在职业选择大厅中，无需重复操作",
                    ["page"] = "LOBBY"
                };
            }

            // Step 2: 轮询等待 GameEntryUI 出现（最长 15s）
            bool enteredLobby = false;
            for (int i = 0; i < 75; i++)
            {
                await Task.Delay(200);

                enteredLobby = await GameDispatcher.RunOnMainThread(() =>
                {
                    return IsUIActive_<GameEntryUI>("GameEntryUI");
                });

                if (enteredLobby) break;
            }

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject
                {
                    ["mode"] = mode,
                    ["usedExisting"] = useExisting && initialPage == "RETURN_GAME"
                };

                if (enteredLobby)
                {
                    result["result"] = "success";
                    result["page"] = "LOBBY";
                    result["message"] = useExisting && initialPage == "RETURN_GAME"
                        ? $"已加载 {mode} 模式的存档，进入职业选择大厅"
                        : $"已创建 {mode} 模式的新存档，进入职业选择大厅";
                }
                else
                {
                    result["result"] = "timeout";
                    result["page"] = DetectPage();
                    result["message"] = "等待职业选择大厅超时。建议调用 get_scene_state 确认当前状态";
                }

                return (JToken)result;
            });
        }
    }
}
