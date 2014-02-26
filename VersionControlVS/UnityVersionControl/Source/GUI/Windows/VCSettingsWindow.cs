// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace VersionControl.UserInterface
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
            minSize = new Vector2(320, 430);
        }

        private void OnGUI()
        {
            settingsGUI.DrawGUI();
        }

    }

    [Serializable]
    internal class VCSettingsGUI
    {
        private string clientPath = null;
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
                    VCSettings.PrefabGUI = GUILayout.Toggle(VCSettings.PrefabGUI, new GUIContent("Prefab GUI", "Show Version Control GUI for prefabs in hierarchy view\nDefault: On"));
                    using (GUILayoutHelper.Enabled(VCSettings.PrefabGUI, true))
                    {
                        VCSettings.LockPrefabs = GUILayout.Toggle(VCSettings.LockPrefabs && VCSettings.PrefabGUI, new GUIContent("GUI Lock", "Version Control allowed to lock Inspector GUI for prefabs which are not " + Terminology.getlock + "\nDefault: Off"), GUILayout.ExpandWidth(true), GUILayout.Width(180));
                    }
                }
                using (GUILayoutHelper.Horizontal())
                {
                    VCSettings.MaterialGUI = GUILayout.Toggle(VCSettings.MaterialGUI, new GUIContent("Material GUI", "Show Version Control GUI for material interaction on the Renderer inspector\nDefault: On"));
                    using (GUILayoutHelper.Enabled(VCSettings.MaterialGUI, true))
                    {
                        VCSettings.LockMaterials = GUILayout.Toggle(VCSettings.MaterialGUI && VCSettings.LockMaterials, new GUIContent("GUI Lock", "Version Control allowed to lock Inspector GUI for materials which are not " + Terminology.getlock + "\nDefault: On"), GUILayout.ExpandWidth(true), GUILayout.Width(180));
                    }
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
                        using (GUILayoutHelper.Horizontal())
                        {
                            GUILayout.Label(new GUIContent("Prefabs", "The Inspector GUI locks will only be active on assetpaths that contains the following filter.\neg. assets/prefabs/"));
                            VCSettings.LockPrefabsFilter = EditorGUILayout.TextField(VCSettings.LockPrefabsFilter, GUILayout.ExpandWidth(true), GUILayout.Width(180));
                        }
                        using (GUILayoutHelper.Horizontal())
                        {
                            GUILayout.Label(new GUIContent("Materials", "The Inspector GUI locks will only be active on assetpaths that contains the following filter.\neg. assets/materials/"));
                            VCSettings.LockMaterialsFilter = EditorGUILayout.TextField(VCSettings.LockMaterialsFilter, GUILayout.ExpandWidth(true), GUILayout.Width(180));
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
                    VCSettings.HierarchyIcons = GUILayout.Toggle(VCSettings.HierarchyIcons, new GUIContent("Hierachy Icons", "Show Version Control controls in hierachy view\nDefault: On"));
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
                    GUILayout.Label(new GUIContent(string.Format("Who Controls Asset Saves", Terminology.getlock), string.Format("(Requires Unity Team License) Select {0} to only let Unity save files that are either {1} or {2} \nDefault: {3}", VCSettings.ESaveAssetsStrategy.VersionControl.ToString(), Terminology.allowLocalEdit, Terminology.getlock, VCSettings.ESaveAssetsStrategy.Unity.ToString())));
                    VCSettings.SaveStrategy = (VCSettings.ESaveAssetsStrategy)EditorGUILayout.EnumPopup(VCSettings.SaveStrategy, GUILayout.ExpandWidth(true), GUILayout.Width(180));
                }
                if (clientPath == null) clientPath = VCSettings.ClientPath;
                var textColor = ValidCommandLineClient(clientPath) ? new Color(0.0f, 0.6f, 0.0f) : new Color(0.6f, 0.0f, 0.0f);
                var textStyle = new GUIStyle(EditorStyles.textField) { normal = { textColor = textColor } };
                using (GUILayoutHelper.Horizontal())
                {
                    GUILayout.Label(new GUIContent("Environment Path", "Specify the path to a command line client. eg MacPorts SVN : /opt/local/bin/\nDefault: <Empty>"));
                    clientPath = EditorGUILayout.TextField(clientPath, textStyle, GUILayout.ExpandWidth(true), GUILayout.Width(180)).Trim(new[] { ' ' }).Replace('\\', '/');
                }
                if (ValidCommandLineClient(clientPath)) VCSettings.ClientPath = clientPath;
                using (GUILayoutHelper.Horizontal())
                {
                    GUILayout.Label(new GUIContent("Version Control System", "The selected Version Control will be used if a valid local copy can be found"));
                    VCSettings.VersionControlBackend = (VCSettings.EVersionControlBackend)EditorGUILayout.EnumPopup(VCSettings.VersionControlBackend, GUILayout.ExpandWidth(true), GUILayout.Width(180));
                }
            }
            EditorGUILayout.EndScrollView();
        }

        static bool ValidCommandLineClient(string path)
        {
            return string.IsNullOrEmpty(path) || Directory.Exists(path);
        }
    }
}