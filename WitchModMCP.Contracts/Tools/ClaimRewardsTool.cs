using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
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

        private static readonly BindingFlags _publicInstance = BindingFlags.Public | BindingFlags.Instance;

        public async Task<JToken> Execute(JToken args)
        {
            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();
                result["actions"] = new JArray();

                // 1. BattleRewardsUI — 找到"关闭"按钮并点击
                var rewardsUI = UIManager.Instance?.GetUI<BattleRewardsUI>("BattleRewardsUI");
                if (rewardsUI != null && rewardsUI.gameObject.activeInHierarchy)
                {
                    var closeBtn = rewardsUI.transform.Find("Window Manager/Close");
                    if (closeBtn != null)
                    {
                        if (TryClickButtonManager(closeBtn.gameObject) || TryClickStandardButton(closeBtn.gameObject))
                        {
                            result["claimed"] = true;
                            ((JArray)result["actions"]).Add("BattleRewardsUI closed via button click");
                        }
                    }
                }

                // 2. CardChoiceUI — 退出按钮
                var cardChoiceUI = UIManager.Instance?.GetUI<CardChoiceUI>("CardChoiceUI");
                if (cardChoiceUI != null && cardChoiceUI.gameObject.activeInHierarchy)
                {
                    var exitBtn = cardChoiceUI.transform.Find("ExitButton");
                    if (exitBtn != null)
                    {
                        TryClickButtonManager(exitBtn.gameObject);
                        TryClickStandardButton(exitBtn.gameObject);
                    }
                    ((JArray)result["actions"]).Add("CardChoiceUI closed");
                }

                // 3. BlessingChoiceGenerator — 第一个可交互按钮
                var blessGen = UIManager.Instance?.Find("BlessingChoiceGenerator");
                if (blessGen != null && blessGen.gameObject.activeInHierarchy)
                {
                    var buttons = blessGen.GetComponentsInChildren<Button>(false)
                        .Where(b => b.interactable && b.gameObject.activeInHierarchy)
                        .ToList();
                    if (buttons.Count > 0)
                    {
                        buttons[0].onClick.Invoke();
                    }
                    ((JArray)result["actions"]).Add("BlessingChoiceGenerator closed");
                }

                return (JToken)result;
            });
        }

        private static bool TryClickButtonManager(GameObject obj)
        {
            var monos = obj.GetComponents<MonoBehaviour>();
            foreach (var comp in monos)
            {
                if (comp == null) continue;
                if (comp.GetType().Name != "ButtonManager") continue;

                var onClickField = comp.GetType().GetField("onClick", _publicInstance);
                if (onClickField?.GetValue(comp) is UnityEvent onClick)
                {
                    onClick.Invoke();
                    return true;
                }
            }
            return false;
        }

        private static bool TryClickStandardButton(GameObject obj)
        {
            var btn = obj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.Invoke();
                return true;
            }
            return false;
        }
    }
}
