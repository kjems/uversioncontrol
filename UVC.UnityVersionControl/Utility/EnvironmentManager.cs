// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using UnityEditor;

namespace UVC
{
    [InitializeOnLoad]
    class EnvironmentManager
    {
        private const string pathIdentifier = "PATH";
        private static readonly string initialPathEnv;
        static EnvironmentManager()
        {
            initialPathEnv = Environment.GetEnvironmentVariable(pathIdentifier);
            Environment.SetEnvironmentVariable("LC_ALL", "C");
        }

        public static void AddPathEnvironment(string value, string delimiter)
        {
            var current = Environment.GetEnvironmentVariable(pathIdentifier);
            if (current != null) SetEnvironment(pathIdentifier, value + delimiter + current);
            else SetEnvironment(pathIdentifier, value);
        }

        public static void ResetPathEnvironment()
        {
            SetEnvironment(pathIdentifier, initialPathEnv);
        }

        public static void SetEnvironment(string key, string value)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
