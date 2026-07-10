using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class GetSceneStateTool : IMcpTool
    {
        public string Name => "get_scene_state";
        public string Description => "检测当前游戏页面/状态。返回当前所在页面(MAIN_MENU/MODE_SELECT/LOBBY/MAP/FIGHT/HUB)、战斗状态、弹窗/转场阻挡、跑局信息等。";
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
            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                // --- 弹窗 / 转场检测 ---
                bool hasModal = UIManager.Instance?.WindowObj != null || UIManager.Instance?.InputObj != null;
                bool isTransitioning = IsUIActive<InkTurnUI>("InkTurnUI")
                    || IsUIActive<CurtainTurnUI>("CurtainTurnUI")
                    || IsUIActive<SceneTurnUI>("SceneTurnUI");

                result["modals"] = hasModal;
                result["transitioning"] = isTransitioning;

                // --- 叠加层检测（可以与其他页面共存） ---
                var overlays = new JArray();
                if (IsUIActive<SettingUI>("SettingUI")) overlays.Add("SettingUI");
                if (IsUIActive<BackpackUI>("BackpackUI")) overlays.Add("BackpackUI");
                result["overlays"] = overlays;

                // --- 页面检测（优先级从高到低） ---

                // 1. 主菜单
                bool isMainMenu = IsUIActive<MainMenuUI>("MainMenuUI");

                // 2. 模式选择
                bool isModeSelect = IsUIActive<ModeChoiceUI>("ModeChoiceUI");

                // 3. 职业选择大厅
                bool isLobby = IsUIActive<GameEntryUI>("GameEntryUI");

                // 4. 战斗中
                bool inFight = FightManager.Instance != null
                    && FightManager.Instance.fightType != FightType.None;

                // 5. 地图
                bool isMap = IsUIActive<MapSelectUI>("MapSelectUI");

                // 6. 小屋内（中枢）
                bool isHub = GameApp.Instance != null
                    && GameApp.Instance.HouseItem != null
                    && GameApp.Instance.HouseItem.activeSelf;

                // 7. 跑局中（MapManager激活）
                bool inRun = MapManager.Instance != null
                    && !isMainMenu
                    && !isModeSelect
                    && !isLobby;

                // --- 判断主页面 ---
                string page = "UNKNOWN";
                if (isMainMenu) page = "MAIN_MENU";
                else if (isModeSelect) page = "MODE_SELECT";
                else if (isLobby) page = "LOBBY";
                else if (inFight) page = "FIGHT";
                else if (isMap) page = "MAP";
                else if (isHub) page = "HUB";
                else if (inRun) page = "MAP";

                result["page"] = page;
                result["inRun"] = inRun;
                result["inFight"] = inFight;

                // --- 战斗详情 ---
                if (inFight)
                {
                    result["fightType"] = FightManager.Instance.fightType.ToString();
                    result["isFake"] = FightManager.Instance.IsFake;

                    if (FightPlayer.Instance?.Status != null)
                    {
                        var fp = new JObject
                        {
                            ["hp"] = FightPlayer.Instance.Status.CurHp,
                            ["maxHp"] = FightPlayer.Instance.Status.MaxHp,
                            ["power"] = FightPlayer.Instance.CurPowerCount,
                            ["shield"] = (int)(FightPlayer.Instance.Status.Defend)
                        };
                        result["fightPlayer"] = fp;
                    }
                }

                // --- 跑局详情 ---
                if (inRun && MapManager.Instance != null)
                {
                    try
                    {
                        result["level"] = MapManager.Instance.Level;
                    }
                    catch { }
                }

                // --- 玩家基础属性 ---
                if (RoleTable.Instance != null)
                {
                    var player = new JObject();
                    try { player["hp"] = FightPlayer.Instance?.Status?.CurHp ?? 0; } catch { }
                    try { player["maxHp"] = FightPlayer.Instance?.Status?.MaxHp ?? 0; } catch { }
                    try
                    {
                        player["san"] = RoleTable.Instance.San;
                        player["maxSan"] = RoleTable.Instance.MaxSan;
                    }
                    catch { }
                    try { player["money"] = (int)RoleTable.Instance.Money; } catch { }
                    result["player"] = player;
                }

                return (JToken)result;
            });
        }
    }
}
