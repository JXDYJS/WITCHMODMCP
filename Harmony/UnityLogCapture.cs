using UnityEngine;
using WitchModMCP.Utils;

namespace WitchModMCP.Harmony
{
    internal static class UnityLogCapture
    {
        private static bool _subscribed;

        internal static void Subscribe()
        {
            if (_subscribed) return;
            Application.logMessageReceivedThreaded += OnUnityLogThreaded;
            _subscribed = true;
        }

        internal static void Unsubscribe()
        {
            if (!_subscribed) return;
            Application.logMessageReceivedThreaded -= OnUnityLogThreaded;
            _subscribed = false;
        }

        private static void OnUnityLogThreaded(string logString, string stackTrace, LogType type)
        {
            LogBuffer.Enqueue(logString, stackTrace, type.ToString());
        }
    }
}
