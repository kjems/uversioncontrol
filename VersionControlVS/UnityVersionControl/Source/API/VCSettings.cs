// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnitySVN@gmail.com>
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
            hierarchyIcons = EditorPrefs.GetBool("VCSSettings/hierarchyIcons", true);
            projectIcons = EditorPrefs.GetBool("VCSSettings/projectIcons", true);
            bugReport = EditorPrefs.GetBool("VCSSettings/bugReport", true);
            bugReportMode = (EBugReportMode)EditorPrefs.GetInt("VCSSettings/bugReportMode", (int)EBugReportMode.Manual);
            Logging = EditorPrefs.GetBool("VCSSettings/logging", false); // using Logging property instead of field by intention
            lockScenesFilter = EditorPrefs.GetString("VCSSettings/lockScenesFilter");
            lockPrefabsFilter = EditorPrefs.GetString("VCSSettings/lockPrefabsFilter");
            lockMaterialsFilter = EditorPrefs.GetString("VCSSettings/lockMaterialsFilter");
            OnSettingsChanged();
            
            AppDomain.CurrentDomain.DomainUnload += (o, d) =>
            {
                EditorPrefs.SetBool("VCSSettings/vcEnabled", vcEnabled);
                EditorPrefs.SetBool("VCSSettings/lockPrefabs", lockPrefabs);
                EditorPrefs.SetBool("VCSSettings/lockScenes", lockScenes);
                EditorPrefs.SetBool("VCSSettings/lockMaterials", lockMaterials);
                EditorPrefs.SetBool("VCSSettings/sceneviewGUI", sceneviewGUI);
                EditorPrefs.SetBool("VCSSettings/hierarchyIcons", hierarchyIcons);
                EditorPrefs.SetBool("VCSSettings/projectIcons", projectIcons);
                EditorPrefs.SetBool("VCSSettings/bugReport", bugReport);
                EditorPrefs.SetInt("VCSSettings/bugReportMode", (int)bugReportMode);
                EditorPrefs.SetBool("VCSSettings/logging", logging);
                EditorPrefs.SetString("VCSSettings/lockScenesFilter", lockScenesFilter);
                EditorPrefs.SetString("VCSSettings/lockPrefabsFilter", lockPrefabsFilter);
                EditorPrefs.SetString("VCSSettings/lockMaterialsFilter", lockMaterialsFilter);

            };
        }

        public enum EBugReportMode { Automatic, Manual }

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
                    if (vcEnabled)
                    {
                        VCCommands.Instance.RequestStatus();
                    }
                    else
                    {
                        VCCommands.Instance.ClearDatabase();
                    }
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
        
        [SerializeField] private static bool hierarchyIcons;
        public static bool HierarchyIcons { get { return hierarchyIcons; } set { if (hierarchyIcons != value) { hierarchyIcons = value; OnSettingsChanged(); } } }
        
        [SerializeField] private static bool projectIcons;
        public static bool ProjectIcons { get { return projectIcons; } set { if (projectIcons != value) { projectIcons = value; OnSettingsChanged(); } } }
        
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


