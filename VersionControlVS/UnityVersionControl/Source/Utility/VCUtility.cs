// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

// This script includes common SVN related operations

using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace VersionControl
{
    using Extensions;
    public static class VCUtility
    {
        public static System.Action<Object> onHierarchyReverted;
        public static System.Action<Object> onHierarchyCommit;
        public static System.Action<Object> onHierarchyGetLock;
        public static System.Func<Object, bool> onHierarchyAllowGetLock;
        public static System.Action<Object> onHierarchyBypass;

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
            if (ObjectUtilities.ChangesStoredInScene(obj)) EditorApplication.SaveScene(EditorApplication.currentScene);
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
                ((assetStatus.lockStatus == VCLockStatus.LockedHere || assetStatus.bypassRevisionControl) && VCCommands.Instance.Ready) &&
                PrefabHelper.IsPrefab(obj, true, false, true);
        }

        public static void ApplyAndCommit(Object obj, string commitMessage = "", bool showCommitDialog = false)
        {
            var gameObject = obj as GameObject;
            if (ObjectUtilities.ChangesStoredInScene(obj)) EditorApplication.SaveScene();
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
                return VCCommands.Instance.GetLock(new[] {assetpath}, operationMode);
            }
            return false;
        }

        public static void BypassRevision(Object obj)
        {
            VCCommands.Instance.BypassRevision(obj.ToAssetPaths());
            if (onHierarchyBypass != null) onHierarchyBypass(obj);
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

        public static void DiffWithBase(string assetPath)
        {
            string baseAssetPath = VCCommands.Instance.GetBasePath(assetPath);
            EditorUtility.InvokeDiffTool("Working Base : " + assetPath, baseAssetPath, "Working Copy : " + assetPath, assetPath, assetPath, baseAssetPath);
        }

        public static bool IsTextAsset(string assetPath)
        {
            var textPostfix = new List<string> { ".cs", ".js", ".boo", ".text", ".shader", ".txt", ".xml" };
            if (EditorSettings.serializationMode == SerializationMode.ForceText) textPostfix.AddRange(new[] { ".unity", ".prefab", ".mat" });
            return textPostfix.Any(assetPath.EndsWith);
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
            return HaveVCLock(assetStatus) || assetStatus.bypassRevisionControl || !VCSettings.VCEnabled || assetStatus.fileStatus == VCFileStatus.Unversioned || Application.isPlaying;
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
            return assetStatus.fileStatus != VCFileStatus.Unversioned && !System.String.IsNullOrEmpty(assetStatus.assetPath) && !Application.isPlaying;
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
