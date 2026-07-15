using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class SearchConfigTool : IMcpTool
    {
        public string Name => "search_config";
        public string Description => "在游戏 DataConfigCache 中按关键词模糊搜索配置条目。用于快速查找卡牌、Buff、卡包、遗物等所有已加载内容的运行时 ID，是 query_config 的补充（query_config 查 _tables 表，search_config 查 DataConfigCache 全量条目）。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["pattern"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "搜索关键词（忽略大小写）。匹配 DataConfigCache 的 key 和 NativeIds"
                },
                ["limit"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "最多返回条数，默认 20",
                    ["default"] = 20
                },
                ["includeFields"] = new JObject
                {
                    ["type"] = "boolean",
                    ["description"] = "是否额外返回匹配条目的核心字段（Id/Name/Type/Rarity/Expend 等），默认 false。设为 true 会略微增加耗时",
                    ["default"] = false
                },
                ["searchNativeIds"] = new JObject
                {
                    ["type"] = "boolean",
                    ["description"] = "是否同时搜索 NativeIds（游戏原生 ID 注册表），默认 true。NativeIds 包含 buff_regenerate 等内置 ID",
                    ["default"] = true
                }
            },
            ["required"] = new JArray { "pattern" }
        };

        public Task<JToken> Execute(JToken args)
        {
            string pattern = args?["pattern"]?.Value<string>() ?? "";
            int limit = args?["limit"]?.Value<int>() ?? 20;
            bool includeFields = args?["includeFields"]?.Value<bool>() ?? false;
            bool searchNativeIds = args?["searchNativeIds"]?.Value<bool>() ?? true;

            return GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                var mgrType = FindType("GameConfigManager");
                if (mgrType == null)
                {
                    result["error"] = "找不到 GameConfigManager 类型";
                    return (JToken)result;
                }

                object mgrInstance = GetStaticPropertyOrField(mgrType, "Instance")
                    ?? GetStaticPropertyOrField(mgrType, "instance");
                if (mgrInstance == null)
                {
                    result["error"] = "无法获取 GameConfigManager 实例";
                    return (JToken)result;
                }

                result["searchPattern"] = pattern;

                object cacheObj = GetMemberValueByName(mgrInstance, mgrType, "DataConfigCache");
                if (cacheObj == null)
                {
                    result["error"] = "找不到 DataConfigCache";
                    return (JToken)result;
                }

                int totalCacheSize = 0;
                var matchedKeys = new JArray();
                int realMatchCount = 0;

                if (cacheObj is IDictionary cacheDict)
                {
                    totalCacheSize = cacheDict.Count;
                    var allKeys = new List<string>();
                    foreach (object key in cacheDict.Keys)
                        allKeys.Add(key?.ToString() ?? "");

                    foreach (string key in allKeys)
                    {
                        if (string.IsNullOrEmpty(pattern) ||
                            key.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (realMatchCount >= limit)
                            {
                                matchedKeys.Add("... (truncated)");
                                break;
                            }

                            if (includeFields)
                            {
                                var entry = new JObject();
                                entry["_key"] = key;

                                try
                                {
                                    object val = cacheDict[key];
                                    if (val != null)
                                    {
                                        object dataDict = GetMemberValueByName(val, val.GetType(), "data");
                                        if (dataDict is IDictionary dd)
                                        {
                                            int fieldCount = 0;
                                            foreach (string dk in dd.Keys)
                                            {
                                                if (fieldCount >= 8) break;
                                                string ddk = dk?.ToString() ?? "";
                                                string low = ddk.ToLower();
                                                if (low == "id" || low == "name" || low == "type" ||
                                                    low == "rarity" || low == "action" || low == "expend" ||
                                                    low == "packbelong" || low == "icon" ||
                                                    low == "basescript" || low == "script" ||
                                                    low == "note" || low == "description" ||
                                                    low == "initScript".ToLower())
                                                {
                                                    entry[ddk] = dd[ddk]?.ToString() ?? "";
                                                    fieldCount++;
                                                }
                                            }
                                            if (fieldCount == 0)
                                            {
                                                int i = 0;
                                                foreach (string dk in dd.Keys)
                                                {
                                                    if (i >= 3) break;
                                                    entry[dk?.ToString() ?? ""] = dd[dk]?.ToString() ?? "";
                                                    i++;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            entry["_type"] = val.GetType().Name;
                                        }
                                    }
                                }
                                catch { }

                                matchedKeys.Add(entry);
                            }
                            else
                            {
                                matchedKeys.Add(key);
                            }

                            realMatchCount++;
                        }
                    }
                }

                result["matchedKeys"] = matchedKeys;
                result["matchCount"] = realMatchCount;
                result["totalCacheSize"] = totalCacheSize;

                if (searchNativeIds)
                {
                    object nativeObj = GetMemberValueByName(mgrInstance, mgrType, "NativeIds");
                    if (nativeObj is IEnumerable nativeEnum)
                    {
                        var matchedNative = new JArray();
                        int nativeMatchCount = 0;
                        int totalNative = 0;

                        MethodInfo countProp = nativeObj.GetType().GetProperty("Count")?.GetMethod;
                        if (countProp != null)
                            totalNative = (int)countProp.Invoke(nativeObj, null);

                        foreach (object id in nativeEnum)
                        {
                            string idStr = id?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(pattern) &&
                                idStr.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                if (nativeMatchCount >= limit)
                                {
                                    matchedNative.Add("... (truncated)");
                                    break;
                                }
                                matchedNative.Add(idStr);
                                nativeMatchCount++;
                            }
                        }

                        result["nativeIdMatches"] = matchedNative;
                        result["nativeIdMatchCount"] = nativeMatchCount;
                        result["totalNativeIds"] = totalNative;
                    }
                }

                result["hint"] = "匹配的是运行时 ID（如 ModFolder_CsvFile_RawId）。用 give_item 注入测试或用 query_config 进一步查看详情。";

                return (JToken)result;
            });
        }

        private static object GetMemberValueByName(object instance, Type type, string name)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var fi in type.GetFields(flags))
            {
                if (string.Equals(fi.Name, name, StringComparison.OrdinalIgnoreCase))
                    return fi.GetValue(instance);
            }
            foreach (var pi in type.GetProperties(flags))
            {
                if (string.Equals(pi.Name, name, StringComparison.OrdinalIgnoreCase) && pi.GetIndexParameters().Length == 0)
                    return pi.GetValue(instance, null);
            }
            return null;
        }

        private static object GetStaticPropertyOrField(Type type, string name)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            var pi = type.GetProperty(name, flags);
            if (pi != null) return pi.GetValue(null);
            var fi = type.GetField(name, flags);
            if (fi != null) return fi.GetValue(null);
            return null;
        }

        private static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == name || (t.FullName != null && t.FullName.EndsWith("." + name)))
                            return t;
                    }
                }
                catch { }
            }
            return null;
        }
    }
}
