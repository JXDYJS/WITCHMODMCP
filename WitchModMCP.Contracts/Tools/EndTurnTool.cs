using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class EndTurnTool : IMcpTool
    {
        public string Name => "end_turn";
        public string Description => "强制结束当前玩家回合，触发敌方行动。仅在战斗中且为玩家回合时有效。";
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

                bool inFight = FightManager.Instance != null
                    && FightManager.Instance.fightType != FightType.None;
                if (!inFight)
                {
                    result["result"] = "error";
                    result["message"] = "当前不在战斗中";
                    return (JToken)result;
                }

                if (FightManager.Instance.fightType != FightType.Player)
                {
                    result["result"] = "error";
                    result["message"] = $"当前不是玩家回合（phase={FightManager.Instance.fightType}），无法结束回合";
                    return (JToken)result;
                }

                try
                {
                    // Same call as the end-turn button in FightUI
                    FightManager.Instance.CmdAnnounceDone(
                        FightPlayer.Instance.InstanceId,
                        FightPlayer.Instance.Status.state == IStatusManager.State.Dead
                    );

                    result["result"] = "success";
                    result["message"] = "已触发结束回合指令，敌方即将行动";
                    result["phase"] = FightManager.Instance.fightType.ToString();
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"结束回合失败: {ex.Message}";
                }

                return (JToken)result;
            });
        }
    }
}
