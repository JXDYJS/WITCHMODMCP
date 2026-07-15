using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Witch.Mod;
using WitchModMCP.MCP.Models;

namespace WitchModMCP.MCP
{
    public static class McpRouter
    {
        private static readonly ConcurrentDictionary<string, IMcpTool> _tools = new();

        public static void RegisterTool(IMcpTool tool)
        {
            _tools[tool.Name] = tool;

            var sourceMod = tool.GetType().Assembly.GetName().Name;
            if (!string.IsNullOrEmpty(sourceMod))
                _tools.TryAdd($"{sourceMod}/{tool.Name}", tool);
        }

        public static void RegisterTools(IEnumerable<IMcpTool> tools)
        {
            foreach (var t in tools) RegisterTool(t);
        }

        public static int ToolCount => _tools.Count;

        public static string[] GetToolNames() => _tools.Keys.OrderBy(n => n).ToArray();

        public static void ClearTools()
        {
            _tools.Clear();
        }

        public static void ReloadAllTools()
        {
            ClearTools();
            foreach (var type in McpToolPlugin.DiscoverToolTypes())
            {
                try
                {
                    if (Activator.CreateInstance(type) is IMcpTool tool)
                    {
                        RegisterTool(tool);
                        var dllName = type.Assembly.GetName().Name;
                        Commands.Log(WitchModMCPEntry.MOD_TAG, $"load {tool.Name} from {dllName} success");
                    }
                }
                catch (Exception ex)
                {
                    Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpRouter] Failed to instantiate tool {type.FullName}: {ex.Message}");
                }
            }
        }

        public static async Task<string> HandleRequest(string requestJson)
        {
            JsonRpcRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<JsonRpcRequest>(requestJson);
            }
            catch (Exception ex)
            {
                Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[McpRouter] JSON parse error: {ex.Message}");
                return JsonConvert.SerializeObject(new JsonRpcResponse
                {
                    Id = 0,
                    Error = new JsonRpcError { Code = -32700, Message = "Parse error" }
                });
            }

            if (request == null || request.Method == null)
            {
                return JsonConvert.SerializeObject(new JsonRpcResponse
                {
                    Id = request?.Id ?? 0,
                    Error = new JsonRpcError { Code = -32600, Message = "Invalid Request" }
                });
            }

            if (request.Method == "list_tools")
                return await HandleListTools(request.Id);

            if (request.Method == "ping")
                return HandlePing(request.Id);

            var tool = ResolveTool(request.Method.Trim());
            if (tool != null)
                return await HandleToolCall(request.Id, tool, request.Params);

            return JsonConvert.SerializeObject(new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError { Code = -32601, Message = $"Method not found: {request.Method}" }
            });
        }

        private static Task<string> HandleListTools(int id)
        {
            var seen = new HashSet<string>();
            var tools = _tools
                .Where(kvp => !kvp.Key.Contains('/'))
                .Select(kvp => kvp.Value)
                .Distinct()
                .Select(t => new JObject
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["inputSchema"] = t.InputSchema ?? new JObject(),
                ["sourceMod"] = t.GetType().Assembly.GetName().Name
            });

            var result = new JObject
            {
                ["tools"] = new JArray(tools)
            };

            var response = new JsonRpcResponse
            {
                Id = id,
                Result = result
            };
            return Task.FromResult(JsonConvert.SerializeObject(response));
        }

        private static string HandlePing(int id)
        {
            var response = new JsonRpcResponse
            {
                Id = id,
                Result = new JObject
                {
                    ["status"] = "ok",
                    ["toolCount"] = _tools.Count
                }
            };
            return JsonConvert.SerializeObject(response);
        }

        public static Task<string> HandleHeartbeat(int port, string body)
        {
            JToken bodyToken;
            try { bodyToken = JToken.Parse(body); }
            catch { bodyToken = new JObject(); }

            var ctx = HeartbeatHub.ProcessHeartbeat(bodyToken);

            var result = new JObject
            {
                ["status"] = "ok",
                ["port"] = port,
                ["toolCount"] = _tools.Count,
                ["sessionId"] = ctx["sessionId"],
                ["isFirstHeartbeat"] = ctx["isFirstHeartbeat"],
                ["timestamp"] = ctx["timestamp"],
                ["workspacePath"] = ctx["workspacePath"],
                ["pid"] = ctx["pid"],
                ["activeModules"] = BuildActiveModules(),
            };

            return Task.FromResult(JsonConvert.SerializeObject(result));
        }

        private static JArray BuildActiveModules()
        {
            var activeModules = new JArray();
            var seen = new HashSet<string>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    string skillRel = null;
                    string pluginRel = null;

                    // Use CustomAttributeData to read attributes without instantiating types
                    // This works for assemblies loaded via Assembly.Load(byte[]) where
                    // GetCustomAttributes(false) can silently fail.
                    var attrs = asm.GetCustomAttributesData();
                    foreach (var cad in attrs)
                    {
                        var typeName = cad.Constructor?.DeclaringType?.Name;
                        if (typeName == null) continue;

                        if (typeName == "MCPSkillNamespaceAttribute" && cad.ConstructorArguments.Count > 0)
                            skillRel = cad.ConstructorArguments[0].Value as string;

                        if (typeName == "MCPPluginNamespaceAttribute" && cad.ConstructorArguments.Count > 0)
                            pluginRel = cad.ConstructorArguments[0].Value as string;
                    }

                    if (skillRel == null && pluginRel == null) continue;

                    var asmName = asm.GetName().Name;
                    if (string.IsNullOrEmpty(asmName) || asm.IsDynamic) continue;
                    if (!seen.Add(asmName)) continue;

                    var dir = McpToolPlugin.GetAssemblyDirectory(asmName);
                    if (dir == null && !string.IsNullOrEmpty(asm.Location))
                        dir = Path.GetDirectoryName(asm.Location);
                    if (dir == null) continue;

                    var modRoot = dir.EndsWith("Scripts", StringComparison.OrdinalIgnoreCase)
                        ? Path.GetDirectoryName(dir)
                        : dir;

                    var mod = new JObject
                    {
                        ["assemblyName"] = asmName,
                        ["skillPath"] = skillRel != null
                            ? Path.GetFullPath(Path.Combine(modRoot, skillRel))
                            : null,
                        ["pluginPath"] = pluginRel != null
                            ? Path.GetFullPath(Path.Combine(modRoot, pluginRel))
                            : null
                    };
                    activeModules.Add(mod);
                }
                catch
                {
                }
            }

            return activeModules;
        }

        private static IMcpTool ResolveTool(string method)
        {
            if (string.IsNullOrEmpty(method)) return null;

            if (method.Contains('/'))
            {
                _tools.TryGetValue(method, out var nsTool);
                return nsTool;
            }

            _tools.TryGetValue(method, out var plainTool);
            return plainTool;
        }

        private static async Task<string> HandleToolCall(int id, IMcpTool tool, JToken args)
        {
            try
            {
                var result = await tool.Execute(args);
                var response = new JsonRpcResponse
                {
                    Id = id,
                    Result = result
                };
                return JsonConvert.SerializeObject(response);
            }
            catch (System.Exception ex)
            {
                return JsonConvert.SerializeObject(new JsonRpcResponse
                {
                    Id = id,
                    Error = new JsonRpcError { Code = -32603, Message = $"Internal error: {ex.Message}" }
                });
            }
        }
    }
}
