using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class GetEnvInfoTool : IMcpTool
    {
        public string Name => "get_env_info";
        public string Description => "扫描所有已加载程序集上的 MCPSkillNamespace/MCPPluginNamespace 特性，返回各 Mod 的文档和插件物理路径。用于外部脚本发现 Mod 资源。";
        public JObject InputSchema => new()
        {
            ["type"] = "object",
            ["properties"] = new JObject()
        };

        public Task<JToken> Execute(JToken args)
        {
            var result = new JObject();
            var activeModules = new JArray();
            var seen = new HashSet<string>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var skillAttr = asm.GetCustomAttributes(false)
                        .FirstOrDefault(a => a is MCPSkillNamespaceAttribute) as MCPSkillNamespaceAttribute;
                    var pluginAttr = asm.GetCustomAttributes(false)
                        .FirstOrDefault(a => a is MCPPluginNamespaceAttribute) as MCPPluginNamespaceAttribute;

                    if (skillAttr == null && pluginAttr == null) continue;

                    var asmName = asm.GetName().Name;
                    if (string.IsNullOrEmpty(asmName) || asm.IsDynamic) continue;
                    if (!seen.Add(asmName)) continue;

                    var dir = McpToolPlugin.GetAssemblyDirectory(asmName);
                    if (dir == null && !string.IsNullOrEmpty(asm.Location))
                        dir = System.IO.Path.GetDirectoryName(asm.Location);
                    if (dir == null) continue;

                    // If DLL is in a Scripts/ subdirectory, mod root is one level up
                    // (matches the game's mod folder convention)
                    var modRoot = dir.EndsWith("Scripts", StringComparison.OrdinalIgnoreCase)
                        ? System.IO.Path.GetDirectoryName(dir)
                        : dir;

                    var mod = new JObject
                    {
                        ["assemblyName"] = asmName,
                        ["skillPath"] = skillAttr != null
                            ? System.IO.Path.GetFullPath(System.IO.Path.Combine(modRoot, skillAttr.RelativeFolderPath))
                            : null,
                        ["pluginPath"] = pluginAttr != null
                            ? System.IO.Path.GetFullPath(System.IO.Path.Combine(modRoot, pluginAttr.RelativeFolderPath))
                            : null
                    };
                    activeModules.Add(mod);
                }
                catch
                {
                }
            }

            result["activeModules"] = activeModules;
            return Task.FromResult<JToken>(result);
        }
    }
}
