using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class ConsoleCommandTool : IMcpTool
    {
        public string Name => "eval_command";
        public string Description => "执行任意游戏控制台命令（等同于在游戏中打开控制台输入命令）。返回命令的输出文本。可以用 list_commands 查看所有可用命令。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["command"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "要执行的控制台命令字符串，例如 'give card all'、'cls'、'help give'"
                }
            },
            ["required"] = new JArray { "command" }
        };

        public async Task<JToken> Execute(JToken args)
        {
            string cmd = args?["command"]?.Value<string>();
            if (string.IsNullOrEmpty(cmd))
                throw new ArgumentException("command 参数不能为空");

            string result = await GameDispatcher.RunOnMainThread(() => ConsoleLogic.Input(cmd));
            return new JObject
            {
                ["command"] = cmd,
                ["result"] = result ?? ""
            };
        }
    }
}
