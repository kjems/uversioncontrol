using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace VersionControl
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
