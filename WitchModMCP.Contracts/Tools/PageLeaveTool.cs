using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class PageLeaveTool : IMcpTool
    {
        public string Name => "page_leave";
        public string Description => "离开当前页面。自动检测当前所在页面（BreaksUI/EventUI/SafeBoxUI/弹窗等），调用对应的离开/关闭逻辑。";
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

                // 优先级: SafeBoxUI > BreaksUI > EventUI > 通用UIBase弹窗
                try
                {
                    // 1. SafeBoxUI - 先保存再关闭
                    var safeBox = UIManager.Instance?.GetUI<SafeBoxUI>("SafeBoxUI");
                    if (safeBox != null && safeBox.gameObject.activeInHierarchy)
                    {
                        SafeBoxUI.SafeboxSave();
                        safeBox.Close();
                        result["result"] = "success";
                        result["page"] = "SafeBoxUI";
                        result["message"] = "保险箱已保存并关闭";
                        return (JToken)result;
                    }
                }
                catch { }

                try
                {
                    // 2. BreaksUI - 休息点页面
                    var breaksUI = UnityEngine.Object.FindAnyObjectByType<BreaksUI>();
                    if (breaksUI != null && breaksUI.gameObject.activeInHierarchy)
                    {
                        breaksUI.Close();
                        result["result"] = "success";
                        result["page"] = "BreaksUI";
                        result["message"] = "已离开休息点";
                        return (JToken)result;
                    }
                }
                catch { }

                try
                {
                    // 3. EventUI - 事件页面
                    var eventUI = UIManager.Instance?.GetUI<EventUI>("EventUI");
                    if (eventUI != null && eventUI.gameObject.activeInHierarchy)
                    {
                        eventUI.Entry();
                        result["result"] = "success";
                        result["page"] = "EventUI";
                        result["message"] = "已离开事件";
                        return (JToken)result;
                    }
                }
                catch { }

                try
                {
                    // 4. CardChoiceUI - 选牌
                    var ccUI = UIManager.Instance?.GetUI<CardChoiceUI>("CardChoiceUI");
                    if (ccUI != null && ccUI.gameObject.activeInHierarchy)
                    {
                        ccUI.Close();
                        result["result"] = "success";
                        result["page"] = "CardChoiceUI";
                        result["message"] = "已关闭选牌界面";
                        return (JToken)result;
                    }
                }
                catch { }

                try
                {
                    // 5. BattleRewardsUI - 战斗奖励
                    var rewardsUI = UIManager.Instance?.GetUI<BattleRewardsUI>("BattleRewardsUI");
                    if (rewardsUI != null && rewardsUI.gameObject.activeInHierarchy)
                    {
                        rewardsUI.Close();
                        result["result"] = "success";
                        result["page"] = "BattleRewardsUI";
                        result["message"] = "已关闭战斗奖励";
                        return (JToken)result;
                    }
                }
                catch { }

                try
                {
                    // 6. BlessingChoiceGenerator - 祝福选择
                    var blessGen = UIManager.Instance?.Find("BlessingChoiceGenerator");
                    if (blessGen != null && blessGen.gameObject.activeInHierarchy)
                    {
                        blessGen.Close();
                        result["result"] = "success";
                        result["page"] = "BlessingChoiceGenerator";
                        result["message"] = "已关闭祝福选择";
                        return (JToken)result;
                    }
                }
                catch { }

                try
                {
                    // 7. ShopUI
                    var shopUI = UIManager.Instance?.GetUI<ShopUI>("ShopUI");
                    if (shopUI != null && shopUI.gameObject.activeInHierarchy)
                    {
                        shopUI.Close();
                        result["result"] = "success";
                        result["page"] = "ShopUI";
                        result["message"] = "已关闭商店";
                        return (JToken)result;
                    }
                }
                catch { }

                try
                {
                    // 8. BackpackUI - 背包
                    var backpackUI = UIManager.Instance?.GetUI<BackpackUI>("BackpackUI");
                    if (backpackUI != null && backpackUI.gameObject.activeInHierarchy)
                    {
                        backpackUI.Close();
                        result["result"] = "success";
                        result["page"] = "BackpackUI";
                        result["message"] = "已关闭背包";
                        return (JToken)result;
                    }
                }
                catch { }

                try
                {
                    // 9. SettingUI - 设置
                    var settingUI = UIManager.Instance?.GetUI<SettingUI>("SettingUI");
                    if (settingUI != null && settingUI.gameObject.activeInHierarchy)
                    {
                        settingUI.Close();
                        result["result"] = "success";
                        result["page"] = "SettingUI";
                        result["message"] = "已关闭设置";
                        return (JToken)result;
                    }
                }
                catch { }

                result["result"] = "error";
                result["message"] = "当前没有找到可离开的页面";
                return (JToken)result;
            });
        }
    }
}
