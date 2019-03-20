// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Threading.Tasks;

namespace UVC
{
    public static class TaskExtensions
    {
        public static Task<T> ContinueWithOnNextUpdate<T>(this Task<T> task, Action<T> postAction)
        {
            return task.ContinueWith(NextUpdate(postAction));
        }
        
        private static Func<Task<T>, T> NextUpdate<T>(Action<T> postAction)
        {
            return t =>
            {
                OnNextUpdate.Do(() => postAction(t.Result));
                return t.Result;
            };
        }
        
        public static Task ContinueWithOnNextUpdate(this Task task, Action postAction)
        {
            return task.ContinueWith(NextUpdate(postAction));
        }

        private static Action<Task> NextUpdate(Action postAction)
        {
            return t => OnNextUpdate.Do(postAction);
        }
    }
}
