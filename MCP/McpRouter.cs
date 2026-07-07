using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WitchModMCP.Contracts;
using WitchModMCP.MCP.Models;

namespace WitchModMCP.MCP
{
    public static class McpRouter
    {
        private static readonly Dictionary<string, IMcpTool> _tools = new();

        public static void RegisterTool(IMcpTool tool)
        {
            _tools[tool.Name] = tool;
        }

        public static void RegisterTools(IEnumerable<IMcpTool> tools)
        {
            foreach (var t in tools) RegisterTool(t);
        }

        public static void ClearTools()
        {
            _tools.Clear();
        }

        public static void ReloadAllTools()
        {
            ClearTools();
            foreach (var type in McpToolPlugin.DiscoverToolTypes())
            {
                if (Activator.CreateInstance(type) is IMcpTool tool)
                    RegisterTool(tool);
            }
        }

        public static async Task<string> HandleRequest(string requestJson)
        {
            JsonRpcRequest request;
            try
            {
                request = JsonConvert.DeserializeObject<JsonRpcRequest>(requestJson);
            }
            catch
            {
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

            if (_tools.TryGetValue(request.Method, out var tool))
                return await HandleToolCall(request.Id, tool, request.Params);

            return JsonConvert.SerializeObject(new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError { Code = -32601, Message = $"Method not found: {request.Method}" }
            });
        }

        private static Task<string> HandleListTools(int id)
        {
            var tools = _tools.Values.Select(t => new JObject
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["inputSchema"] = t.InputSchema ?? new JObject()
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
