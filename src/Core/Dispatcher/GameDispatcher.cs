using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace WitchModMCP.Dispatcher
{
    public enum WitchModMCPTaskType
    {
        Awake = 0,
        OnEnable = 1,
        Start = 2,
        FixedUpdate = 3,
        Update = 4,
        LateUpdate = 5,
        OnDisable = 6,
        OnDestroy = 7
    }
    public struct WitchModMCPTask
    {
      public Int64 _time;
      public Func<Task> _task;
      public WitchModMCPTaskType _type = WitchModMCPTaskType.Update;
        public WitchModMCPTask(Func<Task> task = null
        , WitchModMCPTaskType type = WitchModMCPTaskType.Update)
        {
            _task = task ?? (() => Task.CompletedTask);
            _type = type;
            _time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    };
    public static class GameDispatcher
    {
        private static readonly int TypeCount = Enum.GetValues(typeof(WitchModMCPTaskType)).Length;
        private static readonly ConcurrentQueue<WitchModMCPTask>[] _queues;
        private static int _mainThreadId;
        static GameDispatcher()
        {
            _queues = new ConcurrentQueue<WitchModMCPTask>[TypeCount];
            for (int i = 0; i < TypeCount; i++)
            {
                _queues[i] = new ConcurrentQueue<WitchModMCPTask>();
            }
        }

        public static void Initialize()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        public static void EnqueueTask(WitchModMCPTask task)
        {
            int index = (int)task._type;
            if ((uint)index >= (uint)TypeCount) return;

            _queues[index].Enqueue(task);
        }

        public static void ExecuteTasks(WitchModMCPTaskType type)
        {
            int index = (int)type;
            if ((uint)index >= (uint)TypeCount) return;

            var queue = _queues[index];

            int count = queue.Count;

            while (count-- > 0 && queue.TryDequeue(out var work))
            {
                if (work._task == null) continue;

                try
                {
                    work._task();
                }
                catch (Exception ex)
                {
                    Commands.LogError(WitchModMCP.WitchModMCPEntry.MOD_TAG,$"[GameDispatcher] 任务执行异常 ({type}): {ex}");
                }
            }
        }

        public static Task<T> RunOnMainThread<T>(Func<T> func, WitchModMCPTaskType type = WitchModMCPTaskType.Update)
        {
            if (IsMainThread)
            {
                try { return Task.FromResult(func()); }
                catch (Exception ex) { return Task.FromException<T>(ex); }
            }

            var tcs = new TaskCompletionSource<T>();
            EnqueueTask(new WitchModMCPTask(() =>
            {
                try
                {
                    var result = func();
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                return Task.CompletedTask;
            }, type));
            return tcs.Task;
        }

        public static Task RunOnMainThread(Action action, WitchModMCPTaskType type = WitchModMCPTaskType.Update)
        {
            return RunOnMainThread(() => { action(); return true; }, type);
        }
    }
}
