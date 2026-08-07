using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;
using WitchModMCP.Dispatcher;

namespace WitchModMCP.Terminal
{
    public class LuaConsoleBehavior : WebSocketBehavior
    {
        protected override void OnOpen()
        {
            Commands.Log(WitchModMCPEntry.MOD_TAG, "[ConsoleWS] New WebSocket connection");
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Commands.Log(WitchModMCPEntry.MOD_TAG, "[ConsoleWS] Connection closed");
        }

        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[ConsoleWS] Error: {e.Message}");
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            if (!e.IsText || string.IsNullOrEmpty(e.Data))
                return;

            JObject msg;
            try
            {
                msg = JObject.Parse(e.Data);
            }
            catch
            {
                Send(new JObject { ["type"] = "error", ["message"] = "Invalid JSON" }.ToString(Formatting.None));
                return;
            }

            var cmd = msg.Value<string>("cmd");
            var code = msg.Value<string>("code");

            if (cmd == "exec" && !string.IsNullOrEmpty(code))
                ExecuteLuaAndRespond(code);
        }

        private void ExecuteLuaAndRespond(string code)
        {
            string responseJson = null;

            var task = GameDispatcher.RunOnMainThread(() =>
            {
                try
                {
                    var results = LuaExecService.Execute(code);
                    var formatted = ConsoleFormatter.Format(results);
                    responseJson = new JObject
                    {
                        ["type"] = "result",
                        ["data"] = formatted
                    }.ToString(Formatting.None);
                }
                catch (Exception ex)
                {
                    var message = ex.InnerException?.Message ?? ex.Message;
                    message = Regex.Replace(message, @"\r\n?", "\n");
                    message = Regex.Replace(message, @"\t", "  ");
                    responseJson = new JObject
                    {
                        ["type"] = "error",
                        ["message"] = message
                    }.ToString(Formatting.None);
                }
            });

            try
            {
                task.Wait(10000);
            }
            catch { }

            if (responseJson != null)
                Send(responseJson);
        }
    }
}
