using System;

namespace WitchModMCP.MCP
{
    public class HeartbeatContext
    {
        public string SessionId { get; set; }
        public string WorkspacePath { get; set; }
        public int Pid { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsFirstHeartbeat { get; set; }
    }
}