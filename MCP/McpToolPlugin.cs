using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace WitchModMCP.MCP
{
    public static class McpToolPlugin
    {
        private static readonly List<string> _pluginDlls = new();

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
                    var asm = Assembly.Load(File.ReadAllBytes(path));
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

        public static void ClearPlugins()
        {
            _pluginDlls.Clear();
        }
    }
}
