using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Events;
using Witch.UI;
using Witch.UI.Window;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class GetDeckSelectionTool : IMcpTool
    {
        public string Name => "get_deck_selection";
        public string Description => "获取当前 DeckUI 选牌界面的可选卡牌列表，包含每张牌的 ID、名称、索引等信息。";
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

                var deckUI = UIManager.Instance?.GetUI<DeckUI>("DeckUI");
                if (deckUI == null || !deckUI.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "当前没有打开的 DeckUI 选牌界面";
                    return (JToken)result;
                }

                var cardList = CollectDeckCardInfos(deckUI.transform);
                if (cardList.Count == 0)
                {
                    result["result"] = "error";
                    result["message"] = "DeckUI 中没有找到可选卡牌";
                    return (JToken)result;
                }

                result["result"] = "success";
                result["totalCards"] = cardList.Count;
                result["cards"] = JArray.FromObject(cardList);

                return (JToken)result;
            });
        }

        private static List<JObject> CollectDeckCardInfos(Transform deckUI)
        {
            var results = new List<JObject>();

            Transform listRoot = FindCardList(deckUI);
            if (listRoot == null) return results;

            int index = 0;
            foreach (Transform child in listRoot)
            {
                if (!child.gameObject.activeSelf) continue;
                if (child.name == "card" && child.GetSiblingIndex() == 0) continue;

                var info = ReadCardInfo(child, index);
                if (info != null)
                {
                    results.Add(info);
                }
                index++;
            }

            return results;
        }

        private static Transform FindCardList(Transform deckUI)
        {
            var list = deckUI.Find("Window Manager/Windows/新牌堆/Content/List View Custom/Scroll Area/List");
            if (list != null) return list;

            list = deckUI.Find("Window Manager/Windows/抽牌堆/Content/List View Custom/Scroll Area/List");
            if (list != null) return list;

            list = deckUI.Find("Window Manager/Windows/弃牌堆/Content/List View Custom/Scroll Area/List");
            return list;
        }

        private static JObject ReadCardInfo(Transform cardItem, int index)
        {
            try
            {
                var displayCard = cardItem.GetComponentInChildren<DisplayCard>(false);
                if (displayCard == null) return null;

                var info = new JObject();
                info["index"] = index;

                if (displayCard.dataConfig?.data != null)
                {
                    var data = displayCard.dataConfig.data;
                    info["cardId"] = data.GetValueOrDefault("Id", "");
                    info["name"] = data.GetValueOrDefault("Name", "");
                    info["cost"] = data.GetValueOrDefault("Cost", "0");
                    info["rarity"] = data.GetValueOrDefault("Rarity", "");
                    info["tag"] = data.GetValueOrDefault("Tag", "");
                }
                else
                {
                    info["cardId"] = "";
                    info["name"] = cardItem.name;
                }

                info["isSelected"] = displayCard.isSelect;

                return info;
            }
            catch
            {
                return null;
            }
        }
    }

    public class SelectDeckCardsTool : IMcpTool
    {
        public string Name => "select_deck_cards";
        public string Description => "在 DeckUI 选牌界面中选择一张或多张卡牌（点击切换选中状态）。index 从 0 开始。当选中数量达到要求后界面会自动关闭。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["indices"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "integer" },
                    ["description"] = "要选中的卡牌索引列表（0-based），如 [0, 2] 表示选第1张和第3张"
                }
            },
            ["required"] = new JArray { "indices" }
        };

        private static readonly BindingFlags _publicInstance = BindingFlags.Public | BindingFlags.Instance;

        public async Task<JToken> Execute(JToken args)
        {
            var indicesToken = args?["indices"];
            if (indicesToken == null || !indicesToken.HasValues)
                throw new ArgumentException("indices 不能为空");

            var indices = indicesToken.Select(t => t.Value<int>()).Distinct().OrderBy(x => x).ToList();

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                var deckUI = UIManager.Instance?.GetUI<DeckUI>("DeckUI");
                if (deckUI == null || !deckUI.gameObject.activeInHierarchy)
                {
                    result["result"] = "error";
                    result["message"] = "当前没有打开的 DeckUI 选牌界面";
                    return (JToken)result;
                }

                var clickableCards = CollectClickableCards(deckUI.transform);

                if (clickableCards.Count == 0)
                {
                    result["result"] = "error";
                    result["message"] = "DeckUI 中没有找到可选卡牌";
                    return (JToken)result;
                }

                var clicked = new List<int>();
                var errors = new List<string>();

                foreach (var idx in indices)
                {
                    if (idx < 0 || idx >= clickableCards.Count)
                    {
                        errors.Add($"索引 {idx} 超出范围（0-{clickableCards.Count - 1}）");
                        continue;
                    }

                    var (comp, cardName) = clickableCards[idx];
                    try
                    {
                        if (TryInvokeButtonManagerClick(comp))
                        {
                            clicked.Add(idx);
                        }
                        else
                        {
                            errors.Add($"索引 {idx} ({cardName}) 无法触发点击");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"索引 {idx} 点击失败: {ex.Message}");
                    }
                }

                result["result"] = errors.Count == 0 ? "success" : "partial";
                result["clicked"] = JArray.FromObject(clicked);
                result["clickedCount"] = clicked.Count;

                if (errors.Count > 0)
                {
                    result["errors"] = JArray.FromObject(errors);
                    result["message"] = string.Join("; ", errors);
                }
                else
                {
                    result["message"] = $"已点击 {clicked.Count} 张卡牌";
                }

                return (JToken)result;
            });
        }

        private static List<(UnityEngine.Component comp, string name)> CollectClickableCards(Transform deckUI)
        {
            var results = new List<(UnityEngine.Component, string)>();

            Transform listRoot = FindCardList(deckUI);
            if (listRoot == null) return results;

            foreach (Transform child in listRoot)
            {
                if (!child.gameObject.activeSelf) continue;
                if (child.name == "card" && child.GetSiblingIndex() == 0) continue;

                var monos = child.GetComponentsInChildren<MonoBehaviour>(false);
                foreach (var comp in monos)
                {
                    if (comp == null) continue;
                    var type = comp.GetType();
                    if (type.Name != "ButtonManager") continue;

                    var interactField = type.GetField("isInteractable", _publicInstance);
                    bool interactable = interactField == null || (bool)interactField.GetValue(comp);
                    if (!interactable) continue;

                    var displayCard = child.GetComponentInChildren<DisplayCard>(false);
                    var cardName = displayCard?.dataConfig?.data.GetValueOrDefault("Name", "") ?? child.name;
                    results.Add((comp, cardName));
                    break;
                }
            }

            return results;
        }

        private static Transform FindCardList(Transform deckUI)
        {
            var list = deckUI.Find("Window Manager/Windows/新牌堆/Content/List View Custom/Scroll Area/List");
            if (list != null) return list;

            list = deckUI.Find("Window Manager/Windows/抽牌堆/Content/List View Custom/Scroll Area/List");
            if (list != null) return list;

            list = deckUI.Find("Window Manager/Windows/弃牌堆/Content/List View Custom/Scroll Area/List");
            return list;
        }

        private static bool TryInvokeButtonManagerClick(UnityEngine.Component comp)
        {
            var type = comp.GetType();
            if (type.Name != "ButtonManager") return false;

            var onClickField = type.GetField("onClick", _publicInstance);
            if (onClickField?.GetValue(comp) is UnityEvent onClick)
            {
                onClick.Invoke();
                return true;
            }
            return false;
        }
    }
}
