// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEngine;
using UnityEditor;
using System;

namespace VersionControl
{
    using Logging;
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
            prefabGUI = EditorPrefs.GetBool("VCSSettings/prefabGUI", true);
            materialGUI = EditorPrefs.GetBool("VCSSettings/materialGUI", true);            
            hierarchyIcons = EditorPrefs.GetBool("VCSSettings/hierarchyIcons", true);
            hierarchyReflectionMode = (EReflectionLevel)EditorPrefs.GetInt("VCSSettings/hierarchyReflectionMode", (int)EReflectionLevel.Remote);
            projectIcons = EditorPrefs.GetBool("VCSSettings/projectIcons", true);
            projectReflectionMode = (EReflectionLevel)EditorPrefs.GetInt("VCSSettings/projectReflectionMode", (int)EReflectionLevel.Local);
            bugReport = EditorPrefs.GetBool("VCSSettings/bugReport", true);
            analytics = EditorPrefs.GetBool("VCSSettings/analytics", true);
            bugReportMode = (EBugReportMode)EditorPrefs.GetInt("VCSSettings/bugReportMode", (int)EBugReportMode.Manual);
            Logging = EditorPrefs.GetBool("VCSSettings/logging", false); // using Logging property instead of field by intention
            lockScenesFilter = EditorPrefs.GetString("VCSSettings/lockScenesFilter");
            lockPrefabsFilter = EditorPrefs.GetString("VCSSettings/lockPrefabsFilter");
            lockMaterialsFilter = EditorPrefs.GetString("VCSSettings/lockMaterialsFilter");
            ClientPath = EditorPrefs.GetString("VCSSettings/clientPath"); // using ClientPath property instead of field by intention
            autoCloseAfterSuccess = EditorPrefs.GetBool("VCSSettings/autoCloseAfterSuccess", false);
            includeDepedenciesAsDefault = EditorPrefs.GetBool("VCSSettings/includeDepedenciesAsDefault", true);
            requireLockBeforeCommit = EditorPrefs.GetBool("VCSSettings/requireLockBeforeCommit", false);
            selectiveCommit = EditorPrefs.GetBool("VCSSettings/selectiveCommit", false);
            saveStrategy = (ESaveAssetsStrategy)EditorPrefs.GetInt("VCSSettings/preventSaveNoLock", (int)ESaveAssetsStrategy.Unity);
            versionControlBackend = (EVersionControlBackend)EditorPrefs.GetInt("VCSSettings/versionControlBackend", (int)EVersionControlBackend.None);

            OnSettingsChanged();

            AppDomain.CurrentDomain.DomainUnload += (o, d) =>
            {
                EditorPrefs.SetBool("VCSSettings/vcEnabled", vcEnabled);
                EditorPrefs.SetBool("VCSSettings/lockPrefabs", lockPrefabs);
                EditorPrefs.SetBool("VCSSettings/lockScenes", lockScenes);
                EditorPrefs.SetBool("VCSSettings/lockMaterials", lockMaterials);
                EditorPrefs.SetBool("VCSSettings/sceneviewGUI", sceneviewGUI);
                EditorPrefs.SetBool("VCSSettings/prefabGUI", prefabGUI);
                EditorPrefs.SetBool("VCSSettings/materialGUI", materialGUI);
                EditorPrefs.SetBool("VCSSettings/hierarchyIcons", hierarchyIcons);
                EditorPrefs.SetInt("VCSSettings/hierarchyReflectionMode", (int)hierarchyReflectionMode);
                EditorPrefs.SetBool("VCSSettings/projectIcons", projectIcons);
                EditorPrefs.SetInt("VCSSettings/projectReflectionMode", (int)projectReflectionMode);
                EditorPrefs.SetBool("VCSSettings/bugReport", bugReport);
                EditorPrefs.SetBool("VCSSettings/analytics", analytics);
                EditorPrefs.SetInt("VCSSettings/bugReportMode", (int)bugReportMode);
                EditorPrefs.SetBool("VCSSettings/logging", logging);
                EditorPrefs.SetString("VCSSettings/lockScenesFilter", lockScenesFilter);
                EditorPrefs.SetString("VCSSettings/lockPrefabsFilter", lockPrefabsFilter);
                EditorPrefs.SetString("VCSSettings/lockMaterialsFilter", lockMaterialsFilter);
                EditorPrefs.SetString("VCSSettings/clientPath", clientPath);
                EditorPrefs.SetBool("VCSSettings/autoCloseAfterSuccess", autoCloseAfterSuccess);
                EditorPrefs.SetBool("VCSSettings/includeDepedenciesAsDefault", includeDepedenciesAsDefault);
                EditorPrefs.SetBool("VCSSettings/requireLockBeforeCommit", requireLockBeforeCommit);
                EditorPrefs.SetBool("VCSSettings/selectiveCommit", selectiveCommit);
                EditorPrefs.SetInt("VCSSettings/saveStrategy", (int)saveStrategy);
                EditorPrefs.SetInt("VCSSettings/versionControlBackend", (int)versionControlBackend);
            };
        }

        public enum EBugReportMode { Automatic, Manual }
        public enum EReflectionLevel { Local, Remote }
        public enum EVersionControlBackend { None, Svn, Perforce }
        public enum ESaveAssetsStrategy { Unity, VersionControl }

        public static event Action SettingChanged;        
        
        private static bool vcEnabled = true;
        public static bool VCEnabled
        {
            get { return vcEnabled; }
            set
            {
                if (vcEnabled != value)
                {
                    if (value)
                    {
                        if (VCUtility.UserSelectedVersionControlSystem())
                        {
                            vcEnabled = true;
                            VCCommands.Instance.Start();
                        }
                    }
                    else
                    {
                        vcEnabled = false;
                        VCCommands.Instance.Stop();
                    }
                }
                OnSettingsChanged();
            }
        }

        private static void OnSettingsChanged()
        {
            if (SettingChanged != null) SettingChanged();
        }

        
        private static EVersionControlBackend versionControlBackend;
        public static EVersionControlBackend VersionControlBackend 
        { 
            get { return versionControlBackend; } 
            set { 
                if (versionControlBackend != value) 
                {
                    if (value == EVersionControlBackend.None) VCSettings.VCEnabled = false;
                    string errors = "";
                    Action<string> addToErrors = err => errors += "\n" + err;
                    D.combinedShorthandCallback += addToErrors;
                    
                    if (VersionControlFactory.CreateVersionControlCommands(value))
                    {
                        versionControlBackend = value;
                        OnSettingsChanged();
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Version Control Selection failed", "Unable to initialize '" + value + "'.\n\n" + errors, "OK");
                    }
                    D.combinedShorthandCallback -= addToErrors;
                    
                } 
            } 
        }

        
        private static bool lockPrefabs;
        public static bool LockPrefabs { get { return lockPrefabs; } set { if (lockPrefabs != value) { lockPrefabs = value; OnSettingsChanged(); } } }

        
        private static bool lockScenes;
        public static bool LockScenes { get { return lockScenes; } set { if (lockScenes != value) { lockScenes = value; OnSettingsChanged(); } } }

        
        private static bool lockMaterials;
        public static bool LockMaterials { get { return lockMaterials; } set { if (lockMaterials != value) { lockMaterials = value; OnSettingsChanged(); } } }

        
        private static bool sceneviewGUI;
        public static bool SceneviewGUI { get { return sceneviewGUI; } set { if (sceneviewGUI != value) { sceneviewGUI = value; OnSettingsChanged(); } } }
        
        
        private static bool prefabGUI;
        public static bool PrefabGUI { get { return prefabGUI; } set { if (prefabGUI != value) { prefabGUI = value; OnSettingsChanged(); } } }

        
        private static bool materialGUI;
        public static bool MaterialGUI { get { return materialGUI; } set { if (materialGUI != value) { materialGUI = value; OnSettingsChanged(); } } }

        
        private static bool hierarchyIcons;
        public static bool HierarchyIcons { get { return hierarchyIcons; } set { if (hierarchyIcons != value) { hierarchyIcons = value; OnSettingsChanged(); } } }

        
        private static EReflectionLevel hierarchyReflectionMode;
        public static EReflectionLevel HierarchyReflectionMode { get { return hierarchyReflectionMode; } set { if (hierarchyReflectionMode != value) { hierarchyReflectionMode = value; } } }

        
        private static bool projectIcons;
        public static bool ProjectIcons { get { return projectIcons; } set { if (projectIcons != value) { projectIcons = value; OnSettingsChanged(); } } }

        
        private static EReflectionLevel projectReflectionMode;
        public static EReflectionLevel ProjectReflectionMode { get { return projectReflectionMode; } set { if (projectReflectionMode != value) { projectReflectionMode = value; } } }

        
        private static bool bugReport;
        public static bool BugReport { get { return bugReport; } set { if (bugReport != value) { bugReport = value; OnSettingsChanged(); } } }

        private static bool analytics;
        public static bool Analytics { get { return analytics; } set { if (analytics != value) { analytics = value; OnSettingsChanged(); } } }

        
        private static EBugReportMode bugReportMode;
        public static EBugReportMode BugReportMode { get { return bugReportMode; } set { if (bugReportMode != value) { bugReportMode = value; } } }

        
        private static string lockScenesFilter;
        public static string LockScenesFilter { get { return lockScenesFilter; } set { if (lockScenesFilter != value) { lockScenesFilter = value.TrimStart(new[] { ' ', '/' }); } } }

        
        private static string lockPrefabsFilter;
        public static string LockPrefabsFilter { get { return lockPrefabsFilter; } set { if (lockPrefabsFilter != value) { lockPrefabsFilter = value.TrimStart(new[] { ' ', '/' }); } } }

        
        private static string lockMaterialsFilter;
        public static string LockMaterialsFilter { get { return lockMaterialsFilter; } set { if (lockMaterialsFilter != value) { lockMaterialsFilter = value.TrimStart(new[] { ' ', '/' }); } } }


        private static bool autoCloseAfterSuccess;
        public static bool AutoCloseAfterSuccess { get { return autoCloseAfterSuccess; } set { if (autoCloseAfterSuccess != value) { autoCloseAfterSuccess = value; OnSettingsChanged(); } } }


        private static bool includeDepedenciesAsDefault;
        public static bool IncludeDepedenciesAsDefault { get { return includeDepedenciesAsDefault; } set { if (includeDepedenciesAsDefault != value) { includeDepedenciesAsDefault = value; OnSettingsChanged(); } } }

        private static bool requireLockBeforeCommit;
        public static bool RequireLockBeforeCommit { get { return requireLockBeforeCommit; } set { if (requireLockBeforeCommit != value) { requireLockBeforeCommit = value; OnSettingsChanged(); } } }

        private static bool selectiveCommit;
        public static bool SelectiveCommit { get { return selectiveCommit; } set { if (selectiveCommit != value) { selectiveCommit = value; OnSettingsChanged(); } } }
        

        private static ESaveAssetsStrategy saveStrategy;
        public static ESaveAssetsStrategy SaveStrategy { get { return saveStrategy; } set { if (saveStrategy != value) { saveStrategy = value; OnSettingsChanged(); } } }
        
        


        private static string clientPath;
        public static string ClientPath
        {
            get { return clientPath; }
            set
            {
                if (clientPath != value)
                {
                    clientPath = value;
                    if (!string.IsNullOrEmpty(clientPath)) EnvironmentManager.AddPathEnvironment(clientPath, ":");
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
                    // D.writeErrorCallback is always shown from VCExceptionHandler
                    logging = value;
                    if (logging)
                    {
                        D.writeLogCallback += Debug.Log;
                        D.writeWarningCallback += Debug.LogWarning;
                    }
                    else
                    {
                        if (D.writeLogCallback != null) D.writeLogCallback -= Debug.Log;
                        if (D.writeWarningCallback != null) D.writeWarningCallback -= Debug.LogWarning;
                    }
                }
            }
        }
        private static bool logging;
    }
}


