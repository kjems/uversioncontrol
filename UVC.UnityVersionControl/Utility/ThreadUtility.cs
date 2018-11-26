// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using System;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace UVC
{
    [InitializeOnLoad]
    public class ThreadUtility
    {
        private static readonly ExecutionContext unityExecutionContext;
        private static SynchronizationContext unitySynchronizationContext;
        
        static ThreadUtility()
        {
            unityExecutionContext = Thread.CurrentThread.ExecutionContext;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterSynchronizationContext()
        {
            unitySynchronizationContext = SynchronizationContext.Current;
        }

        public static bool IsMainThread()
        {
            return unityExecutionContext == Thread.CurrentThread.ExecutionContext;
        }

        public static bool IsUnitySynchronizationContext()
        {
            return SynchronizationContext.Current == unitySynchronizationContext;
        }
        
        static void ExecuteWithUnityContext(Action action)
        {
            if (IsUnitySynchronizationContext())
                action();
            else
                unitySynchronizationContext.Post(_ => action(), null);
        }

        public static void ExecuteOnMainThread(Action action)
        {
            if (IsMainThread())
                action();
            else
                OnNextUpdate.Do(action);
        }
    }
}
