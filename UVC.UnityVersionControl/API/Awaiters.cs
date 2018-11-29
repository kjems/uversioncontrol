using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace UVC
{
    public static class Awaiters
    {
        // TaskPool
        public static TaskPoolAwaiter TaskPool => new TaskPoolAwaiter(); 
        public struct TaskPoolAwaiter
        {
            public Awaiter GetAwaiter() => new Awaiter();

            public struct Awaiter : ICriticalNotifyCompletion
            {
                static readonly Action<object> switchToCallback = Callback;

                public bool IsCompleted => false;
                public void GetResult() { }

                public void OnCompleted(Action continuation)
                {
                    Task.Factory.StartNew(switchToCallback, continuation, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
                }

                public void UnsafeOnCompleted(Action continuation)
                {
                    Task.Factory.StartNew(switchToCallback, continuation, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
                }

                static void Callback(object state)
                {
                    var continuation = (Action)state;
                    continuation();
                }
            }
        }
        
        // MainThread
        public static MainThreadAwaiter MainThread => new MainThreadAwaiter(); 
        public struct MainThreadAwaiter
        {
            public Awaiter GetAwaiter() => new Awaiter();

            public struct Awaiter : ICriticalNotifyCompletion
            {
                public bool IsCompleted => ThreadUtility.IsMainThread();
                public void GetResult() { }

                public void OnCompleted(Action continuation)
                {
                    OnNextUpdate.Do(continuation);
                }

                public void UnsafeOnCompleted(Action continuation)
                {
                    OnNextUpdate.Do(continuation);
                }
            }
        }
    }
}