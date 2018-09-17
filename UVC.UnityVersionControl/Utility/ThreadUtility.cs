// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Threading;
using UnityEditor;

namespace UVC
{
    [InitializeOnLoad]
    public class ThreadUtility
    {
        static ThreadUtility()
        {
            unityExecutionContext = Thread.CurrentThread.ExecutionContext;
        }

        public static bool IsMainThread()
        {
            return unityExecutionContext == Thread.CurrentThread.ExecutionContext;
        }

        public static void ExecuteOnMainThread(System.Action action)
        {
            if (ThreadUtility.IsMainThread())
                action();
            else
                OnNextUpdate.Do(action);
        }

        private static readonly ExecutionContext unityExecutionContext;
    }
}
