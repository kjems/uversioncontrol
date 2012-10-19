// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace VersionControl
{
    using Extensions;
    public static class EditableManager
    {
        private static bool IsEditable(Object obj)
        {
            return (obj.hideFlags & HideFlags.NotEditable) == 0;
        }

        internal static void SetEditable(Object obj, bool editable)
        {
            //D.Log("Setting '" + obj + "' to " + (editable ? "editable" : "readonly"));
            if (obj == null || AvoidGUILock(obj)) return;
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

        public static bool LockPrefab(string assetPath)
        {
            return VCSettings.LockPrefabs && assetPath.ToLowerInvariant().Contains(VCSettings.LockPrefabsFilter.ToLowerInvariant());
        }

        public static bool LockScene(string assetPath)
        {
            return VCSettings.LockScenes && assetPath.ToLowerInvariant().Contains(VCSettings.LockScenesFilter.ToLowerInvariant());
        }

        public static bool LockMaterial(string assetPath)
        {
            return VCSettings.LockMaterials && assetPath.ToLowerInvariant().Contains(VCSettings.LockMaterialsFilter.ToLowerInvariant());
        }

        internal static void RefreshEditableMaterial(Material material)
        {
            SetEditable(material, VCUtility.HaveAssetControl(material.GetAssetStatus()));
        }

        internal static void RefreshEditableObject(GameObject gameObject)
        {
            bool editable = ShouleBeEditable(gameObject);
            bool parentEditable = gameObject.transform.parent ? ShouleBeEditable(gameObject.transform.parent.gameObject) : VCUtility.HaveAssetControl(EditorApplication.currentScene);
            SetEditable(gameObject, editable || (PrefabHelper.IsPrefabRoot(gameObject) && parentEditable));
            foreach (var componentIt in gameObject.GetComponents<Component>())
            {
                RefreshEditableComponent(gameObject, componentIt);
            }
        }

        private static bool ShouleBeEditable(GameObject gameObject)
        {
            var assetPath = gameObject.GetAssetPath();
            if (assetPath == "") return true;
            var assetStatus = gameObject.GetAssetStatus();
            if(!VCUtility.ManagedByRepository(assetStatus)) return true;
            bool isPrefab = ObjectUtilities.ChangesStoredInPrefab(gameObject);
            if (isPrefab && LockPrefab(assetPath))
            {
                return VCUtility.HaveAssetControl(assetStatus);
            }
            else // Treat as scene object
            {
                string scenePath = ObjectUtilities.ObjectToAssetPath(gameObject, false);
                if (scenePath == "") return true;
                var vcSceneStatus = VCCommands.Instance.GetAssetStatus(scenePath);
                bool haveSceneControl = VCUtility.HaveAssetControl(vcSceneStatus);
                bool lockScene = LockScene(scenePath);
                return haveSceneControl || !lockScene;
            }
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
