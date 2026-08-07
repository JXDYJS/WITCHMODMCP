using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class ListCommandsTool : IMcpTool
    {
        public string Name => "list_commands";
        public string Description => "列出游戏中所有可用的控制台调试命令及其参数和帮助说明。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
        };

        private static string[] _cachedGiveSubs;

        public Task<JToken> Execute(JToken args)
        {
            return GameDispatcher.RunOnMainThread(() =>
            {
                var methods = typeof(Commands).GetMethods(BindingFlags.Public | BindingFlags.Static);
                var commands = new JArray();

                foreach (var m in methods.OrderBy(m => m.Name))
                {
                    if (m.ReturnType != typeof(string)) continue;

                    var helpAttr = m.GetCustomAttribute<HelpText>();
                    var desc = ReadHelpText(helpAttr);

                    var parameters = new JArray();
                    foreach (var p in m.GetParameters())
                    {
                        parameters.Add(new JObject
                        {
                            ["name"] = p.Name,
                            ["hasDefault"] = p.HasDefaultValue,
                            ["default"] = p.HasDefaultValue ? p.DefaultValue?.ToString() : null
                        });
                    }

                    var cmd = new JObject
                    {
                        ["name"] = m.Name,
                        ["parameters"] = parameters
                    };

                    if (!string.IsNullOrEmpty(desc))
                        cmd["description"] = desc;

                    if (m.Name == "give")
                        cmd["subCommands"] = new JArray(GetGiveSubCommands());

                    commands.Add(cmd);
                }

                return (JToken)new JObject
                {
                    ["commands"] = commands,
                    ["hint"] = "使用 eval_command 执行命令。例如: eval_command { \"command\": \"give money 100\" }"
                };
            });
        }

        private static string[] GetGiveSubCommands()
        {
            if (_cachedGiveSubs != null) return _cachedGiveSubs;

            var method = typeof(Commands).GetMethod("$Rougamo_give",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (method?.GetMethodBody()?.GetILAsByteArray() is not { } il)
                return _cachedGiveSubs = Array.Empty<string>();

            var result = new HashSet<string>();
            var blacklist = new HashSet<string>
            {
                "Id", "Name", "Tag", "Rarity", "Strength", "Lucky", "Perceive", "Wisdom",
                "FightUI", "TopBarUI", "CaptionUI", "DeckUI", "SelectCardEnd",
                "null", "all", "Null"
            };
            var module = method.Module;

            for (int i = 0; i < il.Length - 5; i++)
            {
                if (il[i] != 0x72) continue;

                int token = il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
                string s;
                try { s = module.ResolveString(token); }
                catch { continue; }

                if (s.Length >= 2
                    && s.All(c => c <= 127)
                    && !s.Contains(' ')
                    && !s.Contains('<')
                    && !s.Contains('>')
                    && !s.Contains('|')
                    && !blacklist.Contains(s)
                    && !s.All(char.IsDigit))
                {
                    result.Add(s);
                }
                i += 4;
            }

            _cachedGiveSubs = result.OrderBy(x => x).ToArray();
            return _cachedGiveSubs;
        }

        private static string ReadHelpText(HelpText attr)
        {
            if (attr == null) return null;
            try
            {
                var backingField = typeof(HelpText).GetField(
                    "_003Ctext_003Ek__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                return (string)backingField?.GetValue(attr);
            }
            catch
            {
                return null;
            }
        }
    }
}
