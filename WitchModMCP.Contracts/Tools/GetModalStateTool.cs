using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Witch.UI;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class GetModalStateTool : IMcpTool
    {
        public string Name => "get_modal_state";
        public string Description => "检测当前是否有 ModalWindow 弹窗开启，如有则返回标题、描述文本、按钮配置等信息。配合 scan_ui/click_ui 可操作弹窗按钮。";

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

                var windowObj = UIManager.Instance?.WindowObj;
                if (windowObj == null)
                {
                    result["hasModal"] = false;
                    return (JToken)result;
                }

                var modal = windowObj.GetComponent("ModalWindowManager");
                if (modal == null)
                {
                    result["hasModal"] = false;
                    return (JToken)result;
                }

                result["hasModal"] = true;
                result["gameObjectName"] = windowObj.name;

                try
                {
                    var titleText = modal.GetType().GetField("titleText")?.GetValue(modal) as string;
                    result["title"] = titleText ?? "";
                }
                catch { result["title"] = ""; }

                try
                {
                    var descText = modal.GetType().GetField("descriptionText")?.GetValue(modal) as string;
                    result["description"] = descText ?? "";
                }
                catch { result["description"] = ""; }

                try
                {
                    var mustChoose = (bool)(modal.GetType().GetField("mustChoose")?.GetValue(modal) ?? false);
                    result["mustChoose"] = mustChoose;
                }
                catch { result["mustChoose"] = false; }

                try
                {
                    var showConfirm = (bool)(modal.GetType().GetField("showConfirmButton")?.GetValue(modal) ?? true);
                    result["showConfirm"] = showConfirm;
                }
                catch { result["showConfirm"] = true; }

                try
                {
                    var showCancel = (bool)(modal.GetType().GetField("showCancelButton")?.GetValue(modal) ?? true);
                    result["showCancel"] = showCancel;
                }
                catch { result["showCancel"] = true; }

                var buttons = new JObject();

                try
                {
                    var confirmBtn = modal.GetType().GetField("confirmButton")?.GetValue(modal);
                    if (confirmBtn != null)
                    {
                        var btnGO = confirmBtn.GetType().GetProperty("gameObject")?.GetValue(confirmBtn) as GameObject;
                        buttons["confirmActive"] = btnGO != null && btnGO.activeInHierarchy;

                        var btnText = confirmBtn.GetType().GetMethod("GetText")?.Invoke(confirmBtn, null) as string;
                        if (btnText == null)
                        {
                            var btnTextObj = confirmBtn.GetType().GetField("buttonText")?.GetValue(confirmBtn);
                            if (btnTextObj != null)
                                btnText = btnTextObj.GetType().GetProperty("text")?.GetValue(btnTextObj) as string;
                        }
                        buttons["confirmText"] = btnText ?? "Confirm";
                    }
                }
                catch { buttons["confirmActive"] = true; buttons["confirmText"] = "Confirm"; }

                try
                {
                    var cancelBtn = modal.GetType().GetField("cancelButton")?.GetValue(modal);
                    if (cancelBtn != null)
                    {
                        var btnGO = cancelBtn.GetType().GetProperty("gameObject")?.GetValue(cancelBtn) as GameObject;
                        buttons["cancelActive"] = btnGO != null && btnGO.activeInHierarchy;

                        var btnText = cancelBtn.GetType().GetMethod("GetText")?.Invoke(cancelBtn, null) as string;
                        if (btnText == null)
                        {
                            var btnTextObj = cancelBtn.GetType().GetField("buttonText")?.GetValue(cancelBtn);
                            if (btnTextObj != null)
                                btnText = btnTextObj.GetType().GetProperty("text")?.GetValue(btnTextObj) as string;
                        }
                        buttons["cancelText"] = btnText ?? "Cancel";
                    }
                }
                catch { buttons["cancelActive"] = true; buttons["cancelText"] = "Cancel"; }

                result["buttons"] = buttons;

                return (JToken)result;
            });
        }
    }
}
