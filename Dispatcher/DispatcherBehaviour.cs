using UnityEngine;
using WitchModMCP.MCP;

namespace WitchModMCP.Dispatcher
{
    public class DispatcherBehaviour : MonoBehaviour
    {
        private void Update()
        {
            GameDispatcher.ExecuteTasks(WitchModMCPTaskType.Update);

            if (Input.GetKeyDown(KeyCode.F5))
            {
                Commands.Log(WitchModMCPEntry.MOD_TAG, "[Hotkey] F5 pressed - reloading MCP tools...");
                McpRouter.ReloadAllTools();
                Commands.Log(WitchModMCPEntry.MOD_TAG, "[Hotkey] MCP tools reloaded");
            }
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
