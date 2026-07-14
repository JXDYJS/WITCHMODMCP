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
    public class EnterGameTool : IMcpTool
    {
        public string Name => "enter_game";
        public string Description => "从主菜单点击'开始游戏'进入游戏小屋（中枢场景）。如果已经进入游戏则直接返回成功。需要先有 get_scene_state 确认当前页面。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
        };

        private static bool IsUIActive<T>(string name) where T : UIBase
        {
            var ui = UIManager.Instance?.GetUI<T>(name);
            return ui != null && ui.gameObject != null && ui.gameObject.activeInHierarchy;
        }

        public async Task<JToken> Execute(JToken args)
        {
            // Step 1: 在主线程检查当前状态并触发开始游戏
            string initialPage = await GameDispatcher.RunOnMainThread(() =>
            {
                bool isMainMenu = UIManager.Instance?.GetUI<MainMenuUI>("MainMenuUI") != null
                    && UIManager.Instance.GetUI<MainMenuUI>("MainMenuUI").gameObject.activeInHierarchy;

                if (isMainMenu)
                {
                    var mainMenu = UIManager.Instance.GetUI<MainMenuUI>("MainMenuUI");
                    mainMenu.StartGame();
                    return "MAIN_MENU";
                }

                bool isHub = GameApp.Instance != null
                    && GameApp.Instance.HouseItem != null
                    && GameApp.Instance.HouseItem.activeSelf;

                if (isHub) return "ALREADY_IN_HUB";

                bool inRun = MapManager.Instance != null;
                if (inRun) return "ALREADY_IN_RUN";

                return "UNKNOWN";
            });

            if (initialPage != "MAIN_MENU")
            {
                return new JObject
                {
                    ["result"] = initialPage switch
                    {
                        "ALREADY_IN_HUB" => "already_in_hub",
                        "ALREADY_IN_RUN" => "already_in_run",
                        _ => "unknown_state"
                    },
                    ["message"] = initialPage switch
                    {
                        "ALREADY_IN_HUB" => "已经在游戏小屋中，无需操作",
                        "ALREADY_IN_RUN" => "已经在跑局中，无需操作",
                        _ => "无法识别当前页面状态，建议先调用 get_scene_state"
                    }
                };
            }

            // Step 2: 轮询等待转场真正完成（每 200ms，最长 15s）
            bool completed = false;
            for (int i = 0; i < 75; i++)
            {
                await Task.Delay(200);

                completed = await GameDispatcher.RunOnMainThread(() =>
                {
                    bool sceneTurn = IsUIActive<SceneTurnUI>("SceneTurnUI");
                    bool inkTurn = IsUIActive<InkTurnUI>("InkTurnUI");
                    bool curtain = IsUIActive<CurtainTurnUI>("CurtainTurnUI");
                    bool hubActive = GameApp.Instance?.HouseItem?.activeSelf == true;
                    return hubActive && !sceneTurn && !inkTurn && !curtain;
                });

                if (completed) break;
            }

            // Step 3: 返回结果
            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                if (completed)
                {
                    result["result"] = "success";
                    result["message"] = "已进入游戏小屋";
                    result["page"] = "HUB";
                }
                else
                {
                    bool mainMenuActive = IsUIActive<MainMenuUI>("MainMenuUI");
                    result["result"] = "timeout";
                    result["message"] = mainMenuActive
                        ? "转场超时，主菜单仍然可见。可能有弹窗阻挡或网络延迟，请检查游戏窗口"
                        : "转场超时，当前状态未知。建议调用 get_scene_state 确认";
                    result["page"] = mainMenuActive ? "MAIN_MENU" : "UNKNOWN";
                }

                return (JToken)result;
            });
        }
    }
}
