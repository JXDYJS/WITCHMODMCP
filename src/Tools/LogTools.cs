using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WitchModMCP.MCP;
using WitchModMCP.Utils;

namespace WitchModMCP.Tools
{
    public class LogTools : IMcpTool
    {
        public string Name => "get_recent_logs";
        public string Description => "获取最近 N 条日志，可按级别筛选";
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
                },
                ["level"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "日志级别筛选：Error / Warning / Log / All（默认 All）",
                    ["default"] = "All",
                    ["enum"] = new JArray { "Error", "Warning", "Log", "All" }
                }
            }
        };

        public Task<JToken> Execute(JToken args)
        {
            int count = args?["count"]?.Value<int>() ?? 50;
            string level = args?["level"]?.Value<string>() ?? "All";

            var raw = LogBuffer.GetRecent(count);
            var allEntries = JArray.Parse(raw);

            if (string.Equals(level, "All", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<JToken>(allEntries);

            var filtered = new JArray();
            foreach (var entry in allEntries)
            {
                var entryType = entry["type"]?.Value<string>() ?? "";
                if (string.Equals(entryType, level, StringComparison.OrdinalIgnoreCase))
                    filtered.Add(entry);
            }
            return Task.FromResult<JToken>(filtered);
        }
    }
}
