using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WitchModMCP.MCP;
using WitchModMCP.Utils;

namespace WitchModMCP.Tools
{
    public class LogTools : IMcpTool
    {
        public string Name => "get_recent_logs";
        public string Description => "获取最近 N 条日志";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["count"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "日志条数，默认 50",
                    ["default"] = 50
                }
            }
        };

        public Task<JToken> Execute(JToken args)
        {
            int count = args?["count"]?.Value<int>() ?? 50;
            var result = JArray.Parse(LogBuffer.GetRecent(count));
            return Task.FromResult<JToken>(result);
        }
    }
}
