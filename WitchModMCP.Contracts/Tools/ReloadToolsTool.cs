using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class ReloadToolsTool : IMcpTool
    {
        public string Name => "reload_tools";
        public string Description => "热重载所有 MCP 工具。在修改工具代码并重新编译 DLL 后，调用此接口即可让新工具/修改后的工具立即生效，无需重启游戏或按 F5。调完后建议用 list_tools 确认。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
        };

        public Task<JToken> Execute(JToken args)
        {
            return GameDispatcher.RunOnMainThread(() =>
            {
                McpRouter.ReloadAllTools();
                return (JToken)new JObject
                {
                    ["status"] = "ok",
                    ["hint"] = "工具已热重载，调用 list_tools 可查看当前工具列表"
                };
            });
        }
    }
}
