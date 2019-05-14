// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace UVC
{
    using Logging;
    using UserInterface;
    [InitializeOnLoad]
    public class VCSettings
    {
        static VCSettings()
        {
            vcEnabled = EditorPrefs.GetBool("VCSSettings/vcEnabled", false);
            lockScenes = EditorPrefs.GetBool("VCSSettings/lockScenes", true);
            lockAssets = EditorPrefs.GetBool("VCSSettings/lockAssets", true);
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
            autoCloseAfterSuccess = EditorPrefs.GetBool("VCSSettings/autoCloseAfterSuccess", true);
            includeDepedenciesAsDefault = EditorPrefs.GetBool("VCSSettings/includeDepedenciesAsDefault", false);
            requireLockBeforeCommit = EditorPrefs.GetBool("VCSSettings/requireLockBeforeCommit", false);
            selectiveCommit = EditorPrefs.GetBool("VCSSettings/selectiveCommit", true);
            saveStrategy = (ESaveAssetsStrategy)EditorPrefs.GetInt("VCSSettings/saveStrategy", (int)ESaveAssetsStrategy.Unity);
            versionControlBackend = (EVersionControlBackend)EditorPrefs.GetInt("VCSSettings/versionControlBackend", (int)EVersionControlBackend.None);
            handleFileMove = (EHandleFileMove)EditorPrefs.GetInt("VCSSettings/handleFileMove", (int)EHandleFileMove.TeamLicense);
            mergeToolPath = EditorPrefs.GetString("VCSSettings/mergetoolpath", mergeTools[0].pathMerge);
            mergeToolArgs = EditorPrefs.GetString("VCSSettings/mergetoolargs", mergeTools[0].argumentsMerge);
            diffToolPath  = EditorPrefs.GetString("VCSSettings/difftoolpath", mergeTools[0].pathDiff);
            diffToolArgs  = EditorPrefs.GetString("VCSSettings/difftoolargs", mergeTools[0].argumentsDiff);
            mergeToolIndex  = EditorPrefs.GetInt("VCSSettings/mergeToolIndex", 0);

            OnSettingsChanged();

            AppDomain.CurrentDomain.DomainUnload += (o, d) =>
            {
                EditorPrefs.SetBool("VCSSettings/vcEnabled", vcEnabled);
                EditorPrefs.SetBool("VCSSettings/lockScenes", lockScenes);
                EditorPrefs.SetBool("VCSSettings/lockAssets", lockAssets);
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
                EditorPrefs.SetBool("VCSSettings/autoCloseAfterSuccess", autoCloseAfterSuccess);
                EditorPrefs.SetBool("VCSSettings/includeDepedenciesAsDefault", includeDepedenciesAsDefault);
                EditorPrefs.SetBool("VCSSettings/requireLockBeforeCommit", requireLockBeforeCommit);
                EditorPrefs.SetBool("VCSSettings/selectiveCommit", selectiveCommit);
                EditorPrefs.SetInt("VCSSettings/saveStrategy", (int)saveStrategy);
                EditorPrefs.SetInt("VCSSettings/versionControlBackend", (int)versionControlBackend);
                EditorPrefs.SetInt("VCSSettings/handleFileMove", (int)handleFileMove);
                EditorPrefs.SetString("VCSSettings/mergetoolpath", mergeToolPath);
                EditorPrefs.SetString("VCSSettings/mergetoolargs", mergeToolArgs);
                EditorPrefs.SetString("VCSSettings/difftoolpath", diffToolPath );
                EditorPrefs.SetString("VCSSettings/difftoolargs", diffToolArgs );
                EditorPrefs.SetInt("VCSSettings/mergeToolIndex", mergeToolIndex);
            };
        }

        public enum EBugReportMode { Automatic, Manual }
        public enum EReflectionLevel { Local, Remote }
        public enum EVersionControlBackend { None, Svn/*P4_DISABLED, P4_Beta*/ }
        public enum ESaveAssetsStrategy { Unity, VersionControl, User }
        public enum EHandleFileMove { None, TeamLicense }

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
                    DebugLog.combinedShorthandCallback += addToErrors;

                    if (VersionControlFactory.CreateVersionControlCommands(value))
                    {
                        versionControlBackend = value;
                        OnSettingsChanged();
                    }
                    else
                    {
                        var dialog = CustomDialogs.CreateMessageDialog("Version Control Selection failed", "Unable to initialize '" + value + "'\n\n" + errors, MessageType.Error);
                        dialog.AddButton("OK", () => dialog.Close());
                        dialog.ShowUtility();
                    }
                    DebugLog.combinedShorthandCallback -= addToErrors;

                }
            }
        }

        private static bool lockScenes;
        public static bool LockScenes { get { return lockScenes; } set { if (lockScenes != value) { lockScenes = value; OnSettingsChanged(); } } }


        private static bool lockAssets;
        public static bool LockAssets { get { return lockAssets; } set { if (lockAssets != value) { lockAssets = value; OnSettingsChanged(); } } }


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

        private static EHandleFileMove handleFileMove;
        public static EHandleFileMove HandleFileMove { get { return handleFileMove; } set { if (handleFileMove != value) { handleFileMove = value; OnSettingsChanged(); } } }

        private static string mergeToolPath;
        public static string MergetoolPath { get { return mergeToolPath; } set { if(mergeToolPath != value) { mergeToolPath = value; OnSettingsChanged(); } } }

        private static string mergeToolArgs;
        public static string MergetoolArgs { get { return mergeToolArgs; } set { if(mergeToolArgs != value) { mergeToolArgs = value; OnSettingsChanged(); } } }

        private static string diffToolPath;
        public static string DifftoolPath { get { return diffToolPath; } set { if(diffToolPath != value) { diffToolPath = value; OnSettingsChanged(); } } }

        private static string diffToolArgs;
        public static string DifftoolArgs { get { return diffToolArgs; } set { if(diffToolArgs != value) { diffToolArgs = value; OnSettingsChanged(); } } }

        private static int mergeToolIndex;
        public static int MergeToolIndex { get { return mergeToolIndex; } set { if(mergeToolIndex != value) { mergeToolIndex = value; OnSettingsChanged(); } } }


        public struct MergeTool
        {
            public string name;
            public string pathDiff;
            public string pathMerge;
            public string argumentsDiff;
            public string argumentsMerge;
        }

        public static List<MergeTool> mergeTools = new List<MergeTool>
        {
            #if UNITY_EDITOR_OSX
            new MergeTool
            {
                name = "Apple File Merge",
                pathDiff  = "/Applications/Xcode.app/Contents/Applications/FileMerge.app/Contents/MacOS/FileMerge",
                pathMerge = "/Applications/Xcode.app/Contents/Applications/FileMerge.app/Contents/MacOS/FileMerge",
                argumentsDiff  = "-left '[theirs]' -right '[yours]'",
                argumentsMerge = "-left '[theirs]' -right '[yours]' -ancestor '[base]' -merge '[merge]'"
            },
            new MergeTool
            {
                name = "P4Merge",
                pathDiff  = "/Applications/p4merge.app/Contents/MacOS/p4merge",
                pathMerge = "/Applications/p4merge.app/Contents/MacOS/p4merge",
                argumentsDiff  = "'[theirs]' '[yours]'",
                argumentsMerge = "'[base]' '[theirs]' '[yours]' '[merge]'"
            },
            new MergeTool
            {
                name = "Beyond Compare 4",
                pathDiff  = "/Applications/Beyond Compare.app/Contents/MacOS/bcomp",
                pathMerge = "/Applications/Beyond Compare.app/Contents/MacOS/bcomp",
                argumentsDiff  = "'[theirs]' '[yours]'",
                argumentsMerge = "'[theirs]' '[yours]' '[base]' '[merge]'"
            },
            new MergeTool
            {
                name = "Semantic Merge (P4 diff)",
                pathDiff  = "/Applications/p4merge.app/Contents/MacOS/p4merge",
                pathMerge = "/Applications/semanticmerge.app/Contents/MacOS/semanticmerge",
                argumentsDiff  = "'[theirs]' '[yours]'",
                argumentsMerge = "'[yours]' '[theirs]' '[base]' '[merge]' " +
                                 "--nolangwarn -emt=\"/Applications/p4merge.app/Contents/MacOS/p4merge '[base]' '[theirs]' '[yours]' '[merge]'\""
            }
            #endif
            #if UNITY_EDITOR_WIN
            new MergeTool
            {
                name = "Tortoise Merge",
                pathDiff  = "C:/Program Files/TortoiseSVN/bin/TortoiseMerge.exe",
                pathMerge = "C:/Program Files/TortoiseSVN/bin/TortoiseMerge.exe",
                argumentsDiff  = "\"[theirs]\" \"[yours]\"",
                argumentsMerge = "/base:\"[base]\" /theirs:\"[theirs]\" /mine:\"[yours]\" /merged:\"[merge]\""
            },
            new MergeTool
            {
                name = "P4Merge",
                pathDiff  = "C:/Program Files/Perforce/p4merge.exe",
                pathMerge = "C:/Program Files/Perforce/p4merge.exe",
                argumentsDiff  = "\"[theirs]\" \"[yours]\"",
                argumentsMerge = "\"[base]\" \"[theirs]\" \"[yours]\" \"[merge]\""
            },
            new MergeTool
            {
                name = "Beyond Compare 4",
                pathDiff  = "C:/Program Files/Beyond Compare 4/BComp.exe",
                pathMerge = "C:/Program Files/Beyond Compare 4/BComp.exe",
                argumentsDiff  = "\"[theirs]\" \"[yours]\"",
                argumentsMerge = "\"[theirs]\" \"[yours]\" \"[base]\" \"[merge]\""
            },
            new MergeTool
            {
                name = "Semantic Merge (P4 diff)",
                pathDiff  = "~/AppData/Local/semanticmerge/semanticmergetool.exe",
                pathMerge = "~/AppData/Local/semanticmerge/semanticmergetool.exe",
                argumentsDiff  = "-s=\"[theirs]\" -d=\"[yours]\"",
                argumentsMerge = "\"[yours]\" \"[theirs]\" \"[base]\" \"[merge]\" --nolangwarn"
            }
            #endif
	    #if UNITY_EDITOR_LINUX
	    new MergeTool
            {
                name = "Semantic Merge (P4 diff)",
                pathDiff  = "/usr/local/bin/p4merge",
                pathMerge = "/usr/local/bin/p4merge",
                argumentsDiff  = "\"[theirs]\" \"[yours]\"",
                argumentsMerge = "\"[base]\" \"[theirs]\" \"[yours]\" \"[merge]\""
            }
            #endif
        };

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
                        DebugLog.writeLogCallback += Debug.Log;
                        DebugLog.writeWarningCallback += Debug.LogWarning;
                    }
                    else
                    {
                        if (DebugLog.writeLogCallback != null) DebugLog.writeLogCallback -= Debug.Log;
                        if (DebugLog.writeWarningCallback != null) DebugLog.writeWarningCallback -= Debug.LogWarning;
                    }
                }
            }
        }
        private static bool logging;
    }
}


