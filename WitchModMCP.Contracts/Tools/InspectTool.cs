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
    public class InspectTool : IMcpTool
    {
        public string Name => "inspect";
        public string Description => "通过反射读取任意 C# 对象的字段/属性值。支持静态成员和实例成员链式访问。可指定深度递归读取对象的子成员。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["typeName"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "类型名，如 RoleTable 或 Witch.Data.RoleTable，或值类型的实例（通过 Instance 等静态属性获取）"
                },
                ["memberPath"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "成员路径，用 . 分隔。如 Instance.CurHp，留空则只获取类型本身的静态成员列表"
                },
                ["maxDepth"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "递归序列化深度，默认 3。控制返回的子对象展开层级",
                    ["default"] = 3
                },
                ["maxItems"] = new JObject
                {
                    ["type"] = "integer",
                    ["description"] = "集合最大元素数，默认 20",
                    ["default"] = 20
                }
            },
            ["required"] = new JArray { "typeName" }
        };

        public async Task<JToken> Execute(JToken args)
        {
            string typeName = args?["typeName"]?.Value<string>();
            string memberPath = args?["memberPath"]?.Value<string>();
            int maxDepth = args?["maxDepth"]?.Value<int>() ?? 3;
            int maxItems = args?["maxItems"]?.Value<int>() ?? 20;

            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentException("typeName 不能为空");

            return await GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                Type type = FindType(typeName);
                if (type == null)
                {
                    result["error"] = $"找不到类型: {typeName}";
                    return (JToken)result;
                }

                result["foundType"] = type.FullName;

                object current = type;

                if (!string.IsNullOrEmpty(memberPath))
                {
                    var segments = memberPath.Split('.');

                    for (int i = 0; i < segments.Length; i++)
                    {
                        string segment = segments[i];
                        bool isLast = (i == segments.Length - 1);

                        try
                        {
                            MemberInfo member = ResolveMember(current, segment);

                            if (member == null)
                            {
                                if (current is Type ct)
                                {
                                    var avail = new JArray();
                                    foreach (var m in ct.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                                        .Where(m => m is FieldInfo || m is PropertyInfo).Take(30))
                                        avail.Add(m.Name);
                                    foreach (var m in ct.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                                        .Where(m => m is FieldInfo || m is PropertyInfo).Take(30))
                                    {
                                        if (!avail.Any(a => a.Value<string>() == m.Name))
                                            avail.Add(m.Name);
                                    }
                                    result[$"availableMembers_on_{ct.Name}"] = avail;
                                }
                                else if (current != null)
                                {
                                    var avail = new JArray();
                                    foreach (var m in current.GetType().GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                        .Where(m => m is FieldInfo || m is PropertyInfo).Take(50))
                                        avail.Add(m.Name);
                                    result[$"availableMembers_on_{current.GetType().Name}"] = avail;
                                }
                                result["error"] = $"找不到成员: {segment}";
                                return (JToken)result;
                            }

                            object value = GetMemberValue(member, current);

                            if (isLast)
                            {
                                result["memberPath"] = memberPath;
                                result["memberType"] = GetTypeName(value);
                                result["value"] = SerializeValue(value, current, 0, maxDepth, maxItems, segment);
                            }

                            current = value;
                            if (current == null)
                            {
                                result["memberPath"] = memberPath;
                                result["value"] = "null";
                                return (JToken)result;
                            }
                        }
                        catch (Exception ex)
                        {
                            result["error"] = $"解析 '{segment}' 时出错: {ex.Message}";
                            return (JToken)result;
                        }
                    }
                }
                else
                {
                    var members = new JObject();
                    var staticMembers = new JObject();
                    var instanceMembers = new JObject();

                    foreach (var m in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        if (m is FieldInfo fi)
                            staticMembers[fi.Name] = GetTypeName(GetMemberValue(m, type));
                        else if (m is PropertyInfo pi && pi.GetIndexParameters().Length == 0)
                        {
                            try { staticMembers[pi.Name] = GetTypeName(GetMemberValue(m, type)); }
                            catch { staticMembers[pi.Name] = "???"; }
                        }
                    }

                    foreach (var m in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        if (m is FieldInfo fi)
                            instanceMembers[fi.Name] = GetTypeName(fi.FieldType);
                        else if (m is PropertyInfo pi && pi.GetIndexParameters().Length == 0)
                            instanceMembers[pi.Name] = GetTypeName(pi.PropertyType);
                    }

                    members["static"] = staticMembers;
                    members["instance"] = instanceMembers;
                    result["members"] = members;
                }

                return (JToken)result;
            });
        }

        private static Type FindType(string name)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var asm in assemblies)
            {
                try
                {
                    var types = asm.GetTypes();
                    var match = types.FirstOrDefault(t =>
                        t.FullName == name || t.Name == name ||
                        t.FullName != null && t.FullName.EndsWith("." + name));
                    if (match != null)
                        return match;
                }
                catch
                {
                }
            }

            return Type.GetType(name, false);
        }

        private static MemberInfo ResolveMember(object context, string name)
        {
            if (context is Type type)
            {
                var f = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (f != null) return f;
                var p = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (p != null && p.GetIndexParameters().Length == 0) return p;
            }

            if (context != null && context is not Type)
            {
                var objType = context.GetType();
                var f = objType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) return f;
                var p = objType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (p != null && p.GetIndexParameters().Length == 0) return p;
            }

            return null;
        }

        private static object GetMemberValue(MemberInfo member, object context)
        {
            if (member is FieldInfo fi)
            {
                object target = fi.IsStatic ? null : context;
                return fi.GetValue(target);
            }
            if (member is PropertyInfo pi)
            {
                object target = pi.GetMethod?.IsStatic == true ? null : context;
                return pi.GetValue(target, null);
            }
            return null;
        }

        private static string GetTypeName(object value)
        {
            if (value == null) return "null";
            if (value is Type t) return $"Type({t.FullName})";
            return value.GetType().FullName ?? value.GetType().Name;
        }

        private JToken SerializeValue(object value, object parentContext, int depth, int maxDepth, int maxItems, string memberName)
        {
            if (value == null) return JValue.CreateNull();
            if (value is Type t) return new JValue($"Type({t.FullName})");

            Type vt = value.GetType();

            if (vt.IsPrimitive || value is string || value is decimal)
                return JToken.FromObject(value);

            if (value is Enum)
                return new JValue(value.ToString());

            if (value is IEnumerable enumerable and not string)
            {
                var arr = new JArray();
                int count = 0;
                foreach (var item in enumerable)
                {
                    if (count >= maxItems)
                    {
                        arr.Add("... (truncated)");
                        break;
                    }
                    if (depth >= maxDepth)
                        arr.Add(GetTypeName(item) ?? "null");
                    else
                        arr.Add(SerializeValue(item, value, depth + 1, maxDepth, maxItems, $"{memberName}[{count}]"));
                    count++;
                }
                return arr;
            }

            if (depth >= maxDepth)
                return new JValue($"{GetTypeName(value)} (max depth reached)");

            var obj = new JObject();
            obj["_type"] = vt.FullName ?? vt.Name;

            foreach (var fi in vt.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    object v = fi.GetValue(value);
                    obj[fi.Name] = SerializeValue(v, value, depth + 1, maxDepth, maxItems, fi.Name);
                }
                catch { obj[fi.Name] = "???"; }
            }

            foreach (var pi in vt.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (pi.GetIndexParameters().Length > 0) continue;
                try
                {
                    object v = pi.GetValue(value, null);
                    obj[pi.Name] = SerializeValue(v, value, depth + 1, maxDepth, maxItems, pi.Name);
                }
                catch { obj[pi.Name] = "???"; }
            }

            return obj;
        }
    }
}
