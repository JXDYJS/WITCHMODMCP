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
        public string Description => "修改战斗中实体（玩家/敌人）的属性：HP、盾、能量、Buff。支持 instanceId（推荐，从 get_fight_state 获取）或 target（\"player\" 或敌人索引，不推荐）。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["instanceId"] = new JObject { ["type"] = "integer", ["description"] = "（推荐）实体的运行时 instanceId，从 get_fight_state 的 player/enemies[].instanceId 获取（Unity Object.GetInstanceID）" },
                ["target"] = new JObject { ["type"] = "string", ["description"] = "（不推荐）\"player\" 或敌人索引(0开始)" },
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
            }
        };

        public async Task<JToken> Execute(JToken args)
        {
            int? instanceId = args?["instanceId"]?.Value<int>();
            string target = args?["target"]?.Value<string>();

            if (!instanceId.HasValue && string.IsNullOrEmpty(target))
                throw new ArgumentException("需要提供 instanceId 或 target 参数之一");

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

                bool isPlayer = false;
                IStatusManager status;

                if (instanceId.HasValue)
                {
                    (status, isPlayer) = ResolveByInstanceId(instanceId.Value);
                }
                else
                {
                    status = ResolveStatus(target);
                    isPlayer = target == "player";
                }

                if (status == null)
                {
                    string hint = instanceId.HasValue ? $"instanceId={instanceId}" : $"target='{target}'";
                    result["result"] = "error";
                    result["message"] = $"目标 ({hint}) 不存在";
                    return (JToken)result;
                }

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
                return null;
            }

            if (int.TryParse(target, out int idx) && EnemyManager.Instance?.enemyList != null)
            {
                if (idx >= 0 && idx < EnemyManager.Instance.enemyList.Count)
                    return EnemyManager.Instance.enemyList[idx]?.Status;
            }

            return null;
        }

        private static (IStatusManager status, bool isPlayer) ResolveByInstanceId(int instanceId)
        {
            if (FightPlayer.Instance?.gameObject?.GetInstanceID() == instanceId)
            {
                if (FightPlayer.Instance.Status != null)
                    return (FightPlayer.Instance.Status, true);
            }

            if (EnemyManager.Instance?.enemyList != null)
            {
                foreach (var e in EnemyManager.Instance.enemyList)
                {
                    if (e?.gameObject?.GetInstanceID() == instanceId)
                    {
                        if (e.Status != null)
                            return (e.Status, false);
                    }
                }
            }

            return (null, false);
        }
    }
}
