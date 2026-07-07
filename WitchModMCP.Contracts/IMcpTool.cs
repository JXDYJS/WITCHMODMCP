using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WitchModMCP.Contracts
{
    public interface IMcpTool
    {
        string Name { get; }
        string Description { get; }
        JObject InputSchema { get; }
        Task<JToken> Execute(JToken args);
    }
}
