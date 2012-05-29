using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace VersionControl
{
    [InitializeOnLoad]
    class EnvironmentManager
    {
        static EnvironmentManager()
        {
            Environment.SetEnvironmentVariable("LC_ALL", "C");
        }

        public static void AddEnvironment(string key, string value, string delimiter)
        {
            var current = Environment.GetEnvironmentVariable(key);
            if (current != null) Environment.SetEnvironmentVariable(key, current + delimiter + value);
            else Environment.SetEnvironmentVariable(key, value);
        }

        public static void SetEnvironment(string key, string value)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
