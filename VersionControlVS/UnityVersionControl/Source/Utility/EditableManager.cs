// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace VersionControl
{
    public static class EditableManager
    {
        public static bool IsEditable(Object obj)
        {
            return (obj.hideFlags & HideFlags.NotEditable) == 0;
        }

        private static void SetEditable(Object obj, bool editable)
        {
            if (obj == null) return;
            if (editable && !IsEditable(obj)) obj.hideFlags &= ~HideFlags.NotEditable;
            if (!editable && IsEditable(obj)) obj.hideFlags |= HideFlags.NotEditable;
        }

        public static void AddAvoidLockCondition(System.Func<Object, bool> condition)
        {
            avoidGUILockConditions.Add(condition);
        }

        private static readonly List<System.Func<Object, bool>> avoidGUILockConditions = new List<System.Func<Object, bool>>();
        private static bool AvoidGUILock(Object obj)
        {
            return avoidGUILockConditions.Any(c => c(obj));
        }

        private static bool LockPrefab(string assetPath)
        {
            return VCSettings.LockPrefabs && assetPath.ToLowerInvariant().Contains(VCSettings.LockPrefabsFilter.ToLowerInvariant());
        }

        private static bool LockScene(string assetPath)
        {
            return VCSettings.LockScenes && assetPath.ToLowerInvariant().Contains(VCSettings.LockScenesFilter.ToLowerInvariant());
        }

        private static bool LockMaterial(string assetPath)
        {
            return VCSettings.LockMaterials && assetPath.ToLowerInvariant().Contains(VCSettings.LockMaterialsFilter.ToLowerInvariant());
        }

        internal static void RefreshEditableMaterial(Material material)
        {
            if (AvoidGUILock(material)) return;
            SetEditable(material,  VCUtility.HaveAssetControl(material.GetAssetStatus()));
        }

        internal static void RefreshEditableObject(GameObject gameObject)
        {
            if (AvoidGUILock(gameObject)) return;

            bool editable = ShouleBeEditable(gameObject);
            SetEditable(gameObject, editable || PrefabHelper.IsPrefabRoot(gameObject));
            foreach (var componentIt in gameObject.GetComponents<Component>())
            {
                RefreshEditableComponent(gameObject, componentIt);
            }
        }

        private static bool ShouleBeEditable(GameObject gameObject)
        {
            var assetPath = gameObject.GetAssetPath();
            var assetStatus = gameObject.GetAssetStatus();
            bool lockScene = LockScene(assetPath);
            var vcSceneStatus = VCCommands.Instance.GetAssetStatus(EditorApplication.currentScene);
            bool hasAssetPath = assetPath != "";
            bool changesStoredInScene = ObjectExtension.ChangesStoredInScene(gameObject);
            bool haveSceneControl = VCUtility.HaveAssetControl(vcSceneStatus) || !lockScene;
            bool havePrefabControl =  haveSceneControl && PrefabHelper.IsPrefab(gameObject, true, false, true) && (VCUtility.HaveAssetControl(assetStatus) || !LockPrefab(assetPath));
            bool editable = VCUtility.HaveAssetControl(assetStatus) || !VCUtility.ManagedByRepository(assetStatus) || !hasAssetPath || (changesStoredInScene && !lockScene) || havePrefabControl;
            return editable;
        }

        private static void RefreshEditableComponent(GameObject gameObject, Component component)
        {
            bool editable = ShouleBeEditable(gameObject);
            SetEditable(component, editable);
            var renderer = component as Renderer;
            if (renderer)
            {
                foreach (var materialIt in renderer.sharedMaterials)
                {
                    SetMaterialLock(materialIt, !editable);
                }
            }
        }

        private static void SetMaterialLock(Material material, bool gameObjectLocked)
        {
            var assetPath = AssetDatabase.GetAssetPath(material);
            var assetStatus = VCCommands.Instance.GetAssetStatus(assetPath);
            bool materialStoredInScene = VCUtility.MaterialStoredInScene(material);
            bool shouldLock = (materialStoredInScene ? gameObjectLocked : (VCUtility.ManagedByRepository(assetStatus) && !VCUtility.HaveAssetControl(assetStatus))) && LockMaterial(assetPath);
            SetEditable(material, !shouldLock);
        }
    }
}
