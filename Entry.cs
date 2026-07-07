using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Witch.Mod;
using WitchModMCP.Contracts;
using WitchModMCP.Dispatcher;
using WitchModMCP.Harmony;
using WitchModMCP.MCP;

namespace WitchModMCP
{
    public static class WitchModMCPEntry
    {
        public const string MOD_TAG = "WitchModMCP";

        [ModInitialize]
        public static void Entry(ModConfig modConfig)
        {
            Commands.Log(MOD_TAG, "(DLL) Mod loaded");

            GameDispatcher.Initialize();
            UnityLogCapture.Subscribe();

            var go = new GameObject("WitchModMCP_Dispatcher");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<DispatcherBehaviour>();

            var cfgPath = Path.Combine(modConfig.DirectoryName, "ModConfig.json");
            var json = JObject.Parse(File.ReadAllText(cfgPath, Encoding.UTF8));
            int port = json.Value<int>("MCPPort");
            Commands.Log(MOD_TAG, $"[Config] MCPPort={port}");

            var myDllPath = Path.Combine(modConfig.DirectoryName, "Scripts", "Entry.dll");
            McpToolPlugin.RegisterPluginDll(myDllPath);
            McpRouter.ReloadAllTools();

            McpServer mcpServer = new();
            mcpServer.Start(port);
            Commands.Log(MOD_TAG, $"[MCP] Server started on http://localhost:{port}/");
        }
    }
}
