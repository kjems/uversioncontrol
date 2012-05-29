// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Threading;
using UnityEditor;

[InitializeOnLoad]
internal class ThreadUtility
{
    static ThreadUtility()
    {
        unityExecutionContext = Thread.CurrentThread.ExecutionContext;
    }
    public static bool IsMainThread()
    {
        return unityExecutionContext == Thread.CurrentThread.ExecutionContext;
    }
    private static readonly ExecutionContext unityExecutionContext;
}
