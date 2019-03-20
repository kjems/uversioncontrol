// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using System.Collections.Generic;
using UnityEditor;

internal static class ContinuationManager
{
    private sealed class Job
    {
        public Job(System.Func<bool> completed, System.Action continueWith)
        {
            Completed = completed;
            ContinueWith = continueWith;
        }
        public System.Func<bool> Completed { get; private set; }
        public System.Action ContinueWith { get; private set; }
    }

    private static readonly List<Job> jobs = new List<Job>();

    public static void Add(System.Func<bool> completed, System.Action continueWith)
    {
        if (jobs.Count == 0)
            EditorApplication.update += Update;
        jobs.Add(new Job(completed, continueWith));
    }

    private static void Update()
    {
        try
        {
            for (int i = jobs.Count - 1; i >= 0; --i)
            {
                var jobIt = jobs[i];
                if (jobIt.Completed())
                {
                    jobs.RemoveAt(i);
                    jobIt.ContinueWith();
                }
            }
        }
        finally
        {
            if (jobs.Count == 0)
                EditorApplication.update -= Update;
        }
    }
}
