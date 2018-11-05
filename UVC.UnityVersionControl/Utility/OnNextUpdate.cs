// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

// This class makes sure an operation performed is executed within the scope of 
// Unity engines update loop. This is used by the callback functions from an
// asynchronous operation that although executing on the same thread as Unity
// not necessarily execute within the update scope.

using System;
using System.Collections.Generic;
using UnityEditor;

namespace UVC
{
    using Logging;
    [InitializeOnLoad]
    public static class OnNextUpdate
    {
        private static readonly object mLockToken = new object();
        private static readonly Queue<Action> mActionQueue = new Queue<Action>();

        static OnNextUpdate()
        {
            EditorApplication.update += Update;
        }

        public static void Do(Action work)
        {
            lock (mLockToken)
            {
                mActionQueue.Enqueue(work);
            }
        }

        private static void Update()
        {
            if (mActionQueue.Count > 0)
            {
                Queue<Action> actionQueueCopy;
                lock (mLockToken)
                {
                    actionQueueCopy = new Queue<Action>(mActionQueue);
                    mActionQueue.Clear();
                }
                while (actionQueueCopy.Count > 0)
                {
                    try
                    {
                        actionQueueCopy.Dequeue()();
                    }
                    catch (Exception e)
                    {
                        DebugLog.ThrowException(e);
                    }
                }
            }
        }
    }
}
