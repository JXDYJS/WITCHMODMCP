using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace WitchModMCP.MCP
{
    public static class McpToolPlugin
    {
        private static readonly List<string> _pluginDlls = new();
        private static readonly Dictionary<string, string> _assemblyPaths = new(StringComparer.OrdinalIgnoreCase);

        public static void RegisterPluginDll(string absoluteDllPath)
        {
            if (!string.IsNullOrEmpty(absoluteDllPath) && !_pluginDlls.Contains(absoluteDllPath))
                _pluginDlls.Add(absoluteDllPath);
        }

        public static List<Type> DiscoverToolTypes()
        {
            var types = new List<Type>();
            foreach (var path in _pluginDlls)
            {
                if (!File.Exists(path))
                {
                    Commands.Log(WitchModMCPEntry.MOD_TAG, $"[McpToolPlugin] DLL not found: {path}");
                    continue;
                }
                try
                {
                    var absPath = Path.GetFullPath(path);
                    var asm = Assembly.Load(File.ReadAllBytes(absPath));
                    var asmName = asm.GetName().Name;
                    if (asmName != null)
                        _assemblyPaths[asmName] = Path.GetDirectoryName(absPath);

                    foreach (var type in asm.GetExportedTypes())
                    {
                        if (typeof(IMcpTool).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                            types.Add(type);
                    }
                }
                catch (Exception ex)
                {
                    Commands.Log(WitchModMCPEntry.MOD_TAG, $"[McpToolPlugin] Load failed for {path}: {ex.GetType().Name} - {ex.Message}");
                }
            }
            return types;
        }

        public static string GetAssemblyDirectory(string assemblyName)
        {
            if (assemblyName == null) return null;

            if (_assemblyPaths.TryGetValue(assemblyName, out var tracked))
                return tracked;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (string.Equals(asm.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                    {
                        var loc = asm.Location;
                        if (!string.IsNullOrEmpty(loc) && File.Exists(loc))
                        {
                            var dir = Path.GetDirectoryName(loc);
                            _assemblyPaths[assemblyName] = dir;
                            return dir;
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        public static IReadOnlyDictionary<string, string> GetTrackedAssemblyPaths()
        {
            return _assemblyPaths;
        }

        public static void ClearPlugins()
        {
            _pluginDlls.Clear();
        }
    }
}
