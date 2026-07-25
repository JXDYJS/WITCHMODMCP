using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Witch;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class UseSkillTool : IMcpTool
    {
        public string Name => "use_skill";
        public string Description => "释放角色技能（Skill1/Skill2）。需在战斗中且为玩家回合。支持指定目标索引，可选强制冷却或重置冷却。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["index"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "技能索引: 1 或 2"
                },
                ["targetIndex"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "目标敌人索引（可选，对需选目标的技能有效；不传则选第一个敌人）"
                },
                ["ignoreCooldown"] = new JObject
                {
                    ["type"] = "boolean",
                    ["description"] = "是否无视冷却强制释放（默认 true）"
                },
                ["setCooldown"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "释放后设置冷却为指定值（可选，默认由技能脚本决定）"
                }
            },
            ["required"] = new JArray { "index" }
        };

        public async Task<JToken> Execute(JToken args)
        {
            int index = args?["index"]?.Value<int>() ?? 0;
            int? targetIndex = args?["targetIndex"]?.Value<int>();
            bool ignoreCooldown = args?["ignoreCooldown"]?.Value<bool>() ?? true;
            int? setCooldown = args?["setCooldown"]?.Value<int>();

            if (index != 1 && index != 2)
                throw new ArgumentException("index 必须为 1 或 2");

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                // Check in fight
                bool inFight = FightManager.Instance != null
                    && FightManager.Instance.fightType != FightType.None;
                if (!inFight)
                {
                    result["result"] = "error";
                    result["message"] = "当前不在战斗中";
                    return (JToken)result;
                }

                // Check player turn
                if (FightManager.Instance.fightType != FightType.Player)
                {
                    result["result"] = "error";
                    result["message"] = "当前不是玩家回合";
                    return (JToken)result;
                }

                // Check career
                if (RoleTable.Instance?.Career == null)
                {
                    result["result"] = "error";
                    result["message"] = "角色数据未加载";
                    return (JToken)result;
                }

                // Get skill runtime ID from career data
                string skillKey = "Skill" + index;
                if (!RoleTable.Instance.Career.data.TryGetValue(skillKey, out var skillRuntimeId)
                    || string.IsNullOrEmpty(skillRuntimeId))
                {
                    result["result"] = "error";
                    result["message"] = $"技能 {index} 不存在（Career 中无 {skillKey}）";
                    return (JToken)result;
                }

                // Look up the skill card DataConfig
                if (!Globals.DataConfigCache.TryGetValue(skillRuntimeId, out var skillConfig))
                {
                    result["result"] = "error";
                    result["message"] = $"技能卡牌数据未找到: {skillRuntimeId}";
                    return (JToken)result;
                }

                // Check rawId for cooldown
                string rawId = skillConfig.data.GetValueOrDefault("Id", "");
                result["skillRuntimeId"] = skillRuntimeId;
                result["skillRawId"] = rawId;
                result["skillName"] = skillConfig.data.GetValueOrDefault("Name", "");

                // Check cooldown
                if (!ignoreCooldown && !string.IsNullOrEmpty(rawId)
                    && RoleTable.Instance.SkillTime != null
                    && RoleTable.Instance.SkillTime.ContainsKey(rawId)
                    && RoleTable.Instance.SkillTime[rawId] > 0)
                {
                    result["result"] = "error";
                    result["message"] = $"技能冷却中（剩余 {RoleTable.Instance.SkillTime[rawId]} 回合）";
                    result["cooldown"] = RoleTable.Instance.SkillTime[rawId];
                    return (JToken)result;
                }

                try
                {
                    // Get ScriptExecutor from the skill card's DataConfig
                    var executor = skillConfig.scriptExecutor;

                    // Set Self to player
                    if (FightPlayer.Instance?.Status != null)
                    {
                        executor.Self = FightPlayer.Instance.Status;
                        executor.Object.Clear();
                        executor.Object.Add(FightPlayer.Instance.Status);
                    }

                    // Set Target
                    IStatusManager targetStatus = null;
                    if (targetIndex.HasValue && EnemyManager.Instance?.enemyList != null)
                    {
                        if (targetIndex.Value >= 0 && targetIndex.Value < EnemyManager.Instance.enemyList.Count)
                            targetStatus = EnemyManager.Instance.enemyList[targetIndex.Value]?.Status;
                    }
                    else if (EnemyManager.Instance?.enemyList != null && EnemyManager.Instance.enemyList.Count > 0)
                    {
                        targetStatus = EnemyManager.Instance.enemyList[0]?.Status;
                    }

                    if (targetStatus != null)
                    {
                        executor.Target = targetStatus;
                        executor.Object.Add(targetStatus);
                        result["targetIndex"] = targetIndex ?? 0;
                    }

                    // Execute skill
                    executor.RunScript("UseScript");
                    executor.Self?.PlayVocal(IStatusManager.VocalState.Skill);

                    // Run InitScript to refresh display data
                    executor.RunScript("InitScript");

                    // Update buffs and UI
                    FightPlayer.Instance?.Status?.UpdateBuff();

                    var fightUi = UIManager.Instance?.GetUI<FightUI>("FightUI");
                    fightUi?.CallActionAnimation(executor);

                    // Update cooldown
                    if (setCooldown.HasValue && !string.IsNullOrEmpty(rawId)
                        && RoleTable.Instance.SkillTime != null)
                    {
                        RoleTable.Instance.SkillTime[rawId] = setCooldown.Value;
                    }

                    // Refresh skill UI cooldown display
                    fightUi?.UpdateSkill();

                    result["result"] = "success";
                    result["message"] = $"技能 {index} 释放成功";

                    // Return state after skill use
                    if (FightPlayer.Instance?.Status != null)
                    {
                        var p = new JObject();
                        p["hp"] = FightPlayer.Instance.Status.CurHp;
                        p["maxHp"] = FightPlayer.Instance.Status.MaxHp;
                        p["shield"] = FightPlayer.Instance.Status.Defend;
                        p["power"] = FightPlayer.Instance.CurPowerCount;
                        result["player"] = p;
                    }
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"技能释放失败: {ex.Message}";
                }

                return (JToken)result;
            });
        }
    }
}
