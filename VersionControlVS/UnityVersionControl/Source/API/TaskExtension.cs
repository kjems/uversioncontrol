// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Threading.Tasks;
using VersionControl;

public static class TaskExtensions
{
    public static Task ContinueWithOnNextUpdate<T>(this Task<T> task, Action<T> postAction)
    {
        return task.ContinueWith(NextUpdate(postAction));
    }

    private static Action<Task<T>> NextUpdate<T>(Action<T> postAction)
    {
        return t => OnNextUpdate.Do(() => postAction(t.Result));
    }
}
