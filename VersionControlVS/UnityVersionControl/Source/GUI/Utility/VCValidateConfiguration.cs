// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace VersionControl
{
    [InitializeOnLoad]
    internal static class VCValidateConfiguration
    {
        static readonly string[] defaultIgnores = 
        { "Library", "Temp", "obj", "*.booproj", "*.unityproj", "*.csproj", "*.sln", 
          "*.suo", "*.user", "*.pidb", "*.userprefs", "*.user", "*.ide", "_ReSharper.*" };

        static VCValidateConfiguration()
        {
            VCCommands.Instance.StatusCompleted += ValidateIgnoreFoldersInternal;
        }
        private static void ValidateIgnoreFoldersInternal()
        {
            if (VCCommands.Instance.IsReady())
            {
                VCCommands.Instance.StatusCompleted -= ValidateIgnoreFoldersInternal;
                ValidateIgnoreFolders(false);
            }
        }

        [MenuItem("UVC/Validate Setup", false, 1)]
        private static void ValidateMenuItem()
        {
            ValidateIgnoreFolders(true);
        }
        public static void ValidateIgnoreFolders(bool forceValidate)
        {
            string workDirectory = Application.dataPath.Remove(Application.dataPath.LastIndexOf("/Assets", StringComparison.InvariantCultureIgnoreCase));
            var ignores = VCCommands.Instance.GetIgnore(workDirectory);
            if (ignores != null)
            {
                bool needSetIgnore = !ignores.Contains("Library") || !ignores.Contains("Temp");
                if (needSetIgnore || forceValidate)
                {
                    const string title = "Fix ignores?";
                    const string message = "Do you want UVC to automatically fix file and folder ignores?";
                    if (EditorUtility.DisplayDialog(title, message, "Fix it", "No"))
                    {
                        VCCommands.Instance.SetIgnore(workDirectory, defaultIgnores);
                    }
                }
            }
        }

    }
}