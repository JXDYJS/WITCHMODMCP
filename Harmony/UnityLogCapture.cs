using UnityEngine;
using WitchModMCP.Utils;

namespace WitchModMCP.Harmony
{
    internal static class UnityLogCapture
    {
        internal static void Subscribe()
        {
            Application.logMessageReceivedThreaded += OnUnityLogThreaded;
        }

        private static void OnUnityLogThreaded(string logString, string stackTrace, LogType type)
        {
            LogBuffer.Enqueue(logString, stackTrace, type.ToString());
        }
    }
}
