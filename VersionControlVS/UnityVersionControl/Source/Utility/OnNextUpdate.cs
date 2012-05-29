// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

// This class makes sure an operation performed is executed within the scope of 
// Unity engines update loop. This is used by the callback functions from an
// asynchronous operation that although executing on the same thread as Unity
// not necessarily execute within the update scope.

using System;
using System.Collections.Generic;
using UnityEditor;

[InitializeOnLoad]
internal static class OnNextUpdate
{
    static readonly object lockToken = new object();
    static readonly List<Action> actionQueue = new List<Action>();

    static OnNextUpdate()
    {
        EditorApplication.update += Update;
    }

    static public void Do(Action work)
    {
        lock (lockToken)
        {
            actionQueue.Add(work);
        }
    }

    static private void Update()
    {
        if (actionQueue.Count > 0)
        {
            List<Action> actionQueueCopy;
            lock (lockToken)
            {
                actionQueueCopy = new List<Action>(actionQueue);
                actionQueue.Clear();
            }
            while (actionQueueCopy.Count > 0)
            {
                actionQueueCopy[0]();
                actionQueueCopy.RemoveAt(0);
            }
        }
    }
}

