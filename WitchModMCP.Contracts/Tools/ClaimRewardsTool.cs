using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class ClaimRewardsTool : IMcpTool
    {
        public string Name => "claim_rewards";
        public string Description => "领取当前战斗奖励。如果有 BattleRewardsUI 则点确定关闭（未领取的奖励会自动转化为金钱）；如果有 CardChoiceUI / BlessingChoiceGenerator 等子选择界面则尝试透传关闭。之后再调用 load_scene 可进入下一场。";
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
                result["actions"] = new JArray();

                // 1. BattleRewardsUI — 奖励总界面
                var rewardsUI = UIManager.Instance?.GetUI<BattleRewardsUI>("BattleRewardsUI");
                if (rewardsUI != null && rewardsUI.gameObject.activeInHierarchy)
                {
                    rewardsUI.Close();
                    result["claimed"] = true;
                    ((JArray)result["actions"]).Add("BattleRewardsUI closed, unconverted rewards → gold");
                }

                // 2. CardChoiceUI — 选卡界面（通常在奖励之后出现）
                var cardChoiceUI = UIManager.Instance?.GetUI<CardChoiceUI>("CardChoiceUI");
                if (cardChoiceUI != null && cardChoiceUI.gameObject.activeInHierarchy)
                {
                    cardChoiceUI.Close();
                    ((JArray)result["actions"]).Add("CardChoiceUI closed");
                }

                // 3. BlessingChoiceGenerator — 祝福选择界面
                var blessGen = UIManager.Instance?.Find("BlessingChoiceGenerator");
                if (blessGen != null && blessGen.gameObject.activeInHierarchy)
                {
                    blessGen.Close();
                    ((JArray)result["actions"]).Add("BlessingChoiceGenerator closed");
                }

                return (JToken)result;
            });
        }
    }
}
