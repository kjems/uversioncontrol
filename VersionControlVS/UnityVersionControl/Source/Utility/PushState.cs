// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;

internal class PushState : IDisposable
{
    readonly Action endAction;
    public PushState(Action startAction, Action endAction)
    {
        this.endAction = endAction;
        startAction();
    }
    public void Dispose()
    {
        endAction();
    }
}

internal class PushState<T> : IDisposable
{
    readonly T initialValue;
    readonly Action<T> setAction;
    public PushState(T initialValue, T temporaryValue, Action<T> setAction)
    {
        this.initialValue = initialValue;
        this.setAction = setAction;
        setAction(temporaryValue);
    }
    public void Dispose()
    {
        setAction(initialValue);
    }
}

internal static class PushStateUtility
{
    public static PushState Profiler(string name, UnityEngine.Object obj = null)
    {
        return new PushState(() => UnityEngine.Profiler.BeginSample(name, obj), () => UnityEngine.Profiler.EndSample());
    }
}

