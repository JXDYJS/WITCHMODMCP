using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class SetFightEntityTool : IMcpTool
    {
        public string Name => "set_fight_entity";
        public string Description => "修改战斗中实体（玩家/敌人）的属性：HP、盾、能量、Buff。target为 \"player\" 或敌人索引(0开始)。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["target"] = new JObject { ["type"] = "string", ["description"] = "\"player\" 或敌人索引(0开始)" },
                ["hp"] = new JObject { ["type"] = "integer", ["description"] = "设置当前 HP" },
                ["maxHp"] = new JObject { ["type"] = "integer", ["description"] = "设置最大 HP" },
                ["shield"] = new JObject { ["type"] = "integer", ["description"] = "设置护盾值" },
                ["power"] = new JObject { ["type"] = "integer", ["description"] = "仅玩家有效" },
                ["maxPower"] = new JObject { ["type"] = "integer", ["description"] = "仅玩家有效" },
                ["addBuffs"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["id"] = new JObject { ["type"] = "string" },
                            ["level"] = new JObject { ["type"] = "integer" }
                        }
                    }
                },
                ["removeBuffs"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" },
                    ["description"] = "要移除的 Buff ID 列表，如 [\"buff_vulnerability\", \"buff_burn\"]"
                },
                ["clearBuffs"] = new JObject { ["type"] = "boolean", ["description"] = "是否清除所有 Buff" }
            },
            ["required"] = new JArray { "target" }
        };

        public async Task<JToken> Execute(JToken args)
        {
            string target = args?["target"]?.Value<string>();
            if (string.IsNullOrEmpty(target))
                throw new ArgumentException("target 参数不能为空");

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();
                var changes = new JArray();

                bool inFight = FightManager.Instance != null
                    && FightManager.Instance.fightType != FightType.None;
                if (!inFight)
                {
                    result["result"] = "error";
                    result["message"] = "当前不在战斗中";
                    return (JToken)result;
                }

                var status = ResolveStatus(target);
                if (status == null)
                {
                    result["result"] = "error";
                    result["message"] = $"目标 '{target}' 不存在";
                    return (JToken)result;
                }

                bool isPlayer = target == "player";

                // HP
                if (args["hp"] != null)
                {
                    int v = args["hp"].Value<int>();
                    var old = status.CurHp;
                    status.CurHp = v;
                    changes.Add($"hp: {old} -> {v}");
                }

                // MaxHp
                if (args["maxHp"] != null)
                {
                    int v = args["maxHp"].Value<int>();
                    var old = status.MaxHp;
                    status.MaxHp = v;
                    changes.Add($"maxHp: {old} -> {v}");
                }

                // Shield (defend)
                if (args["shield"] != null && status is StatusManager sm)
                {
                    int v = args["shield"].Value<int>();
                    int old = sm.defend;
                    sm.defend = v;
                    changes.Add($"shield: {old} -> {v}");
                }

                // Power (player only)
                if (isPlayer && args["power"] != null)
                {
                    int v = args["power"].Value<int>();
                    var old = FightPlayer.Instance.CurPowerCount;
                    FightPlayer.Instance.CurPowerCount = v;
                    changes.Add($"power: {old} -> {v}");
                }

                // MaxPower (player only)
                if (isPlayer && args["maxPower"] != null)
                {
                    int v = args["maxPower"].Value<int>();
                    var old = FightPlayer.Instance.MaxPowerCount;
                    FightPlayer.Instance.MaxPowerCount = v;
                    changes.Add($"maxPower: {old} -> {v}");
                }

                // Add buffs
                if (args["addBuffs"] is JArray addBuffsArr)
                {
                    foreach (var b in addBuffsArr)
                    {
                        string id = b["id"]?.Value<string>();
                        int level = b["level"]?.Value<int>() ?? 1;
                        if (!string.IsNullOrEmpty(id))
                        {
                            try
                            {
                                status.AddBuff(id, level);
                                changes.Add($"addBuff: {id} lv{level}");
                            }
                            catch (Exception ex)
                            {
                                changes.Add($"addBuff error ({id}): {ex.Message}");
                            }
                        }
                    }
                }

                // Remove buffs
                if (args["removeBuffs"] is JArray removeBuffsArr)
                {
                    foreach (var id in removeBuffsArr)
                    {
                        string bid = id?.Value<string>();
                        if (!string.IsNullOrEmpty(bid))
                        {
                            try
                            {
                                status.RemoveBuff(bid);
                                changes.Add($"removeBuff: {bid}");
                            }
                            catch (Exception ex)
                            {
                                changes.Add($"removeBuff error ({bid}): {ex.Message}");
                            }
                        }
                    }
                }

                // Clear buffs
                if (args["clearBuffs"] is JToken clearB && clearB.Type == JTokenType.Boolean && clearB.Value<bool>())
                {
                    try { status.ClearAllBuff(); changes.Add("clearBuffs: true"); }
                    catch (Exception ex) { changes.Add($"clearBuffs error: {ex.Message}"); }
                }

                result["result"] = "success";
                result["changes"] = changes;
                return (JToken)result;
            });
        }

        private static IStatusManager ResolveStatus(string target)
        {
            if (target == "player")
            {
                if (FightPlayer.Instance?.Status != null)
                    return FightPlayer.Instance.Status;

                // Fallback not available in debug fights; return null
                return null;
            }

            if (int.TryParse(target, out int idx) && EnemyManager.Instance?.enemyList != null)
            {
                if (idx >= 0 && idx < EnemyManager.Instance.enemyList.Count)
                    return EnemyManager.Instance.enemyList[idx]?.Status;
            }

            return null;
        }
    }
}
