using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class GiveItemTool : IMcpTool
    {
        public string Name => "give_item";
        public string Description => "给予玩家物品/资源。参数 type 支持: maxsan, card, time, relic, bless, money, san, power, timecount, true/truth, win, str/strength, luc/lucky, per/perceive, wis/wisdom, level, randomcard, randomcardbydeck, draw, randombless, goodbless, randomrelic, randomrelicByRarity, randomcardByRarity, def, live, AllBuff, ench, exp, slot, escape, unlimitsafe。card 类 value 可以是卡牌 ID 或 'all'。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["type"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "物品类型，例如 card, relic, money, bless, san 等"
                },e
                ["value"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "物品数量/ID，例如 100 (数量)、某个卡牌ID、或 'all' (全部)"
                }
            },
            ["required"] = new JArray { "type", "value" }
        };

        public async Task<JToken> Execute(JToken args)
        {
            string type = args?["type"]?.Value<string>();
            string value = args?["value"]?.Value<string>();
            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(valu))
                throw new ArgumentException("type 和 value 参数不能为空");

            string result = await GameDispatcher.RunOnMainThread(() => Commands.give(type, value));
            return new JObject
            {
                ["type"] = type,
                ["value"] = value,
                ["result"] = result ?? ""
            };
        }
    }
}
