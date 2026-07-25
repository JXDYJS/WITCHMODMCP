using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Witch.UI;
using Witch.UI.Automation;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class PlayCardTool : IMcpTool
    {
        public string Name => "play_card";
        public string Description => "打出手牌中的一张卡。支持按 index、cardId 识别。攻击卡可指定 targetIndex。如果出牌后触发选牌模态，可用 choices 自动处理。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["cardId"] = new JObject { ["type"] = "string", ["description"] = "卡ID，如 card_1（与 index 二选一）" },
                ["index"] = new JObject { ["type"] = "integer", ["description"] = "手牌位置(0-based)" },
                ["targetIndex"] = new JObject { ["type"] = "integer", ["description"] = "攻击目标敌人索引（可选）" },
                ["choices"] = new JObject
                {
                    ["type"] = "object",
                    ["description"] = "出牌后的模态选择（如弃牌、发现等）",
                    ["properties"] = new JObject
                    {
                        ["discardIndices"] = new JObject
                        {
                            ["type"] = "array",
                            ["description"] = "需要弃掉的手牌索引",
                            ["items"] = new JObject { ["type"] = "integer" }
                        },
                        ["selectIndices"] = new JObject
                        {
                            ["type"] = "array",
                            ["description"] = "选择模式中要选的手牌索引",
                            ["items"] = new JObject { ["type"] = "integer" }
                        },
                        ["autoConfirm"] = new JObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "如果出现选择界面，是否自动确认（默认 true）"
                        },
                        ["autoSelectFirst"] = new JObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "如果出现选择界面，是否自动选第一张可用卡（默认 false）"
                        }
                    }
                }
            }
        };

        public async Task<JToken> Execute(JToken args)
        {
            int? index = args?["index"]?.Value<int>();
            string cardId = args?["cardId"]?.Value<string>();
            int? targetIndex = args?["targetIndex"]?.Value<int>();

            if (!index.HasValue && string.IsNullOrEmpty(cardId))
                throw new ArgumentException("请提供 index 或 cardId 之一");

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                if (!RuntimeBattleAutomationService.TryGetContext(out var fightUi, out var error))
                {
                    result["result"] = "error";
                    result["message"] = error ?? "FightUI 不可用，无法出牌";
                    return (JToken)result;
                }

                var cards = FightUI.cardItemList?
                    .Where(c => c != null && c.gameObject != null && c.gameObject.activeInHierarchy)
                    .ToList();

                if (cards == null || cards.Count == 0)
                {
                    result["result"] = "error";
                    result["message"] = "手牌中没有可用卡牌";
                    return (JToken)result;
                }

                // Resolve card
                CardItem card = null;
                if (!string.IsNullOrEmpty(cardId))
                    card = cards.FirstOrDefault(c => c.dataConfig?.data != null
                        && c.dataConfig.data.TryGetValue("Id", out var id) && id == cardId);
                if (card == null && index.HasValue && index.Value >= 0 && index.Value < cards.Count)
                    card = cards[index.Value];
                if (card == null)
                {
                    result["result"] = "error";
                    result["message"] = "未找到匹配的卡牌";
                    return (JToken)result;
                }

                // Legality check (inline the key checks)
                if (!card.gameObject.activeInHierarchy || card.hasUse || !card.enabled || !CardItem.canUse)
                {
                    result["result"] = "error";
                    result["message"] = "该卡牌当前不可使用";
                    result["cardId"] = cardId;
                    return (JToken)result;
                }

                // Resolve target
                StatusManager targetStatus = null;
                if (targetIndex.HasValue && EnemyManager.Instance?.enemyList != null)
                {
                    if (targetIndex.Value >= 0 && targetIndex.Value < EnemyManager.Instance.enemyList.Count)
                        targetStatus = EnemyManager.Instance.enemyList[targetIndex.Value]?.Status as StatusManager;
                }

                int? hpBefore = targetStatus?.CurHp;
                int handBefore = FightUI.cardItemList?.Count ?? 0;

                try
                {
                    if (card is AttackCardItem attackCard)
                    {
                        if (targetStatus == null)
                        {
                            targetStatus = EnemyManager.Instance?.enemyList?
                                .FirstOrDefault()?.Status as StatusManager;
                        }
                        if (targetStatus == null)
                        {
                            result["result"] = "error";
                            result["message"] = "攻击卡需要一个有效目标";
                            return (JToken)result;
                        }
                        // Set target via reflection (same as RuntimeBattleAutomationService)
                        attackCard.scriptExecutor.Target = targetStatus;
                        attackCard.scriptExecutor.Object.Clear();
                        attackCard.scriptExecutor.Object.Add(targetStatus);
                        typeof(AttackCardItem).GetField("hitEnemy",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                            ?.SetValue(attackCard, targetStatus);
                        var lineProp = typeof(AttackCardItem).GetField("<isLine>k__BackingField",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        if (lineProp != null) lineProp.SetValue(attackCard, false);

                        attackCard.TrueUse();
                    }
                    else if (card is CommonCardItem commonCard)
                    {
                        if (targetStatus != null)
                            commonCard.dataConfig.scriptExecutor.Target = targetStatus;
                        commonCard.TrueUse();
                    }
                    else
                    {
                        result["result"] = "error";
                        result["message"] = $"不支持的卡牌类型: {card.GetType().Name}";
                        return (JToken)result;
                    }

                    result["result"] = "success";
                    result["message"] = "出牌成功";
                    result["cardId"] = card.dataConfig?.data?.GetValueOrDefault("Id", "");
                    result["handBefore"] = handBefore;
                    result["handAfter"] = FightUI.cardItemList?.Count;
                    if (targetStatus != null)
                    {
                        result["targetHpBefore"] = hpBefore;
                        result["targetHpAfter"] = targetStatus.CurHp;
                        result["targetIndex"] = targetIndex;
                    }

                    // --- Handle post-play choices ---
                    var choices = (args is JObject jo) ? jo["choices"] : null;
                    if (choices != null && choices.Type != JTokenType.Null)
                    {
                        bool autoConfirm = choices["autoConfirm"]?.Value<bool>() ?? true;
                        bool autoSelectFirst = choices["autoSelectFirst"]?.Value<bool>() ?? false;

                        // Handle discard/select choices via SelectedCard manipulation
                        var discardIndices = choices["discardIndices"];
                        var selectIndices = choices["selectIndices"];
                        if (discardIndices != null || selectIndices != null)
                        {
                            var clickIndices = discardIndices ?? selectIndices;
                            foreach (var di in clickIndices)
                            {
                                int idx = di.Value<int>();
                                if (idx >= 0 && idx < FightUI.cardItemList.Count)
                                {
                                    var ci = FightUI.cardItemList[idx];
                                    if (ci != null && !ci.hasUse && ci.gameObject.activeInHierarchy)
                                    {
                                        if (!FightUI.SelectedCard.Contains(ci))
                                        {
                                            FightUI.SelectedCard.Add(ci);
                                        }
                                    }
                                }
                            }
                            if (discardIndices is JArray da)
                                result["discardedCount"] = da.Count;
                        }

                        // Auto confirm if in selection mode
                        if (autoConfirm && FightUI.InIEn)
                        {
                            if (autoSelectFirst && FightUI.SelectedCard.Count == 0)
                            {
                                var firstValid = FightUI.cardItemList
                                    .FirstOrDefault(ci => ci != null && !ci.hasUse && ci.gameObject.activeInHierarchy);
                                if (firstValid != null)
                                {
                                    FightUI.SelectedCard.Add(firstValid);
                                }
                            }
                            // Confirm selection via FightUI.Yes()
                            if (FightUI.SelectedCard.Count > 0)
                            {
                                var yesMethod = fightUi.GetType().GetMethod("Yes",
                                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                                    | System.Reflection.BindingFlags.Public);
                                if (yesMethod != null)
                                {
                                    yesMethod.Invoke(fightUi, null);
                                    result["autoConfirmed"] = true;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    result["result"] = "error";
                    result["message"] = $"出牌失败: {ex.Message}";
                    result["cardId"] = card.dataConfig?.data?.GetValueOrDefault("Id", "");
                }

                return (JToken)result;
            });
        }
    }
}
