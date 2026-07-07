using Newtonsoft.Json.Linq;

namespace WitchModMCP.MCP.Models
{
    public class JsonRpcResponse
    {
        public string JsonRpc { get; set; } = "2.0";
        public int Id { get; set; }
        public JToken Result { get; set; }
        public JsonRpcError Error { get; set; }
    }
}
