using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WitchModMCP.MCP;

namespace WitchModMCP.Tools
{
    public class GetEnvInfoTool : IMcpTool
    {
        public string Name => "get_env_info";
        public string Description => "扫描所有已加载程序集上的 MCPSkillNamespace 特性，返回各 Mod 的 skill 文档物理路径。用于外部脚本发现 Mod 资源。";
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
                    string skillRel = null;

                    var attrs = asm.GetCustomAttributesData();
                    foreach (var cad in attrs)
                    {
                        var typeName = cad.Constructor?.DeclaringType?.Name;
                        if (typeName == null) continue;

                        if (typeName == "MCPSkillNamespaceAttribute" && cad.ConstructorArguments.Count > 0)
                            skillRel = cad.ConstructorArguments[0].Value as string;
                    }

                    if (skillRel == null) continue;

                    var asmName = asm.GetName().Name;
                    if (string.IsNullOrEmpty(asmName) || asm.IsDynamic) continue;
                    if (!seen.Add(asmName)) continue;

                    var dir = McpToolPlugin.GetAssemblyDirectory(asmName);
                    if (dir == null && !string.IsNullOrEmpty(asm.Location))
                        dir = System.IO.Path.GetDirectoryName(asm.Location);
                    if (dir == null) continue;

                    var modRoot = dir.EndsWith("Scripts", StringComparison.OrdinalIgnoreCase)
                        ? System.IO.Path.GetDirectoryName(dir)
                        : dir;

                    var mod = new JObject
                    {
                        ["assemblyName"] = asmName,
                        ["skillPath"] = System.IO.Path.GetFullPath(System.IO.Path.Combine(modRoot, skillRel))
                    };
                    activeModules.Add(mod);
                }
                catch (Exception ex)
                {
                    Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[GetEnvInfoTool] assembly scan: {ex.Message}");
                }
            }

            result["activeModules"] = activeModules;
            return Task.FromResult<JToken>(result);
        }
    }
}
