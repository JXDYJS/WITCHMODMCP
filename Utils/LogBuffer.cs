using System;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace WitchModMCP.Utils
{
    public class LogEntry
    {
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public LogType Type { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public static class LogBuffer
    {
        private static readonly ConcurrentQueue<LogEntry> _buffer = new();

        private const int MaxEntries = 4096;

        public static void Enqueue(string message, string stackTrace, LogType type)
        {
            _buffer.Enqueue(new LogEntry
            {
                Message = message,
                StackTrace = stackTrace,
                Type = type,
                Timestamp = DateTime.Now
            });

            while (_buffer.Count > MaxEntries)
            {
                _buffer.TryDequeue(out _);
            }
        }

        public static string GetRecent(int count = 50)
        {
            var arr = new JArray();
            var snapshot = _buffer.ToArray();
            int start = Math.Max(0, snapshot.Length - count);
            for (int i = start; i < snapshot.Length; i++)
            {
                var e = snapshot[i];
                arr.Add(new JObject
                {
                    ["message"] = e.Message,
                    ["stackTrace"] = e.StackTrace,
                    ["type"] = e.Type.ToString(),
                    ["time"] = e.Timestamp.ToString("HH:mm:ss.fff")
                });
            }
            return arr.ToString();
        }
    }
}
