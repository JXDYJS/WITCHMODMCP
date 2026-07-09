using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WitchModMCP.Dispatcher;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class ModStateTool : IMcpTool
    {
        public string Name => "dump_mod_state";
        public string Description => "列出当前游戏加载的所有 Mod 信息，包括 Mod 名称、程序集、初始化入口等。用于 Mod 开发者排查加载问题。";
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
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var modList = new JArray();
                var seenAssemblies = new HashSet<string>();

                foreach (var asm in assemblies)
                {
                    try
                    {
                        string asmName = asm.GetName().Name;

                        if (asm.IsDynamic || string.IsNullOrEmpty(asmName))
                            continue;

                        var modEntries = new List<ModEntry>();
                        foreach (var type in asm.GetExportedTypes())
                        {
                            bool typeHasModInit = type.GetCustomAttributes(false)
                                .Any(a => a.GetType().Name == "ModInitializeAttribute");

                            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                            {
                                bool methodHasModInit = method.GetCustomAttributes(false)
                                    .Any(a => a.GetType().Name == "ModInitializeAttribute");

                                if (methodHasModInit)
                                {
                                    modEntries.Add(new ModEntry
                                    {
                                        TypeName = type.FullName,
                                        NamespaceName = type.Namespace,
                                        EntryMethod = method.Name,
                                        Source = "method"
                                    });
                                }
                            }

                            if (typeHasModInit && !modEntries.Any(e => e.TypeName == type.FullName))
                            {
                                modEntries.Add(new ModEntry
                                {
                                    TypeName = type.FullName,
                                    NamespaceName = type.Namespace,
                                    Source = "type"
                                });
                            }
                        }

                        if (modEntries.Count > 0)
                        {
                            string asmKey = asmName + "|" + asm.Location;
                            if (seenAssemblies.Contains(asmKey)) continue;
                            seenAssemblies.Add(asmKey);

                            var modInfo = new JObject
                            {
                                ["assemblyName"] = asmName,
                                ["assemblyLocation"] = asm.Location,
                                ["assemblyVersion"] = asm.GetName().Version?.ToString() ?? "unknown"
                            };

                            var initTypes = new JArray();
                            foreach (var e in modEntries)
                            {
                                var ti = new JObject
                                {
                                    ["typeName"] = e.TypeName,
                                    ["namespace"] = e.NamespaceName
                                };
                                if (e.EntryMethod != null)
                                    ti["entryMethod"] = e.EntryMethod;
                                ti["attributeOn"] = e.Source;
                                initTypes.Add(ti);
                            }
                            modInfo["initTypes"] = initTypes;
                            modList.Add(modInfo);
                        }
                    }
                    catch
                    {
                    }
                }

                var ownedAssemblies = new JArray();
                var seenOwned = new HashSet<string>();
                foreach (var asm in assemblies)
                {
                    try
                    {
                        string name = asm.GetName().Name;
                        if (!string.IsNullOrEmpty(name) && name.StartsWith("WitchMod", StringComparison.OrdinalIgnoreCase))
                        {
                            string key = name;
                            if (seenOwned.Contains(key)) continue;
                            seenOwned.Add(key);

                            ownedAssemblies.Add(new JObject
                            {
                                ["name"] = name,
                                ["version"] = asm.GetName().Version?.ToString() ?? "unknown",
                                ["location"] = asm.Location
                            });
                        }
                    }
                    catch { }
                }

                result["modCount"] = modList.Count;
                result["mods"] = modList;
                result["relatedAssemblies"] = ownedAssemblies;

                try
                {
                    var modCfgType = FindType("ModConfig");
                    if (modCfgType != null)
                        result["hasModConfigType"] = modCfgType.FullName;
                }
                catch { }

                return (JToken)result;
            });
        }

        private struct ModEntry
        {
            public string TypeName;
            public string NamespaceName;
            public string EntryMethod;
            public string Source;
        }

        private static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var types = asm.GetTypes();
                    var match = types.FirstOrDefault(t => t.Name == name || (t.FullName != null && t.FullName.EndsWith("." + name)));
                    if (match != null) return match;
                }
                catch { }
            }
            return null;
        }
    }
}
