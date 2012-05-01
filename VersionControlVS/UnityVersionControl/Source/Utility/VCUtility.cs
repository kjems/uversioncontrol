// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnitySVN@gmail.com>

// This script includes common SVN related operations
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace VersionControl
{
    public static class VCUtility
    {
        
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
            if (ObjectExtension.ChangesStoredInScene(obj)) EditorApplication.SaveScene(EditorApplication.currentScene);
            VCCommands.Instance.Revert(obj.ToAssetPaths());
            return obj;
        }

        private static GameObject RevertPrefab(GameObject gameObject)
        {
            PrefabHelper.ReconnectToLastPrefab(gameObject);
            PrefabUtility.RevertPrefabInstance(gameObject);

            var prefabRoot = PrefabHelper.FindPrefabRoot(gameObject);
            var prefabParent = PrefabHelper.GetPrefabParent(prefabRoot) as GameObject;
            
            if (ShouldVCRevert(prefabParent)) VCCommands.Instance.Revert(prefabParent.ToAssetPaths());

            return gameObject;
        }

        public static bool ShouldVCRevert(Object obj)
        {
            var assetpath = obj.GetAssetPath();
            var assetStatus = VCCommands.Instance.GetAssetStatus(assetpath);
            var material = obj as Material;
            return
                material && ManagedByRepository(assetStatus) ||
                ((assetStatus.lockStatus == VCLockStatus.LockedHere || assetStatus.bypassRevisionControl) && VCCommands.Instance.Ready) &&
                PrefabHelper.IsPrefab(obj, true, false, true);
        }

        public static void ApplyAndCommit(Object obj, string commitMessage, bool showCommitDialog = false)
        {
            var gameObject = obj as GameObject;
            if (PrefabHelper.IsPrefab(gameObject, true, false) && !PrefabHelper.IsPrefabParent(obj)) PrefabHelper.ApplyPrefab(gameObject);
            VCCommands.Instance.CommitDialog(obj.ToAssetPaths(), showCommitDialog, commitMessage);
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
                VCCommands.Instance.Delete(assetPaths, false);
            }
        }

        public static void VCDeleteWithConfirmation(Object obj, bool showConfirmation = true)
        {
            VCDeleteWithConfirmation(obj.ToAssetPaths(), showConfirmation);
        }

        public static void VCForceOpen(string assetPath, VersionControlStatus status)
        {
            if (EditorUtility.DisplayDialog("Force " + Terminology.getlock, "Are you sure you will steal the file from: [" + status.owner + "]", "Yes", "Cancel"))
            {
                VCCommands.Instance.GetLock(new[] { assetPath }, true);
            }
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

        public static void RefreshEditableObject(GameObject gameObject)
        {
            var assetpath = gameObject.GetAssetPath();
            var assetStatus = gameObject.GetAssetStatus();
            var vcSceneStatus = VCCommands.Instance.GetAssetStatus(EditorApplication.currentScene);

            bool hasAssetPath = assetpath != "";
            bool haveSceneControl = HaveAssetControl(vcSceneStatus) || !VCSettings.LockScenes;
            bool havePrefabControl = haveSceneControl && PrefabHelper.IsPrefab(gameObject, true, false, true) && (HaveAssetControl(assetStatus) || !VCSettings.LockPrefabs);
            bool prefabRootMovable = PrefabHelper.IsPrefabRoot(gameObject) && haveSceneControl;
            bool changesStoredInScene = ObjectExtension.ChangesStoredInScene(gameObject);

            bool editable = prefabRootMovable || HaveAssetControl(assetStatus) || !ManagedByRepository(assetStatus) || !hasAssetPath || (changesStoredInScene && !VCSettings.LockScenes) || havePrefabControl;

            SetEditable(gameObject, editable);
            foreach (var componentIt in gameObject.GetComponents<Component>())
            {
                RefreshEditableComponent(gameObject, componentIt, assetStatus);
            }
        }

        public static void RefreshEditableComponent(GameObject gameObject, Component component, VersionControlStatus assetStatus)
        {
            bool hasAssetPath = gameObject.GetAssetPath() != "";
            var vcSceneStatus = VCCommands.Instance.GetAssetStatus(EditorApplication.currentScene);
            bool changesStoredInScene = ObjectExtension.ChangesStoredInScene(gameObject);
            bool sceneObjectAndNoVCControl = !HaveAssetControl(assetStatus) && changesStoredInScene && !VCSettings.LockScenes;
            bool haveSceneControl = HaveAssetControl(vcSceneStatus) || !VCSettings.LockScenes;
            bool havePrefabControl = haveSceneControl && PrefabHelper.IsPrefab(gameObject, true, false, true) && (HaveAssetControl(assetStatus) || !VCSettings.LockPrefabs);
            bool shouldLock = !(HaveAssetControl(assetStatus) || sceneObjectAndNoVCControl || havePrefabControl) && ManagedByRepository(assetStatus) && hasAssetPath;

            SetEditable(component, !shouldLock);
            var renderer = component as Renderer;
            if (renderer)
            {
                foreach (var materialIt in renderer.sharedMaterials)
                {
                    SetMaterialLock(materialIt, shouldLock);
                }
            }
        }

        public static void DiffWithBase(string assetPath)
        {
            string baseAssetPath = VCCommands.Instance.GetBasePath(assetPath);
            EditorUtility.InvokeDiffTool("Working Base : " + assetPath, baseAssetPath, "Working Copy : " + assetPath, assetPath, assetPath, baseAssetPath);
        }

        public static bool IsTextAsset(string assetPath)
        {
            var textPostfix = new List<string> { ".cs", ".js", ".boo", ".text", ".shader", ".txt", ".xml" };
            if (EditorSettings.serializationMode == SerializationMode.ForceText) textPostfix.AddRange(new[] { ".unity", ".prefab" });
            return textPostfix.Any(assetPath.EndsWith);
        }

        public static bool MaterialStoredInScene(Material material)
        {
            return material && !EditorUtility.IsPersistent(material);
        }

        public static void SetMaterialLock(Material material, bool gameObjectLocked)
        {
            var assetpath = AssetDatabase.GetAssetPath(material);
            var assetStatus = VCCommands.Instance.GetAssetStatus(assetpath);
            bool materialStoredInScene = MaterialStoredInScene(material);
            bool shouldLock = (materialStoredInScene ? gameObjectLocked : (ManagedByRepository(assetStatus) && !HaveAssetControl(assetStatus))) && VCSettings.LockMaterials;
            SetEditable(material, !shouldLock);
        }

        public static bool HaveVCLock(VersionControlStatus assetStatus)
        {
            bool isManagedByRepository = ManagedByRepository(assetStatus);
            bool hasLocalLock = assetStatus.lockStatus == VCLockStatus.LockedHere;
            return isManagedByRepository && hasLocalLock;
        }

        public static bool HaveAssetControl(VersionControlStatus assetStatus)
        {
            return HaveVCLock(assetStatus) || assetStatus.bypassRevisionControl || !VCSettings.VCEnabled || assetStatus.fileStatus == VCFileStatus.Unversioned || Application.isPlaying;
        }

        public static bool ManagedByRepository(VersionControlStatus assetStatus)
        {
            return assetStatus.fileStatus != VCFileStatus.Unversioned && !System.String.IsNullOrEmpty(assetStatus.assetPath) && !Application.isPlaying;
        }

        public static bool IsEditable(Object obj)
        {
            return (obj.hideFlags & HideFlags.NotEditable) == 0;
        }

        public static void SetEditable(Object obj, bool editable)
        {
            if (obj == null) return;
            if (editable)
            {
                if (!IsEditable(obj))
                {
                    obj.hideFlags &= ~HideFlags.NotEditable;
                }
            }
            else
            {
                if (IsEditable(obj))
                {
                    obj.hideFlags |= HideFlags.NotEditable;
                }
            }
        }
    }
}
