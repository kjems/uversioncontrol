// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using UnityEngine;
using UnityEditor;

namespace VersionControl
{
    [InitializeOnLoad]
    internal static class VCValidateConfiguration
    {
		static readonly string[] defaultIgnores = new[] { "Library", "Temp", "obj", "*.booproj" ,"*.unityproj" ,"*.csproj", "*.sln", "*.suo", "*.user", "*.pidb", "*.userprefs", "*.user", "*.ide" , "_ReSharper.*" };

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

        [MenuItem("UVC/Re-Validate Setup", false, 1)]
        private static void ValidateMenuItem()
        {
            ValidateIgnoreFolders(true);
        }
        public static void ValidateIgnoreFolders(bool forceValidate)
        {
            string prefsKey = "ProjectValidated/" + Application.dataPath;
            if (!EditorPrefs.GetBool(prefsKey) || forceValidate)
            {
                const string title = "Validate Setup?";
                const string message = "Do you want UVC to automatically setup file ignores for files which should not be managed by Version Control?";
                if (EditorUtility.DisplayDialog(title, message, "Yes", "No"))
                {
                    VCCommands.Instance.Ignore(".", defaultIgnores);
                }
                EditorPrefs.SetBool(prefsKey, true);
            }
        }

    }
}