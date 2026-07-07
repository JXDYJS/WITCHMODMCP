using UnityEngine;
using Witch.Mod;
using WitchModMCP.Utils;

namespace WitchModMCP.Harmony
{
    internal static class GameLogCapture
    {
        [HookBefore(typeof(Commands), "Log")]
        public static void OnBeforeCommandsLog(string tag, string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                LogBuffer.Enqueue($"[{tag}] {message}", null, LogType.Log);
            }
        }

        [HookBefore(typeof(Commands), "LogError")]
        public static void OnBeforeCommandsLogError(string tag, string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                LogBuffer.Enqueue($"[{tag}] Error:{message}", new System.Diagnostics.StackTrace().ToString(), LogType.Error);
            }
        }

        [HookBefore(typeof(Commands), "LogWarning")]
        public static void OnBeforeCommandsLogWarning(string tag, string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                LogBuffer.Enqueue($"[{tag}] Warning:{message}", new System.Diagnostics.StackTrace().ToString(), LogType.Warning);
            }
        }
    }
}
