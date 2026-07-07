using Newtonsoft.Json.Linq;

namespace WitchModMCP.MCP.Models
{
    public class JsonRpcRequest
    {
        public string JsonRpc { get; set; }
        public int Id { get; set; }
        public string Method { get; set; }
        public JToken Params { get; set; }
    }
}
