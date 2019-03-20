// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEngine;
using UnityEngine.Profiling;
using System.Diagnostics;

namespace UVC
{
    public static class ProfilerUtilities
    {
        [Conditional("UVC_ENABLE_PROFILER")]
        public static void BeginSample(string name)
        {
           Profiler.BeginSample(name);
        }

        [Conditional("UVC_ENABLE_PROFILER")]
        public static void BeginSample(string name, Object targetObject)
        {
            Profiler.BeginSample(name, targetObject);
        }

        [Conditional("UVC_ENABLE_PROFILER")]
        public static void EndSample()
        {
            Profiler.EndSample();
        }
    }
}