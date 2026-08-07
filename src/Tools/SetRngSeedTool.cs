using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class SetRngSeedTool : IMcpTool
    {
        public string Name => "set_rng_seed";
        public string Description => "强制设置 TempDataManager 的 RNG 种子池（用于可复现的随机测试）。注意：战斗中 RNG 由 MapManager.NowDice 控制，此工具提供额外的种子控制能力。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["seed"] = new JObject { ["type"] = "integer", ["description"] = "随机种子值" },
                ["forceRng"] = new JObject
                {
                    ["type"] = "number",
                    ["description"] = "强制下一次 Dice 结果为该值（0.0~1.0），用于确定性测试"
                }
            }
        };

        public async Task<JToken> Execute(JToken args)
        {
            int? seed = args?["seed"]?.Value<int>();
            double? forceRng = args?["forceRng"]?.Value<double>();

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();
                var changes = new JArray();

                if (seed.HasValue)
                {
                    try
                    {
                        // Access TempDataManager to reseed
                        var tdm = Singleton<TempDataManager>.Instance;
                        if (tdm != null)
                        {
                            // Use reflection to call the internal Random method
                            var method = typeof(TempDataManager).GetMethod("Random",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                                | System.Reflection.BindingFlags.Public);
                            if (method != null)
                            {
                                method.Invoke(tdm, new object[] { seed.Value });
                                changes.Add($"seed set to {seed.Value}");
                            }
                            else
                            {
                                changes.Add("TempDataManager.Random method not found via reflection");
                            }
                        }
                        else
                        {
                            changes.Add("TempDataManager.Instance is null");
                        }
                    }
                    catch (Exception ex)
                    {
                        changes.Add($"seed error: {ex.Message}");
                    }
                }

                if (forceRng.HasValue)
                {
                    try
                    {
                        // Force the next Dice roll by injecting a seed pool value
                        // This uses the Dice cursor system - we force a specific float
                        var diceType = typeof(Dice);
                        var defaultField = diceType.GetField("Default",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (defaultField != null)
                        {
                            var defaultDice = defaultField.GetValue(null);
                            if (defaultDice != null)
                            {
                                var cursorField = diceType.GetField("_cursor",
                                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                                var poolField = typeof(TempDataManager).GetField("seeds",
                                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                                    | System.Reflection.BindingFlags.Public);

                                if (poolField != null)
                                {
                                    var tdm = Singleton<TempDataManager>.Instance;
                                    if (tdm != null)
                                    {
                                        var pool = poolField.GetValue(tdm) as float[];
                                        if (pool != null && cursorField != null)
                                        {
                                            var cursor = cursorField.GetValue(defaultDice);
                                            if (cursor is ValueType vt)
                                            {
                                                var valField = vt.GetType().GetField("val",
                                                    System.Reflection.BindingFlags.Public
                                                    | System.Reflection.BindingFlags.NonPublic
                                                    | System.Reflection.BindingFlags.Instance);
                                                if (valField != null)
                                                {
                                                    int cursorVal = (int)valField.GetValue(vt);
                                                    if (cursorVal >= 0 && cursorVal < pool.Length)
                                                    {
                                                        pool[cursorVal] = (float)forceRng.Value;
                                                        changes.Add($"forceRng: injected {forceRng.Value} at cursor {cursorVal}");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        changes.Add($"forceRng error: {ex.Message}");
                    }
                }

                result["result"] = changes.Count > 0 ? "success" : "no_action";
                result["changes"] = changes;
                return (JToken)result;
            });
        }
    }
}
