using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class GetSkillsStateTool : IMcpTool
    {
        public string Name => "get_skills_state";
        public string Description => "获取当前角色的技能状态（Skill1/Skill2 的运行时 ID、冷却、是否可用）。无需在战斗中也能查看职业技能信息。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
        };

        public async Task<JToken> Execute(JToken args)
        {
            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                if (RoleTable.Instance?.Career == null)
                {
                    result["result"] = "error";
                    result["message"] = "角色数据未加载（未开始跑局或无职业）";
                    return (JToken)result;
                }

                var careerData = RoleTable.Instance.Career.data;
                var skillTime = RoleTable.Instance.SkillTime;

                result["careerId"] = careerData.GetValueOrDefault("Id", "");
                result["careerName"] = careerData.GetValueOrDefault("Name", "");

                bool inFight = FightManager.Instance != null
                    && FightManager.Instance.fightType != FightType.None;
                result["inFight"] = inFight;

                var skills = new JArray();
                for (int i = 1; i <= 2; i++)
                {
                    string key = "Skill" + i;
                    if (!careerData.TryGetValue(key, out var skillRuntimeId) || string.IsNullOrEmpty(skillRuntimeId))
                        continue;

                    var skillObj = new JObject();
                    skillObj["index"] = i;
                    skillObj["runtimeId"] = skillRuntimeId;
                    skillObj["name"] = "";
                    skillObj["rawId"] = "";
                    skillObj["cooldown"] = 0;
                    skillObj["canUse"] = false;
                    skillObj["actionImage"] = careerData.GetValueOrDefault("ActionImage" + i, "");

                    if (Globals.DataConfigCache.TryGetValue(skillRuntimeId, out var config))
                    {
                        skillObj["rawId"] = config.data.GetValueOrDefault("Id", "");
                        skillObj["name"] = config.data.GetValueOrDefault("Name", "");

                        string rawId = config.data.GetValueOrDefault("Id", "");
                        if (!string.IsNullOrEmpty(rawId) && skillTime != null)
                        {
                            int cd = skillTime.ContainsKey(rawId) ? skillTime[rawId] : 0;
                            skillObj["cooldown"] = cd;
                            skillObj["canUse"] = cd <= 0;
                        }
                    }

                    skills.Add(skillObj);
                }

                result["result"] = "success";
                result["skills"] = skills;
                result["skillCount"] = skills.Count;
                return (JToken)result;
            });
        }
    }
}
