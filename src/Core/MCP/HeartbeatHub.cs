using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace WitchModMCP.MCP
{
    public static class HeartbeatHub
    {
        private static readonly object _lock = new();
        private static DateTime? _lastHeartbeat;
        private static string _sessionId;
        private static bool _firstReceived;

        public static Action<HeartbeatContext> OnFirstHeartbeat;

        public static DateTime? LastHeartbeat
        {
            get { lock (_lock) { return _lastHeartbeat; } }
        }

        public static string SessionId
        {
            get { lock (_lock) { return _sessionId; } }
        }

        public static bool IsFirstHeartbeatReceived
        {
            get { lock (_lock) { return _firstReceived; } }
        }

        public static JObject ProcessHeartbeat(JToken body)
        {
            string workspacePath = body?["workspacePath"]?.Value<string>() ?? "";
            int pid = body?["pid"]?.Value<int>() ?? 0;
            var now = DateTime.UtcNow;

            HeartbeatContext ctx = null;

            lock (_lock)
            {
                if (!_firstReceived)
                {
                    _firstReceived = true;
                    _sessionId = Guid.NewGuid().ToString("N");
                    ctx = new HeartbeatContext
                    {
                        SessionId = _sessionId,
                        WorkspacePath = workspacePath,
                        Pid = pid,
                        Timestamp = now,
                        IsFirstHeartbeat = true,
                    };
                }
                _lastHeartbeat = now;
            }

            if (ctx != null)
            {
                try
                {
                    OnFirstHeartbeat?.Invoke(ctx);
                }
                catch (Exception ex)
                {
                    Commands.LogError(WitchModMCPEntry.MOD_TAG, $"[HeartbeatHub] OnFirstHeartbeat subscriber threw: {ex.Message}");
                }
            }

            return new JObject
            {
                ["status"] = "ok",
                ["sessionId"] = _sessionId,
                ["isFirstHeartbeat"] = ctx != null,
                ["timestamp"] = now.ToString("o"),
                ["workspacePath"] = workspacePath,
                ["pid"] = pid,
            };
        }
    }
}