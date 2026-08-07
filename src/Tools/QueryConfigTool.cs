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
    public class QueryConfigTool : IMcpTool
    {
        public string Name => "query_config";
        public string Description => "查询游戏配置表数据。可列出所有可用表名、查看表结构，或按 ID 查询具体条目";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["tableName"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "配置表名，如 Card、Event、Enemy。留空则列出所有可用配置表。注意：Career/Buff/Relic/Blessing 等数据在 DataConfigCache 中，请用 search_config 查询"
                },
                ["id"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "配置条目 ID。不填则返回表的前几条数据供预览"
                },
                ["limit"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "最多返回条数，默认 5",
                    ["default"] = 5
                }
            }
        };

        public Task<JToken> Execute(JToken args)
        {
            string tableName = args?["tableName"]?.Value<string>();
            int? id = args?["id"]?.Value<int?>();
            int limit = args?["limit"]?.Value<int>() ?? 5;

            return GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                var mgrType = FindType("GameConfigManager");
                if (mgrType == null)
                {
                    result["error"] = "找不到 GameConfigManager 类型";
                    return (JToken)result;
                }

                object mgrInstance = GetStaticPropertyOrField(mgrType, "Instance");
                if (mgrInstance == null)
                {
                    mgrInstance = GetStaticPropertyOrField(mgrType, "instance");
                }
                if (mgrInstance == null)
                {
                    var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new[] { typeof(Type) });
                    if (findMethod != null)
                    {
                        try { mgrInstance = findMethod.Invoke(null, new object[] { mgrType }); }
                        catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[QueryConfigTool] FindObjectOfType: {ex.Message}"); }
                    }
                }
                if (mgrInstance == null)
                {
                    result["error"] = "无法获取 GameConfigManager 实例（无 Instance/instance 静态属性，FindObjectOfType 也未找到）";
                    result["hint"] = "静态成员: " + string.Join(", ", mgrType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Take(10).Select(m => m.Name));
                    return (JToken)result;
                }

                result["mgrType"] = mgrType.FullName;

                if (string.IsNullOrEmpty(tableName))
                {
                    var tables = new JArray();
                    var visited = new HashSet<string>();

                    foreach (var fi in mgrType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        DumpMember(mgrInstance, fi, tables, visited);
                    }

                    foreach (var pi in mgrType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (pi.GetIndexParameters().Length > 0) continue;
                        DumpMember(mgrInstance, pi, tables, visited);
                    }

                    result["availableTables"] = tables;
                    result["hint"] = "用 tableName 指定其中一个 name 来查看表内容";
                    return (JToken)result;
                }

                object tableObj = GetMemberValueByName(mgrInstance, mgrType, tableName);
                if (tableObj == null)
                {
                    result["error"] = $"找不到配置表: {tableName}";
                    return (JToken)result;
                }

                result["tableName"] = tableName;
                result["tableType"] = tableObj.GetType().FullName ?? tableObj.GetType().Name;

                if (id.HasValue)
                {
                    var item = QueryById(tableObj, id.Value);
                    if (item != null)
                        result["item"] = SerializeObject(item, 3);
                    else
                        result["error"] = $"ID={id} 的记录不存在";
                }
                else
                {
                    var sampleItems = new JArray();
                    var enumerator = GetEnumerator(tableObj);
                    int count = 0;
                    if (enumerator != null)
                    {
                        while (enumerator.MoveNext() && count < limit)
                        {
                            sampleItems.Add(SerializeObject(enumerator.Current, 2));
                            count++;
                        }
                    }
                    else
                    {
                        result["note"] = "该成员不是可枚举的集合，已序列化为单值";
                        sampleItems.Add(SerializeObject(tableObj, 2));
                        count = 1;
                    }
                    result["sampleCount"] = count;
                    result["samples"] = sampleItems;

                    int total = TryCountItems(tableObj);
                    if (total >= 0) result["totalCount"] = total;
                }

                return (JToken)result;
            });
        }

        private static void DumpMember(object instance, MemberInfo member, JArray output, HashSet<string> visited)
        {
            string name = member.Name;
            if (visited.Contains(name)) return;
            visited.Add(name);

            try
            {
                object val = member is FieldInfo fi ? fi.GetValue(instance)
                    : member is PropertyInfo pi ? pi.GetValue(instance, null) : null;
                if (val == null) return;

                var info = new JObject
                {
                    ["name"] = name,
                    ["type"] = val.GetType().Name,
                    ["isField"] = member is FieldInfo
                };

                if (val is IDictionary dict)
                {
                    info["kind"] = "dictionary";
                    info["itemCount"] = dict.Count;
                    if (dict.Count > 0)
                    {
                        var sampleKeys = new JArray();
                        foreach (var key in dict.Keys)
                        {
                            sampleKeys.Add(key?.ToString() ?? "null");
                            if (sampleKeys.Count >= 8) break;
                        }
                        info["sampleKeys"] = sampleKeys;
                    }
                }
                else if (val is IEnumerable enumerable and not string)
                {
                    info["kind"] = "collection";
                    int itemCnt = TryCountItems(val);
                    info["itemCount"] = itemCnt;

                    var typeArgs = val.GetType().GetGenericArguments();
                    if (typeArgs.Length > 0)
                        info["elementType"] = typeArgs[0].Name;

                    if (typeArgs.Length == 2)
                        info["isKeyValuePair"] = true;

                    if (itemCnt < 0)
                    {
                        int c = 0;
                        foreach (var _ in enumerable) { c++; if (c > 10000) break; }
                        info["itemCount"] = c;
                    }
                }

                output.Add(info);
            }
            catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[QueryConfigTool] table info: {ex.Message}"); }
        }

        private static object GetMemberValueByName(object instance, Type type, string name)
        {
            foreach (var fi in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (string.Equals(fi.Name, name, StringComparison.OrdinalIgnoreCase))
                    return fi.GetValue(instance);
            }
            foreach (var pi in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (string.Equals(pi.Name, name, StringComparison.OrdinalIgnoreCase) && pi.GetIndexParameters().Length == 0)
                    return pi.GetValue(instance, null);
            }
            return null;
        }

        private static object GetStaticPropertyOrField(Type type, string name)
        {
            var pi = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (pi != null) return pi.GetValue(null);

            var fi = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (fi != null) return fi.GetValue(null);

            return null;
        }

        private static object QueryById(object table, int id)
        {
            var tableType = table.GetType();

            var getIdMethod = tableType.GetMethod("Get", BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int) }, null);
            if (getIdMethod == null)
                getIdMethod = tableType.GetMethod("Get", BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(string) }, null);
            if (getIdMethod != null)
            {
                try { return getIdMethod.Invoke(table, new object[] { getIdMethod.GetParameters()[0].ParameterType == typeof(int) ? (object)id : id.ToString() }); }
                catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[QueryConfigTool] GetIdMethod: {ex.Message}"); }
            }

            var indexerProp = tableType.GetProperties()
                .FirstOrDefault(p => p.GetIndexParameters().Length == 1 &&
                    (p.GetIndexParameters()[0].ParameterType == typeof(int) ||
                     p.GetIndexParameters()[0].ParameterType == typeof(string)));
            if (indexerProp != null)
            {
                try
                {
                    object key = indexerProp.GetIndexParameters()[0].ParameterType == typeof(int) ? (object)id : id.ToString();
                    return indexerProp.GetValue(table, new[] { key });
                }
                catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[QueryConfigTool] indexer: {ex.Message}"); }
            }

            var enumerator = GetEnumerator(table);
            if (enumerator != null)
            {
                while (enumerator.MoveNext())
                {
                    var item = enumerator.Current;
                    if (item == null) continue;
                    var itemType = item.GetType();

                    var idField = itemType.GetField("Id", BindingFlags.Public | BindingFlags.Instance)
                        ?? itemType.GetField("id", BindingFlags.Public | BindingFlags.Instance)
                        ?? itemType.GetField("ID", BindingFlags.Public | BindingFlags.Instance)
                        ?? itemType.GetField("ConfigId", BindingFlags.Public | BindingFlags.Instance);
                    var idProp = itemType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)
                        ?? itemType.GetProperty("id", BindingFlags.Public | BindingFlags.Instance)
                        ?? itemType.GetProperty("ID", BindingFlags.Public | BindingFlags.Instance);

                    if (idField != null)
                    {
                        int itemId = Convert.ToInt32(idField.GetValue(item));
                        if (itemId == id) return item;
                    }
                    if (idProp != null)
                    {
                        int itemId = Convert.ToInt32(idProp.GetValue(item, null));
                        if (itemId == id) return item;
                    }
                }
            }

            return null;
        }

        private static IEnumerator GetEnumerator(object obj)
        {
            if (obj is IEnumerable e) return e.GetEnumerator();

            var method = obj.GetType().GetMethod("GetEnumerator", Type.EmptyTypes)
                ?? obj.GetType().GetMethod("GetValues", Type.EmptyTypes)
                ?? obj.GetType().GetMethod("Values", Type.EmptyTypes)
                ?? obj.GetType().GetMethod("All", Type.EmptyTypes);

            if (method != null && method.ReturnType != typeof(void))
            {
                object ret = method.Invoke(obj, null);
                if (ret is IEnumerable e2) return e2.GetEnumerator();
                if (ret is IEnumerator e3) return e3;
            }

            return null;
        }

        private static int TryCountItems(object obj)
        {
            if (obj is ICollection col) return col.Count;
            if (obj is IDictionary dict) return dict.Count;
            var countProp = obj.GetType().GetProperty("Count", BindingFlags.Public | BindingFlags.Instance)
                ?? obj.GetType().GetProperty("Length", BindingFlags.Public | BindingFlags.Instance);
            if (countProp != null)
            {
                try { return (int)countProp.GetValue(obj, null); }
                catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[QueryConfigTool] Count: {ex.Message}"); }
            }
            return -1;
        }

        private static JToken SerializeObject(object value, int depth)
        {
            if (value == null) return JValue.CreateNull();
            if (value is string s) return new JValue(s);
            if (value is ValueType && value is not Enum)
            {
                try { return JToken.FromObject(value); }
                catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[QueryConfigTool] FromObject: {ex.Message}"); return new JValue(value.ToString()); }
            }
            if (value is Enum) return new JValue(value.ToString());

            if (depth <= 0) return new JValue(value.GetType().Name);

            var obj = new JObject();
            obj["_type"] = value.GetType().Name;

            foreach (var fi in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    object v = fi.GetValue(value);
                    if (v is string || v is ValueType)
                        obj[fi.Name] = JToken.FromObject(v);
                    else if (v is IList list && list.Count > 0 && list[0] is ValueType)
                        obj[fi.Name] = new JArray(list.Cast<object>().Take(10));
                    else if (depth > 1)
                        obj[fi.Name] = SerializeObject(v, depth - 1);
                }
                catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[QueryConfigTool] SerializeObject: {ex.Message}"); }
            }

            return obj;
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
                catch (Exception ex) { Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[QueryConfigTool] FindType: {ex.Message}"); }
            }
            return null;
        }
    }
}
