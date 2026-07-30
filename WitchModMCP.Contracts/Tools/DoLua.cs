using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Witch;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;
using XLua;

namespace WitchModMCP.Tools
{
    public class DoLuaTool : IMcpTool
    {
        public string Name => "doLua";
        public string Description => "通过游戏内置xlua执行lua语句  可能造成主线程卡顿";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["LuaCode"] = new JObject{["type"] = "string",["description"] = "实际需要执行的lua代码"}
            }
        };
        public async Task<JToken> Execute(JToken args)
        {
            string code = args["LuaCode"]?.Value<string>();
            if (string.IsNullOrEmpty(code))
                return new JObject { ["success"] = false, ["error"] = "LuaCode is required" };

            object[] results = null;
            string error = null;

            await GameDispatcher.RunOnMainThread(() =>
            {
                try
                {
                    var luaEnv = ScriptExecutor.luaEnv;
                    if (luaEnv == null) { error = "luaEnv is null"; return; }

                    results = luaEnv.DoString(code, "WitchModMCP", null);
                }
                catch (Exception ex)
                {
                    error = ex.InnerException?.Message ?? ex.Message;
                }
            });

            if (error != null)
                return new JObject { ["success"] = false, ["error"] = error };

            var jResults = new JArray();
            if (results != null)
            {
                foreach (var r in results)
                    jResults.Add(ToJToken(r));
            }

            return new JObject
            {
                ["success"] = true,
                ["results"] = jResults
            };
        }

        private static JToken ToJToken(object value)
        {
            if (value == null) return JValue.CreateNull();
            if (value is string s) return s;
            if (value is bool b) return b;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is float f) return new JValue((double)f);
            if (value is double d) return d;
            return value.ToString();
        }
    }
}