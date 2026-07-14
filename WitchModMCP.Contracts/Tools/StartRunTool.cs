using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class StartRunTool : IMcpTool
    {
        public string Name => "start_run";
        public string Description => "在职业选择大厅中点击'启程'开始跑局。会完成最后初始化（属性加点、卡组构建），进入地图页面。之后可以用 load_scene 跳转战斗。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
        };

        public async Task<JToken> Execute(JToken args)
        {
            // Step 1: 确认状态并触发启程
            string initialPage = await GameDispatcher.RunOnMainThread(() =>
            {
                var ge = UIManager.Instance?.GetUI<GameEntryUI>("GameEntryUI");
                if (ge == null) return "NOT_IN_LOBBY";

                // 检查是否已经在跑局中（MapManager 活跃且 GameEntryUI 已关闭）
                if (MapManager.Instance != null && !ge.gameObject.activeInHierarchy)
                    return "ALREADY_IN_RUN";

                // 确保 Career / Partner 已设置
                if (GameEntryUI.career == null)
                {
                    GameEntryUI.career = new DataConfig("career_1", DataType.Career);
                }
                if (RoleTable.Instance != null && RoleTable.Instance.Career == null)
                    RoleTable.Instance.Career = GameEntryUI.career;

                if (GameEntryUI.partner == null)
                {
                    GameEntryUI.partner = new DataConfig("Partner_10001", DataType.Partner);
                }

                // 确保 GameSaveManager 有 save
                if (GameEntryUI.selectedSave == null)
                {
                    return "NO_SAVE";
                }

                // 调用 GameEntryUI.StartGame 启程
                // 该方法会执行：CheckCareer → sync save → PlayerManager.StartGame()
                try
                {
                    ge.StartGame();
                    return "TRIGGERED";
                }
                catch (Exception ex)
                {
                    return $"ERROR:{ex.Message}";
                }
            });

            if (initialPage == "NOT_IN_LOBBY")
            {
                return new JObject
                {
                    ["result"] = "error",
                    ["message"] = "当前不在职业选择大厅中，请先调用 start_new_game"
                };
            }

            if (initialPage == "ALREADY_IN_RUN")
            {
                return new JObject
                {
                    ["result"] = "already_in_run",
                    ["message"] = "已经在跑局中，无需重复操作"
                };
            }

            if (initialPage == "NO_SAVE")
            {
                return new JObject
                {
                    ["result"] = "error",
                    ["message"] = "没有可用的存档数据，请先调用 start_new_game"
                };
            }

            if (initialPage.StartsWith("ERROR:"))
            {
                // GameEntryUI.StartGame 可能因为网络条件不满足失败
                // 回退方案：直接调 PlayerManager.StartGame
                string fallbackMsg = await GameDispatcher.RunOnMainThread(() =>
                {
                    try
                    {
                        if (RoleTable.Instance != null && RoleTable.Instance.Career == null)
                        {
                            var careerTable = Singleton<GameConfigManager>.Instance
                                .GetTable(DataType.Career).Getlines();
                            if (careerTable != null && careerTable.Count > 0)
                            {
                                string firstId = careerTable[0]["Id"];
                                RoleTable.Instance.Career = new DataConfig(firstId, DataType.Career);
                                GameEntryUI.career = RoleTable.Instance.Career;
                            }
                        }

                        if (GameEntryUI.partner == null)
                        {
                            var partnerTable = Singleton<GameConfigManager>.Instance
                                .GetTable(DataType.Partner).Getlines();
                            if (partnerTable != null && partnerTable.Count > 1)
                            {
                                GameEntryUI.partner = new DataConfig(partnerTable[1]["Id"], DataType.Partner);
                            }
                        }

                        if (PlayerManager.Instance != null)
                        {
                            PlayerManager.Instance.StartGame();
                            return "fallback_ok";
                        }
                        return "fallback_failed: PlayerManager is null";
                    }
                    catch (Exception ex)
                    {
                        return $"fallback_error: {ex.Message}";
                    }
                });

                if (fallbackMsg == "fallback_ok")
                {
                    initialPage = "TRIGGERED";
                }
                else
                {
                    return new JObject
                    {
                        ["result"] = "error",
                        ["message"] = $"启程失败: {initialPage.Substring(6)}。回退也失败: {fallbackMsg}"
                    };
                }
            }

            // Step 2: 轮询等待跑局开始（MapManager 激活，最长 20s）
            bool runStarted = false;
            for (int i = 0; i < 100; i++)
            {
                await Task.Delay(200);

                runStarted = await GameDispatcher.RunOnMainThread(() =>
                {
                    var ge2 = UIManager.Instance?.GetUI<GameEntryUI>("GameEntryUI");
                    return MapManager.Instance != null
                        && (ge2 == null || !ge2.gameObject.activeInHierarchy);
                });

                if (runStarted) break;
            }

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                if (runStarted)
                {
                    result["result"] = "success";
                    result["message"] = "跑局已开始，已进入地图页面";
                    result["page"] = "MAP";

                    try
                    {
                        result["level"] = MapManager.Instance?.Level ?? 0;
                    }
                    catch { }
                }
                else
                {
                    result["result"] = "timeout";
                    result["message"] = "等待跑局启动超时。建议调用 get_scene_state 确认当前状态，或使用 load_scene 直接跳转战斗";
                }

                return (JToken)result;
            });
        }
    }
}
