// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace UVC.UserInterface
{
    [Serializable]
    internal class VCSettingsWindow : EditorWindow
    {
        [MenuItem("Window/UVC/Settings", false, 2)]
        public static void Init()
        {
            GetWindow(typeof(VCSettingsWindow), false, "Version Control Settings");
        }

        [SerializeField]
        private readonly VCSettingsGUI settingsGUI = new VCSettingsGUI();

        private void OnEnable()
        {
            minSize = new Vector2(380, 470);
        }

        private void OnGUI()
        {
            settingsGUI.DrawGUI();
        }

    }

    [Serializable]
    internal class VCSettingsGUI
    {
        private bool filterSettingsOpen = false;
        private Vector2 scrollViewVector;

        private bool VCEnabled
        {
            get { return VCSettings.VCEnabled; }
            set { VCSettings.VCEnabled = value; }
        }

        public void DrawGUI()
        {
            scrollViewVector = EditorGUILayout.BeginScrollView(scrollViewVector, false, false);
            using (GUILayoutHelper.Horizontal())
            {
                GUILayout.Label("GUI Settings", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(new GUIContent("Inspector Lock", "Version Control allowed to lock Inspector GUI for items not " + Terminology.getlock), EditorStyles.boldLabel);
                GUILayout.Space(92);
            }
            using (GUILayoutHelper.VerticalIdented(14))
            {
                using (GUILayoutHelper.Horizontal())
                {
                    VCSettings.SceneviewGUI = GUILayout.Toggle(VCSettings.SceneviewGUI, new GUIContent("Scene GUI", "Show Version Control GUI in Scene view\nDefault: On"));
                    using (GUILayoutHelper.Enabled(VCSettings.SceneviewGUI, true))
                    {
                        VCSettings.LockScenes = GUILayout.Toggle(VCSettings.SceneviewGUI && VCSettings.LockScenes, new GUIContent("GUI Lock", "Version Control allowed to lock Inspector GUI for scenes which are not " + Terminology.getlock + "\nDefault: On"), GUILayout.ExpandWidth(true), GUILayout.Width(180));
                    }
                }
                using (GUILayoutHelper.Horizontal())
                {
                    VCSettings.MaterialGUI = GUILayout.Toggle(VCSettings.MaterialGUI, new GUIContent("Material GUI", "Show Version Control GUI for material interaction on the Renderer inspector\nDefault: On"));
                }

                using (GUILayoutHelper.Horizontal())
                {
                    VCSettings.LockAssets = GUILayout.Toggle(VCSettings.LockAssets, new GUIContent("Project Asset Lock", "Version Control allowed to lock Inspector GUI for project assets which are " + Terminology.getlock + "\nDefault: On"), GUILayout.ExpandWidth(true), GUILayout.Width(180));
                }
            }

            using (GUILayoutHelper.VerticalIdented(14))
            {
                using (GUILayoutHelper.Horizontal())
                {
                    filterSettingsOpen = GUILayout.Toggle(filterSettingsOpen, new GUIContent("Path Filters", "The Inspector GUI locks will only be active on assetpaths that contains the filter below"), EditorStyles.foldout);
                }
                if (filterSettingsOpen)
                {
                    using (GUILayoutHelper.VerticalIdented(14))
                    {
                        using (GUILayoutHelper.Horizontal())
                        {
                            GUILayout.Label(new GUIContent("Scenes", "The Inspector GUI locks will only be active on assetpaths that contains the following filter.\neg. assets/scenes/"));
                            VCSettings.LockScenesFilter = EditorGUILayout.TextField(VCSettings.LockScenesFilter, GUILayout.ExpandWidth(true), GUILayout.Width(180));
                        }
                    }
                }
            }

            using (GUILayoutHelper.Horizontal())
            {
                GUILayout.Label("Reflection Level", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(new GUIContent("Reflection Level", "Select Remote to retrieve extra information from the server in an exchange for speed."), EditorStyles.boldLabel);
                GUILayout.Space(85);
            }
            using (GUILayoutHelper.VerticalIdented(14))
            {
                using (GUILayoutHelper.Horizontal())
                {
                    VCSettings.HierarchyIcons = GUILayout.Toggle(VCSettings.HierarchyIcons, new GUIContent("Hierarchy Icons", "Show Version Control controls in hierarchy view\nDefault: On"));
                    VCSettings.HierarchyReflectionMode = (VCSettings.EReflectionLevel)EditorGUILayout.EnumPopup(VCSettings.HierarchyReflectionMode, GUILayout.ExpandWidth(true), GUILayout.Width(180));
                }
                using (GUILayoutHelper.Horizontal())
                {
                    VCSettings.ProjectIcons = GUILayout.Toggle(VCSettings.ProjectIcons, new GUIContent("Project Icons", "Show Version Control controls in project view\nDefault: On"));
                    VCSettings.ProjectReflectionMode = (VCSettings.EReflectionLevel)EditorGUILayout.EnumPopup(VCSettings.ProjectReflectionMode, GUILayout.ExpandWidth(true), GUILayout.Width(180));
                }
            }

            GUILayout.Label("Commit Window", EditorStyles.boldLabel);
            using (GUILayoutHelper.VerticalIdented(14))
            {
                VCSettings.AutoCloseAfterSuccess = GUILayout.Toggle(VCSettings.AutoCloseAfterSuccess, new GUIContent("Auto Close", "Auto close commit window on successful commit\nDefault: Off"));
                VCSettings.IncludeDepedenciesAsDefault = GUILayout.Toggle(VCSettings.IncludeDepedenciesAsDefault, new GUIContent("Select Dependencies", "Should dependencies automatically be selected when opening the commit window\nDefault: On"));
                VCSettings.SelectiveCommit = GUILayout.Toggle(VCSettings.SelectiveCommit, new GUIContent("Selective Commit", "Add an additional selection column which is used to more explicitly select which files to commit\nDefault: Off"));
                VCSettings.RequireLockBeforeCommit = GUILayout.Toggle(VCSettings.RequireLockBeforeCommit, new GUIContent("Require " + Terminology.getlock + " on commit", "It will be enforced that all non-mergable files are " + Terminology.getlock + " before commit\nDefault: Off"));
            }
            GUILayout.Label("Debug", EditorStyles.boldLabel);
            using (GUILayoutHelper.VerticalIdented(14))
            {
                using (GUILayoutHelper.Horizontal())
                {
                    VCSettings.BugReport = GUILayout.Toggle(VCSettings.BugReport, new GUIContent("Bug Reports", "Send a bug report to Fogbugz when an error occurs\nDefault: On"));
                    VCSettings.BugReportMode = (VCSettings.EBugReportMode)EditorGUILayout.EnumPopup(VCSettings.BugReportMode, GUILayout.ExpandWidth(true), GUILayout.Width(180));
                }
                VCSettings.Analytics = GUILayout.Toggle(VCSettings.Analytics, new GUIContent("Analytics", "Allow UVC to send anonymous analytics data with the purpose of improving the quality of UVC\nDefault: On"));
                VCSettings.Logging = GUILayout.Toggle(VCSettings.Logging, new GUIContent("Logging", "Output logs from Version Control to Unity console\nDefault: Off"));
            }
            GUILayout.Label("Advanced", EditorStyles.boldLabel);
            using (GUILayoutHelper.VerticalIdented(14))
            {
                using (GUILayoutHelper.Horizontal())
                {
                    GUILayout.Label(new GUIContent("How to Move and Rename", "How should file move and renames in project be handled\nDefault: Simple"));
                    VCSettings.HandleFileMove = (VCSettings.EHandleFileMove)EditorGUILayout.EnumPopup(VCSettings.HandleFileMove, GUILayout.ExpandWidth(true), GUILayout.Width(180));
                }
                using (GUILayoutHelper.Horizontal())
                {
                    GUILayout.Label(new GUIContent(string.Format("Who Controls Asset Saves", Terminology.getlock),
                        $"Select {VCSettings.ESaveAssetsStrategy.VersionControl.ToString()} to only let Unity save files that are either {Terminology.allowLocalEdit} or {Terminology.getlock} \nDefault: {VCSettings.ESaveAssetsStrategy.Unity.ToString()}"));
                    VCSettings.SaveStrategy = (VCSettings.ESaveAssetsStrategy)EditorGUILayout.EnumPopup(VCSettings.SaveStrategy, GUILayout.ExpandWidth(true), GUILayout.Width(180));
                }
                using (GUILayoutHelper.Horizontal())
                {
                    GUILayout.Label(new GUIContent("Version Control System", "The selected Version Control will be used if a valid local copy can be found"));
                    VCSettings.VersionControlBackend = (VCSettings.EVersionControlBackend)EditorGUILayout.EnumPopup(VCSettings.VersionControlBackend, GUILayout.ExpandWidth(true), GUILayout.Width(180));
                }
                using (GUILayoutHelper.Horizontal())
                {
                    GUILayout.Label(new GUIContent("External Merge Tool", "The selected Merge Tool must be installed"));
                    EditorGUI.BeginChangeCheck();
                    VCSettings.MergeToolIndex = EditorGUILayout.Popup(VCSettings.MergeToolIndex, MergeToolNames, GUILayout.ExpandWidth(true), GUILayout.Width(180));
                    if (EditorGUI.EndChangeCheck())
                    {
                        var mergeTool = VCSettings.mergeTools[VCSettings.MergeToolIndex];
                        VCSettings.MergetoolPath = mergeTool.pathMerge.Replace("~",GetUserHomePath());
                        VCSettings.MergetoolArgs = mergeTool.argumentsMerge;
                        VCSettings.DifftoolPath = mergeTool.pathDiff.Replace("~",GetUserHomePath());
                        VCSettings.DifftoolArgs = mergeTool.argumentsDiff;
                    }
                }
                using (GUILayoutHelper.VerticalIdented(14))
                {
                    bool validmergeTool = ValidCommandLine(VCSettings.MergetoolPath);
                    using (GUILayoutHelper.Horizontal())
                    {
                        GUILayout.Label(new GUIContent("Merge Tool Path", validmergeTool ? "Valid path to command line tool" : "Invalid path to command line tool"));
                    }
                    using (GUILayoutHelper.VerticalIdented(14))
                    {
                        using (GUILayoutHelper.Color(validmergeTool ? validColor : invalidColor))
                        {
                            VCSettings.MergetoolPath = EditorGUILayout.TextField(VCSettings.MergetoolPath, GUILayout.ExpandWidth(true)).Replace('\\', '/').Replace("~",GetUserHomePath());
                        }
                    }
                    GUILayout.Label(new GUIContent("Merge Tool Arguments", "Include : [base] [theirs] [yours] [merge]"));
                    using (GUILayoutHelper.VerticalIdented(14))
                    {
                        VCSettings.MergetoolArgs = EditorGUILayout.TextField(VCSettings.MergetoolArgs, GUILayout.ExpandWidth(true));
                    }

                    bool validDiffTool = ValidCommandLine(VCSettings.DifftoolPath);
                    using (GUILayoutHelper.Horizontal())
                    {
                        GUILayout.Label(new GUIContent("Diff Tool Path", validDiffTool ? "Valid path to command line tool" : "Invalid path to command line tool"));
                    }

                    using (GUILayoutHelper.VerticalIdented(14))
                    {
                        using (GUILayoutHelper.Color(validDiffTool ? validColor : invalidColor))
                        {
                            VCSettings.DifftoolPath = EditorGUILayout.TextField(VCSettings.DifftoolPath, GUILayout.ExpandWidth(true)).Replace('\\', '/').Replace("~",GetUserHomePath());
                        }
                    }
                    GUILayout.Label(new GUIContent("Diff Tool Arguments", "Include : [theirs] [yours]"));
                    using (GUILayoutHelper.VerticalIdented(14))
                    {
                        VCSettings.DifftoolArgs = EditorGUILayout.TextField(VCSettings.DifftoolArgs, GUILayout.ExpandWidth(true));
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        static Color validColor = new Color(0.0f, 0.6f, 0.0f);
        static Color invalidColor = new Color(0.6f, 0.0f, 0.0f);

        static string[] mergeToolNames;
        static string[] MergeToolNames => mergeToolNames ?? (mergeToolNames = VCSettings.mergeTools.Select(mt => mt.name).ToArray());

        static string GetUserHomePath()
        {
            return (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
                    ? Environment.GetEnvironmentVariable("HOME")
                    : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
        }

        static bool ValidCommandLine(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }
    }
}
