using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class GetEventStateTool : IMcpTool
    {
        private static readonly BindingFlags _nonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly BindingFlags _publicInstance = BindingFlags.Public | BindingFlags.Instance;

        public string Name => "get_event_state";
        public string Description => "获取当前事件(EventUI)的状态：事件ID、标题、描述、所有选项的描述文本和可用状态。";
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

                var eventUI = UIManager.Instance?.GetUI<EventUI>("EventUI");
                if (eventUI == null || !eventUI.gameObject.activeInHierarchy)
                {
                    result["eventOpen"] = false;
                    result["message"] = "当前没有打开的事件UI";
                    return (JToken)result;
                }

                result["eventOpen"] = true;

                try
                {
                    // Read private field: thisid
                    var thisidField = typeof(EventUI).GetField("thisid", _nonPublicInstance);
                    if (thisidField?.GetValue(eventUI) is string eventId)
                        result["eventId"] = eventId;
                }
                catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetEventStateTool] thisid: {ex.Message}"); }

                try
                {
                    // Read private field: dataConfig
                    var dataConfigField = typeof(EventUI).GetField("dataConfig", _nonPublicInstance);
                    var dc = dataConfigField?.GetValue(eventUI);
                    if (dc != null)
                    {
                        var dcType = dc.GetType();
                        var dataProp = dcType.GetProperty("data", _publicInstance);
                        var data = dataProp?.GetValue(dc, null) as IDictionary<string, string>;

                        if (data != null)
                        {
                            result["title"] = ReadLocalized(data, "Name");
                            result["description"] = ReadLocalized(data, "TotalDescribe");

                            // Option descriptions from config
                            var optionDescs = new JArray();
                            for (int i = 1; i <= 10; i++)
                            {
                                string key = i + "Describe";
                                if (data.ContainsKey(key) || data.ContainsKey(key + "_ChineseSimplified"))
                                {
                                    optionDescs.Add(ReadLocalized(data, key));
                                }
                            }
                            if (optionDescs.Count > 0)
                                result["optionDescriptions"] = optionDescs;
                        }

                        // Read Vars for choice types
                        var varsProp = dcType.GetProperty("Vars", _publicInstance);
                        var vars = varsProp?.GetValue(dc, null) as IDictionary<string, string>;
                        if (vars != null)
                        {
                            var choices = new JObject();
                            for (int i = 1; i <= 10; i++)
                            {
                                if (vars.TryGetValue("Choice" + i, out var val))
                                    choices["Choice" + i] = val;
                            }
                            if (choices.Count > 0)
                                result["choices"] = choices;
                        }
                    }
                }
                catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetEventStateTool] dataConfig: {ex.Message}"); }

                // Read options from UI buttons
                try
                {
                    var options = new JArray();
                    var selector = eventUI.transform.Find("Windows/Map0/Content/Selector");
                    if (selector != null)
                    {
                        for (int i = 1; i <= 4; i++)
                        {
                            var opt = selector.Find("option" + i);
                            if (opt == null || !opt.gameObject.activeInHierarchy)
                                continue;

                            var optionInfo = new JObject();
                            optionInfo["index"] = i;

                            // Button text
                            try
                            {
                                var normalDesc = opt.Find("Normal/Description");
                                if (normalDesc != null)
                                {
                                    var tmp = normalDesc.GetComponent("TMP_Text");
                                    if (tmp != null)
                                    {
                                        var textProp = tmp.GetType().GetProperty("text");
                                        if (textProp != null)
                                            optionInfo["text"] = (string)textProp.GetValue(tmp, null) ?? "";
                                    }
                                }
                            }
                            catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetEventStateTool] normalDesc: {ex.Message}"); }

                            // Disabled text (alternative if Normal is empty)
                            if (!optionInfo.ContainsKey("text") || string.IsNullOrEmpty(optionInfo["text"]?.Value<string>()))
                            {
                                try
                                {
                                    var disabledDesc = opt.Find("Disabled/Description");
                                    if (disabledDesc != null)
                                    {
                                        var tmp = disabledDesc.GetComponent("TMP_Text");
                                        if (tmp != null)
                                        {
                                            var textProp = tmp.GetType().GetProperty("text");
                                            if (textProp != null)
                                                optionInfo["text"] = (string)textProp.GetValue(tmp, null) ?? "";
                                        }
                                    }
                                }
                                catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetEventStateTool] disabledDesc: {ex.Message}"); }
                            }

                            // Interactable
                            try
                            {
                                var btnManager = opt.GetComponent("ButtonManager");
                                if (btnManager != null)
                                {
                                    var interactField = btnManager.GetType().GetField("isInteractable", _publicInstance);
                                    bool interactable = interactField == null || (bool)interactField.GetValue(btnManager);
                                    optionInfo["interactable"] = interactable;
                                }
                            }
                            catch { optionInfo["interactable"] = false; }

                            options.Add(optionInfo);
                        }
                    }
                    result["options"] = options;
                }
                catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetEventStateTool] options: {ex.Message}"); }

                return (JToken)result;
            });
        }

        private static string ReadLocalized(IDictionary<string, string> data, string key)
        {
            if (data == null) return null;
            // Try language-specific key first, fallback to base key
            string langKey = key + "_ChineseSimplified";
            if (data.TryGetValue(langKey, out var val) && !string.IsNullOrEmpty(val))
                return val;
            if (data.TryGetValue(key, out val) && !string.IsNullOrEmpty(val))
                return val;
            return null;
        }
    }
}
