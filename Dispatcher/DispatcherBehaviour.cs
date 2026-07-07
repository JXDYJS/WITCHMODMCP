using UnityEngine;
using Witch.UI;
using WitchModMCP.MCP;

namespace WitchModMCP.Dispatcher
{
    public class DispatcherBehaviour : MonoBehaviour
    {
        private void Update()
        {
            GameDispatcher.ExecuteTasks(WitchModMCPTaskType.Update);
        }

        private void OnGUI()
        {
            var e = Event.current;
            if (e == null || !e.isKey || e.keyCode != KeyCode.F5) return;

            var console = ConsoleUI.Instance;
            if (console == null || !console.gameObject.activeSelf) return;

            e.Use();
            McpRouter.ReloadAllTools();
            Commands.Log(WitchModMCPEntry.MOD_TAG, "[Hotkey] MCP tools reloaded");
        }
        private void Start()
        {
            GameDispatcher.ExecuteTasks(WitchModMCPTaskType.Start);
        }
        private void Awake()
        {
            GameDispatcher.ExecuteTasks(WitchModMCPTaskType.Awake);
        }
        private void OnEnable()
        {
            GameDispatcher.ExecuteTasks(WitchModMCPTaskType.OnEnable);
        }
        private void FixedUpdate()
        {
            GameDispatcher.ExecuteTasks(WitchModMCPTaskType.FixedUpdate);
        }
        private void LateUpdate()
        {
            GameDispatcher.ExecuteTasks(WitchModMCPTaskType.LateUpdate);
        }
        private void OnDisable()
        {
            GameDispatcher.ExecuteTasks(WitchModMCPTaskType.OnDisable);
        }
        private void OnDestroy()
        {
            GameDispatcher.ExecuteTasks(WitchModMCPTaskType.OnDestroy);
        }
    }
}
