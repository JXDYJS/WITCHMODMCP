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
        public string Description => "关闭 BattleRewardsUI（未领取奖励自动转金钱），同时尝试关闭 CardChoiceUI 等子选择界面。";
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
