// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

// This script includes common SVN related operations

using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace UVC
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    using Extensions;
    public static class VCUtility
    {
        public static System.Action<Object> onHierarchyReverted;
        public static System.Action<Object> onHierarchyCommit;
        public static System.Action<Object> onHierarchyGetLock;
        public static System.Func<Object, bool> onHierarchyAllowGetLock;
        public static System.Action<Object> onHierarchyAllowLocalEdit;

        public static string GetCurrentVersion()
        {
            return System.Diagnostics.FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
        }

        public static Object Revert(Object obj)
        {
            var gameObject = obj as GameObject;
            if (gameObject && PrefabHelper.IsPrefab(gameObject, true, false, true) && !PrefabHelper.IsPrefabParent(gameObject))
            {
                return RevertPrefab(gameObject);
            }
            return RevertObject(obj);
        }

        private static Object RevertObject(Object obj)
        {
            if (ObjectUtilities.ChangesStoredInScene(obj)) SceneManagerUtilities.SaveActiveScene();
            bool success = VCCommands.Instance.Revert(obj.ToAssetPaths());
            if (success && onHierarchyReverted != null) onHierarchyReverted(obj);
            return obj;
        }

        private static GameObject RevertPrefab(GameObject gameObject)
        {
            PrefabHelper.ReconnectToLastPrefab(gameObject);
            PrefabUtility.RevertPrefabInstance(gameObject);

            if (ShouldVCRevert(gameObject))
            {
                bool success = VCCommands.Instance.Revert(gameObject.ToAssetPaths());
                if (success && onHierarchyReverted != null) onHierarchyReverted(gameObject);
            }

            return gameObject;
        }

        public static bool ShouldVCRevert(Object obj)
        {
            var assetStatus = obj.GetAssetStatus();
            var material = obj as Material;
            return
                material && ManagedByRepository(assetStatus) ||
                ((assetStatus.lockStatus == VCLockStatus.LockedHere || assetStatus.ModifiedOrLocalEditAllowed()) && VCCommands.Instance.Ready) &&
                PrefabHelper.IsPrefab(obj, true, false, true);
        }

        public static void ApplyAndCommit(Object obj, string commitMessage = "", bool showCommitDialog = false)
        {
            var gameObject = obj as GameObject;
            if (ObjectUtilities.ChangesStoredInScene(obj)) SceneManagerUtilities.SaveActiveScene();
            if (PrefabHelper.IsPrefab(gameObject, true, false) && !PrefabHelper.IsPrefabParent(obj)) PrefabHelper.ApplyPrefab(gameObject);
            if (onHierarchyCommit != null) onHierarchyCommit(obj);
            VCCommands.Instance.CommitDialog(obj.ToAssetPaths(), showCommitDialog, commitMessage);
        }

        public static bool GetLock(Object obj, OperationMode operationMode = OperationMode.Normal)
        {
            bool shouldGetLock = true;
            if (onHierarchyAllowGetLock != null) shouldGetLock = onHierarchyAllowGetLock(obj);
            if (shouldGetLock)
            {
                bool success = GetLock(obj.GetAssetPath(), operationMode);
                if (success && onHierarchyGetLock != null) onHierarchyGetLock(obj);
                return success;
            }
            return false;
        }
        public static bool GetLock(string assetpath, OperationMode operationMode = OperationMode.Normal)
        {
            var status = VCCommands.Instance.GetAssetStatus(assetpath);
            if (operationMode == OperationMode.Normal || EditorUtility.DisplayDialog("Force " + Terminology.getlock, "Are you sure you will steal the file from: [" + status.owner + "]", "Yes", "Cancel"))
            {
                return VCCommands.Instance.GetLock(new[] { assetpath }, operationMode);
            }
            return false;
        }

        public static void AllowLocalEdit(Object obj)
        {
            VCCommands.Instance.AllowLocalEdit(obj.ToAssetPaths());
            if (onHierarchyAllowLocalEdit != null) onHierarchyAllowLocalEdit(obj);
        }

        public static void RequestStatus(string assetPath, VCSettings.EReflectionLevel reflectionLevel)
        {
            if (VCSettings.VCEnabled)
            {
                VersionControlStatus assetStatus = VCCommands.Instance.GetAssetStatus(assetPath);
                if (assetStatus.assetPath != null)
                {
                    if (reflectionLevel == VCSettings.EReflectionLevel.Remote && assetStatus.reflectionLevel != VCReflectionLevel.Pending && assetStatus.reflectionLevel != VCReflectionLevel.Repository)
                    {
                        VCCommands.Instance.RequestStatus(assetStatus.assetPath.Compose(), StatusLevel.Remote);
                    }
                    else if (reflectionLevel == VCSettings.EReflectionLevel.Local && assetStatus.reflectionLevel == VCReflectionLevel.None)
                    {
                        VCCommands.Instance.RequestStatus(assetStatus.assetPath.Compose(), StatusLevel.Previous);
                    }
                }
            }
        }

        public static bool VCDialog(string command, Object obj)
        {
            return VCDialog(command, obj.ToAssetPaths());
        }

        public static bool VCDialog(string command, IEnumerable<string> assetPaths)
        {
            if (!assetPaths.Any()) return false;
            return EditorUtility.DisplayDialog(command + " following assest in Version Control?", "\n" + assetPaths.Aggregate((a, b) => a + "\n" + b), "Yes", "No");
        }

        public static void VCDeleteWithConfirmation(IEnumerable<string> assetPaths, bool showConfirmation = true)
        {
            if (!showConfirmation || VCDialog(Terminology.delete, assetPaths))
            {
                VCCommands.Instance.Delete(assetPaths);
            }
        }

        public static bool UserSelectedVersionControlSystem()
        {
            if (VCSettings.VersionControlBackend == VCSettings.EVersionControlBackend.None)
            {
                bool response = EditorUtility.DisplayDialog("Version Control Selection", "Select which Version Control System you are using", "SVN", "None");
                if (response) // SVN
                {
                    VCSettings.VersionControlBackend = VCSettings.EVersionControlBackend.Svn;
                }
                /*P4_DISABLED 
                int response = EditorUtility.DisplayDialogComplex("Version Control Selection", "Select which Version Control System you are using", "SVN", "P4 Beta", "None");
                if (response == 0) // SVN
                {
                    VCSettings.VersionControlBackend = VCSettings.EVersionControlBackend.Svn;
                }
                else if (response == 1) // Perforce
                {
                    VCSettings.VersionControlBackend = VCSettings.EVersionControlBackend.P4_Beta;
                }*/
            }
            return VCSettings.VersionControlBackend != VCSettings.EVersionControlBackend.None;
        }

        public static void VCDeleteWithConfirmation(Object obj, bool showConfirmation = true)
        {
            VCDeleteWithConfirmation(obj.ToAssetPaths(), showConfirmation);
        }

        public static string GetObjectTypeName(Object obj)
        {
            string objectType = "Unknown Type";
            if (PrefabHelper.IsPrefab(obj, false, true, true)) objectType = PrefabHelper.IsPrefabParent(obj) ? "Model" : "Model in Scene";
            if (PrefabHelper.IsPrefab(obj, true, false, true)) objectType = "Prefab";
            if (!PrefabHelper.IsPrefab(obj, true, true, true)) objectType = "Scene";

            if (PrefabHelper.IsPrefab(obj, true, false, true))
            {
                if (PrefabHelper.IsPrefabParent(obj)) objectType += " Asset";
                else if (PrefabHelper.IsPrefabRoot(obj)) objectType += " Root";
                else objectType += " Child";
            }

            return objectType;
        }

        static string binary2TextPath = null;
        static string GetBinaryConverterPath()
        {
            if (binary2TextPath == null)
                binary2TextPath = EditorApplication.applicationPath.Replace("Unity.exe", "") + (Application.platform == RuntimePlatform.WindowsEditor ? "Data/Tools/binary2text.exe" : "/Contents/Tools/binary2text");
            return binary2TextPath;
        }

        const string tempDirectory = "Temp/UVC/";
        public static void DiffWithBase(string assetPath)
        {
            if (!string.IsNullOrEmpty(assetPath))
            {
                string baseAssetPath = VCCommands.Instance.GetBasePath(assetPath);
                if (!string.IsNullOrEmpty(baseAssetPath))
                {
                    if (EditorSettings.serializationMode == SerializationMode.ForceBinary && requiresTextConversionPostfix.Any(new ComposedString(assetPath).EndsWith))
                    {
                        if (!Directory.Exists(tempDirectory))
                            Directory.CreateDirectory(tempDirectory);
                        string convertedBaseFile = tempDirectory + Path.GetFileName(assetPath) + ".svn-base";
                        string convertedWorkingCopyFile = tempDirectory + Path.GetFileName(assetPath) + ".svn-wc";
                        var baseConvertCommand = new CommandLineExecution.CommandLine(GetBinaryConverterPath(), baseAssetPath + " "  + convertedBaseFile, ".").Execute();
                        var workingCopyConvertCommand = new CommandLineExecution.CommandLine(GetBinaryConverterPath(), assetPath + " " + convertedWorkingCopyFile, ".").Execute();
                        EditorUtility.InvokeDiffTool("Working Base : " + convertedWorkingCopyFile, convertedBaseFile, "Working Copy : " + convertedWorkingCopyFile, convertedWorkingCopyFile, convertedWorkingCopyFile, convertedBaseFile);
                    }
                    else
                    {
                        EditorUtility.InvokeDiffTool("Working Base : " + assetPath, baseAssetPath, "Working Copy : " + assetPath, assetPath, assetPath, baseAssetPath);
                    }
                }
            }
        }

        static readonly ComposedString[] requiresTextConversionPostfix  = new ComposedString[] { ".unity", ".prefab", ".mat", ".asset" };
        static readonly ComposedString[] mergablePostfix                = new ComposedString[] { ".cs", ".js", ".boo", ".text", ".shader", ".txt", ".xml", ".json", ".asmdef", ".manifest", ".compute" };
        
        public static bool IsDiffableAsset(ComposedString assetPath)
        {
            return mergablePostfix.Any(assetPath.EndsWith) || requiresTextConversionPostfix.Any(assetPath.EndsWith);
        }

        public static bool IsMergableAsset(ComposedString assetPath)
        {
            return mergablePostfix.Any(assetPath.EndsWith);
        }

        public static bool HaveVCLock(VersionControlStatus assetStatus)
        {
            bool isManagedByRepository = ManagedByRepository(assetStatus);
            bool hasLocalLock = assetStatus.lockStatus == VCLockStatus.LockedHere;
            return isManagedByRepository && hasLocalLock;
        }

        public static bool MaterialStoredInScene(Material material)
        {
            return material && !EditorUtility.IsPersistent(material);
        }

        public static bool HaveAssetControl(VersionControlStatus assetStatus)
        {
            return (!VCCommands.Active ||
                    HaveVCLock(assetStatus) ||
                    assetStatus.fileStatus == VCFileStatus.Added ||
                    assetStatus.fileStatus == VCFileStatus.Unversioned ||
                    assetStatus.fileStatus == VCFileStatus.Ignored ||
                    ComposedString.IsNullOrEmpty(assetStatus.assetPath) ||
                    assetStatus.LocalEditAllowed());
        }

        public static bool HaveAssetControl(string assetPath)
        {
            return HaveAssetControl(VCCommands.Instance.GetAssetStatus(assetPath));
        }

        public static bool HaveAssetControl(Object obj)
        {
            return HaveAssetControl(obj.GetAssetPath());
        }

        public static bool ManagedByRepository(VersionControlStatus assetStatus)
        {
            return assetStatus.fileStatus != VCFileStatus.Unversioned && !ComposedString.IsNullOrEmpty(assetStatus.assetPath) && !Application.isPlaying;
        }

        public static bool ManagedByRepository(string assetPath)
        {
            return ManagedByRepository(VCCommands.Instance.GetAssetStatus(assetPath));
        }

        public static bool ManagedByRepository(Object obj)
        {
            return ManagedByRepository(obj.GetAssetPath());
        }

        public static bool ValidAssetPath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) && (File.Exists(assetPath) || Directory.Exists(assetPath) || VCCommands.Instance.GetAssetStatus(assetPath).fileStatus == VCFileStatus.Deleted);
        }
    }
}
