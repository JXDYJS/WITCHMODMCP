using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class GetGameInfoTool : IMcpTool
    {
        public string Name => "get_game_info";
        public string Description => "获取游戏安装目录信息：游戏根路径、Data 路径、Managed 路径、Mods 目录、游戏版本号等。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
        };

        public Task<JToken> Execute(JToken args)
        {
            return GameDispatcher.RunOnMainThread(() =>
            {
                var result = new JObject();

                try
                {
                    var dataPath = Application.dataPath;
                    result["dataPath"] = dataPath;

                    var gameRoot = Path.GetDirectoryName(dataPath);
                    if (gameRoot != null)
                    {
                        var parent = Path.GetDirectoryName(gameRoot);
                        if (parent != null)
                            result["gameRoot"] = parent;
                    }

                    var managedPath = Path.Combine(dataPath, "Managed");
                    if (Directory.Exists(managedPath))
                        result["managedPath"] = managedPath;

                    var modsPath = Path.Combine(dataPath, "Mods");
                    if (Directory.Exists(modsPath))
                        result["modsPath"] = modsPath;
                }
                catch (Exception ex)
                {
                    result["pathError"] = ex.Message;
                }

                try
                {
                    result["unityVersion"] = Application.unityVersion;
                    result["platform"] = Application.platform.ToString();
                    result["productName"] = Application.productName;
                    result["companyName"] = Application.companyName;
                }
                catch (Exception ex)
                {
                    result["versionError"] = ex.Message;
                }

                try
                {
                    var mgrType = FindType("GameConfigManager");
                    if (mgrType != null)
                    {
                        object mgrInstance = GetStaticPropertyOrField(mgrType, "Instance")
                            ?? GetStaticPropertyOrField(mgrType, "instance");
                        if (mgrInstance != null)
                        {
                            var modConfigs = GetMemberValueByName(mgrInstance, mgrType, "modConfigs");
                            if (modConfigs is System.Collections.IList list)
                            {
                                var mods = new JArray();
                                foreach (var m in list)
                                {
                                    if (m == null) continue;
                                    var mType = m.GetType();
                                    var name = GetStringProperty(m, mType, "ModName")
                                        ?? GetStringField(m, mType, "ModName");
                                    var dir = GetStringProperty(m, mType, "DirectoryName")
                                        ?? GetStringField(m, mType, "DirectoryName");
                                    if (name != null || dir != null)
                                    {
                                        var entry = new JObject();
                                        if (name != null) entry["name"] = name;
                                        if (dir != null) entry["directory"] = dir;
                                        mods.Add(entry);
                                    }
                                }
                                if (mods.Count > 0)
                                    result["loadedMods"] = mods;
                            }

                            var loadedDirs = GetMemberValueByName(mgrInstance, mgrType, "loadedModDirectories");
                            if (loadedDirs is System.Collections.ICollection dirCol && dirCol.Count > 0)
                            {
                                var dirs = new JArray();
                                foreach (var d in dirCol)
                                    dirs.Add(d?.ToString());
                                result["loadedModDirectories"] = dirs;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    result["modsError"] = ex.Message;
                }

                return (JToken)result;
            });
        }

        private static string GetStringProperty(object obj, Type type, string name)
        {
            try
            {
                var p = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                return p?.GetValue(obj, null)?.ToString();
            }
            catch { return null; }
        }

        private static string GetStringField(object obj, Type type, string name)
        {
            try
            {
                var f = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                return f?.GetValue(obj)?.ToString();
            }
            catch { return null; }
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
