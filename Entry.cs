using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Witch;
using Witch.Mod;
using WitchModMCP.Dispatcher;
using WitchModMCP.Harmony;
using WitchModMCP.MCP;
using WitchModMCP.Utils;
using XLua;

namespace WitchModMCP
{
    public static class WitchModMCPEntry
    {
        public const string MOD_TAG = "WitchModMCP";
        internal static McpServer Server;

        [ModInitialize]
        public static void Entry(ModConfig modConfig)
        {
            Commands.Log(MOD_TAG, "(DLL) Mod loaded");

            GameDispatcher.Initialize();
            UnityLogCapture.Subscribe();
            PlayerLogReader.ReadAndEnqueue();

            var go = new GameObject("WitchModMCP_Dispatcher");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<DispatcherBehaviour>();

            var cfgPath = Path.Combine(modConfig.DirectoryName, "ModConfig.json");
            var json = JObject.Parse(File.ReadAllText(cfgPath, Encoding.UTF8));
            int port = json.Value<int>("MCPPort");
            Commands.Log(MOD_TAG, $"[Config] MCPPort={port}");

            var contractsDllPath = Path.Combine(modConfig.DirectoryName, "Scripts", "WitchModMCP.Contracts.dll");
            McpToolPlugin.RegisterPluginDll(contractsDllPath);
            McpRouter.ReloadAllTools();

            Commands.Log(MOD_TAG, $"Contracts DLL: {contractsDllPath}");

            Server = new McpServer();
            Server.ModDirectory = modConfig.DirectoryName;
            Server.Start(port);

            GameDispatcher.EnqueueTask(new WitchModMCP.Dispatcher.WitchModMCPTask(RegisterLuaBridgeRetry));
        }

        private static async Task RegisterLuaBridgeRetry()
        {
            for (int i = 0; i < 30; i++)
            {
                var luaEnv = ScriptExecutor.luaEnv;
                if (luaEnv != null)
                {
                    RegisterLuaBridge(luaEnv);
                    return;
                }
                await Task.Delay(1000);
            }
            Commands.LogError(MOD_TAG, "Lua bridge registration timed out (luaEnv never became available)");
        }

        private static void RegisterLuaBridge(LuaEnv env)
        {
            try
            {
                env.DoString(@"
                    SyncLuaAssemblies = function()
                        local ok, as = pcall(function()
                            return CS.System.AppDomain.CurrentDomain:GetAssemblies()
                        end)
                        if not ok or not as then return end
                        for i = 0, as.Length - 1 do
                            local a = as[i]
                            if a ~= nil then
                                pcall(function() xlua.load_assembly(a:GetName().Name) end)
                            end
                        end
                    end

                    SyncLuaAssemblies()
                ", "WitchModMCP_LuaBridge", null);

                Commands.Log(MOD_TAG, "Lua bridge registered: native CS access, all assemblies synced");
            }
            catch (Exception ex)
            {
                Commands.LogError(MOD_TAG, $"Failed to register Lua bridge: {ex.Message}");
            }
        }

        public static void ResyncLuaAssemblies()
        {
            try
            {
                var luaEnv = ScriptExecutor.luaEnv;
                if (luaEnv == null) return;
                luaEnv.DoString("if SyncLuaAssemblies ~= nil then SyncLuaAssemblies() end", "WitchModMCP_Resync", null);
            }
            catch (Exception ex)
            {
                Commands.LogError(MOD_TAG, $"[LuaBridge] Resync failed: {ex.Message}");
            }
        }
    }

    public static class LuaAPI
    {
        private static readonly FieldInfo _toolsField = typeof(McpRouter).GetField("_tools",
            BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly ConcurrentDictionary<string, IMcpTool> _tools =
            _toolsField?.GetValue(null) as ConcurrentDictionary<string, IMcpTool>;

        public static string RunTool(string toolName, string jsonArgs)
        {
            try
            {
                if (_tools == null || !_tools.TryGetValue(toolName, out var tool))
                    return $"{{\"error\":\"tool not found: {toolName}\"}}";

                var args = string.IsNullOrEmpty(jsonArgs) ? new JObject() : JToken.Parse(jsonArgs);
                var result = tool.Execute(args).GetAwaiter().GetResult();
                return result?.ToString(Formatting.None) ?? "nil";
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message}\"}}";
            }
        }
    }

}
