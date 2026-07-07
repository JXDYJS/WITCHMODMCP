using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class LoadSceneTool : IMcpTool
    {
        public string Name => "load_scene";
        public string Description => "加载/跳转到指定场景。type 支持: event (事件), fight (战斗), fakefight (假战斗)。id 可选: 具体ID、'common' (普通战)、'elite' (精英战)、'boss' (Boss战)。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["type"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "场景类型: event, fight, fakefight"
                },
                ["id"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "场景ID。可选，不填则随机。战斗类型支持 'common'、'elite'、'boss'"
                }
            },
            ["required"] = new JArray { "type" }
        };

        public async Task<JToken> Execute(JToken args)
        {
            string type = args?["type"]?.Value<string>();
            string id = args?["id"]?.Value<string>();
            if (string.IsNullOrEmpty(type))
                throw new ArgumentException("type 参数不能为空");

            string result = await GameDispatcher.RunOnMainThread(() => Commands.load(type, id));
            return new JObject
            {
                ["type"] = type,
                ["id"] = id,
                ["result"] = result ?? ""
            };
        }
    }
}
