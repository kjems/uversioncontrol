// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEngine;
using UnityEditor;
using System;

namespace VersionControl
{
    [InitializeOnLoad]
    public class VCSettings
    {
        static VCSettings()
        {
            vcEnabled = EditorPrefs.GetBool("VCSSettings/vcEnabled", true);
            lockPrefabs = EditorPrefs.GetBool("VCSSettings/lockPrefabs", false);
            lockScenes = EditorPrefs.GetBool("VCSSettings/lockScenes", true);
            lockMaterials = EditorPrefs.GetBool("VCSSettings/lockMaterials", true);
            sceneviewGUI = EditorPrefs.GetBool("VCSSettings/sceneviewGUI", true);
            materialGUI = EditorPrefs.GetBool("VCSSettings/materialGUI", true);
            hierarchyIcons = EditorPrefs.GetBool("VCSSettings/hierarchyIcons", true);
            hierarchyReflectionMode = (EReflectionLevel)EditorPrefs.GetInt("VCSSettings/hierarchyReflectionMode", (int)EReflectionLevel.Remote);
            projectIcons = EditorPrefs.GetBool("VCSSettings/projectIcons", true);
            projectReflectionMode = (int)EReflectionLevel.Local;//(EReflectionLevel)EditorPrefs.GetInt("VCSSettings/projectReflectionMode", (int)EReflectionLevel.Local);
            bugReport = EditorPrefs.GetBool("VCSSettings/bugReport", true);
            bugReportMode = (EBugReportMode)EditorPrefs.GetInt("VCSSettings/bugReportMode", (int)EBugReportMode.Manual);
            Logging = EditorPrefs.GetBool("VCSSettings/logging", false); // using Logging property instead of field by intention
            lockScenesFilter = EditorPrefs.GetString("VCSSettings/lockScenesFilter");
            lockPrefabsFilter = EditorPrefs.GetString("VCSSettings/lockPrefabsFilter");
            lockMaterialsFilter = EditorPrefs.GetString("VCSSettings/lockMaterialsFilter");
            ClientPath = EditorPrefs.GetString("VCSSettings/clientPath"); // using ClientPath property instead of field by intention
            OnSettingsChanged();
            
            AppDomain.CurrentDomain.DomainUnload += (o, d) =>
            {
                EditorPrefs.SetBool("VCSSettings/vcEnabled", vcEnabled);
                EditorPrefs.SetBool("VCSSettings/lockPrefabs", lockPrefabs);
                EditorPrefs.SetBool("VCSSettings/lockScenes", lockScenes);
                EditorPrefs.SetBool("VCSSettings/lockMaterials", lockMaterials);
                EditorPrefs.SetBool("VCSSettings/sceneviewGUI", sceneviewGUI);
                EditorPrefs.SetBool("VCSSettings/materialGUI", materialGUI);
                EditorPrefs.SetBool("VCSSettings/hierarchyIcons", hierarchyIcons);
                EditorPrefs.SetInt("VCSSettings/hierarchyReflectionMode", (int)hierarchyReflectionMode);
                EditorPrefs.SetBool("VCSSettings/projectIcons", projectIcons);
                EditorPrefs.SetInt("VCSSettings/projectReflectionMode", (int)projectReflectionMode);
                EditorPrefs.SetBool("VCSSettings/bugReport", bugReport);
                EditorPrefs.SetInt("VCSSettings/bugReportMode", (int)bugReportMode);
                EditorPrefs.SetBool("VCSSettings/logging", logging);
                EditorPrefs.SetString("VCSSettings/lockScenesFilter", lockScenesFilter);
                EditorPrefs.SetString("VCSSettings/lockPrefabsFilter", lockPrefabsFilter);
                EditorPrefs.SetString("VCSSettings/lockMaterialsFilter", lockMaterialsFilter);
                EditorPrefs.SetString("VCSSettings/clientPath", clientPath);
            };
        }

        public enum EBugReportMode { Automatic, Manual }
        public enum EReflectionLevel { Local, Remote }

        public static event Action SettingChanged;

        [SerializeField] private static bool vcEnabled = true;
        public static bool VCEnabled
        {
            get { return vcEnabled; }
            set
            {
                if (vcEnabled != value)
                {
                    vcEnabled = value;
                    if (vcEnabled) VCCommands.Instance.Start();
                    else VCCommands.Instance.Stop();
                }
                OnSettingsChanged();
            }
        }

        private static void OnSettingsChanged()
        {
            if (SettingChanged != null) SettingChanged();
        }

        [SerializeField] private static bool lockPrefabs;
        public static bool LockPrefabs { get { return lockPrefabs; } set { if (lockPrefabs != value) {lockPrefabs = value; OnSettingsChanged();} } }
        
        [SerializeField] private static bool lockScenes;
        public static bool LockScenes { get { return lockScenes; } set { if (lockScenes != value) { lockScenes = value; OnSettingsChanged(); } } }
        
        [SerializeField] private static bool lockMaterials;
        public static bool LockMaterials { get { return lockMaterials; } set { if (lockMaterials != value) { lockMaterials = value; OnSettingsChanged(); } } }
        
        [SerializeField] private static bool sceneviewGUI;
        public static bool SceneviewGUI { get { return sceneviewGUI; } set { if (sceneviewGUI != value) { sceneviewGUI = value; OnSettingsChanged(); } } }

        [SerializeField] private static bool materialGUI;
        public static bool MaterialGUI { get { return materialGUI; } set { if (materialGUI != value) { materialGUI = value; OnSettingsChanged(); } } }

        [SerializeField] private static bool hierarchyIcons;
        public static bool HierarchyIcons { get { return hierarchyIcons; } set { if (hierarchyIcons != value) { hierarchyIcons = value; OnSettingsChanged(); } } }

        [SerializeField]
        private static EReflectionLevel hierarchyReflectionMode;
        public static EReflectionLevel HierarchyReflectionMode { get { return hierarchyReflectionMode; } set { if (hierarchyReflectionMode != value) { hierarchyReflectionMode = value; } } }
        
        [SerializeField] private static bool projectIcons;
        public static bool ProjectIcons { get { return projectIcons; } set { if (projectIcons != value) { projectIcons = value; OnSettingsChanged(); } } }

        [SerializeField]
        private static EReflectionLevel projectReflectionMode;
        public static EReflectionLevel ProjectReflectionMode { get { return projectReflectionMode; } set { if (projectReflectionMode != value) { projectReflectionMode = value; } } }
        
        [SerializeField] private static bool bugReport;
        public static bool BugReport { get { return bugReport; } set { if (bugReport != value) { bugReport = value; OnSettingsChanged(); } } }
        
        [SerializeField] private static EBugReportMode bugReportMode;
        public static EBugReportMode BugReportMode { get { return bugReportMode; } set { if (bugReportMode != value) { bugReportMode = value; } } }
        
        [SerializeField]private static string lockScenesFilter;
        public static string LockScenesFilter { get { return lockScenesFilter; } set { if (lockScenesFilter != value) { lockScenesFilter = value.TrimStart(new[] { ' ', '/' }); } } }
        
        [SerializeField]private static string lockPrefabsFilter;
        public static string LockPrefabsFilter { get { return lockPrefabsFilter; } set { if (lockPrefabsFilter != value) { lockPrefabsFilter = value.TrimStart(new[] { ' ', '/' }); } } }
        
        [SerializeField]private static string lockMaterialsFilter;
        public static string LockMaterialsFilter { get { return lockMaterialsFilter; } set { if (lockMaterialsFilter != value) { lockMaterialsFilter = value.TrimStart(new[] { ' ', '/' }); } } }

        [SerializeField]private static string clientPath;
        public static string ClientPath
        {
            get { return clientPath; } 
            set { 
                if (clientPath != value)
                {
                    clientPath = value;
                    if (!string.IsNullOrEmpty(clientPath)) EnvironmentManager.AddPathEnvironment(clientPath,  ":");
                    else EnvironmentManager.ResetPathEnvironment();
                }
            }
        }

        public static bool Logging
        {
            get { return logging; }
            set
            {
                if (logging != value)
                {
                    logging = value;
                    if (logging) D.writeLogCallback += Debug.Log;
                    else if (D.writeLogCallback != null) D.writeLogCallback -= Debug.Log;
                }
            }
        }
        private static bool logging;
    }
}


