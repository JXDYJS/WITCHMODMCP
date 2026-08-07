using System;
using System.Threading.Tasks;
using Data.Save;
using Newtonsoft.Json.Linq;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class GetGameDataTool : IMcpTool
    {
        public string Name => "get_game_data";
        public string Description => "获取当前游戏状态快照，包括玩家属性、战斗信息、背包概况等。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
        };

        public async Task<JToken> Execute(JToken args)
        {
            return await GameDispatcher.RunOnMainThread(() =>
            {
                var data = new JObject();

                try
                {
                    if (RoleTable.Instance != null)
                    {
                        var player = new JObject();

                        if (FightPlayer.Instance?.Status != null)
                        {
                            player["hp"] = FightPlayer.Instance.Status.CurHp;
                            player["maxHp"] = FightPlayer.Instance.Status.MaxHp;
                        }

                        player["san"] = RoleTable.Instance.San;
                        player["maxSan"] = RoleTable.Instance.MaxSan;
                        player["money"] = (int)RoleTable.Instance.Money;
                        player["cardCount"] = RoleTable.Instance.cardList?.Count ?? 0;
                        player["relicCount"] = RoleTable.Instance.relicList?.Count ?? 0;
                        player["blessCount"] = RoleTable.Instance.blessingConfigs?.Count ?? 0;
                        player["unCardCount"] = RoleTable.Instance.UnCardList?.Count ?? 0;
                        player["isDead"] = RoleTable.Instance.isDead;

                        data["player"] = player;
                    }
                }
                catch (Exception ex)
                {
                    data["playerError"] = ex.Message;
                }

                try
                {
                    if (FightManager.Instance != null && FightManager.Instance.fightType != FightType.None)
                    {
                        var fight = new JObject
                        {
                            ["inFight"] = true,
                            ["fightType"] = FightManager.Instance.fightType.ToString(),
                            ["playerPower"] = FightPlayer.Instance?.CurPowerCount ?? 0,
                            ["playerShield"] = FightPlayer.Instance?.Status?.Defend ?? 0
                        };
                        data["fight"] = fight;
                    }
                    else
                    {
                        data["fight"] = new JObject { ["inFight"] = false };
                    }
                }
                catch (Exception ex)
                {
                    data["fightError"] = ex.Message;
                }

                try
                {
                    var rt = Singleton<GameRuntimeData>.Instance;
                    if (rt != null)
                    {
                        var runtime = new JObject
                        {
                            ["level"] = GameSaveManager.GetLevel(),
                            ["time"] = (int)rt.Time,
                            ["truth"] = (int)rt.Truth,
                            ["exp"] = (int)rt.Exp
                        };
                        data["runtime"] = runtime;
                    }
                }
                catch (Exception ex)
                {
                    data["runtimeError"] = ex.Message;
                }

                return (JToken)data;
            });
        }
    }
}
