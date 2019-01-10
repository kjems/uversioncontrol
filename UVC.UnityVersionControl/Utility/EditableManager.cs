// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace UVC
{
    using Extensions;
    public static class EditableManager
    {
        public static bool IsEditable(Object obj)
        {
            return (obj.hideFlags & HideFlags.NotEditable) == 0;
        }

        public static void SetEditable(Object obj, bool editable)
        {
            //Debug.Log("Setting '" + obj + "' to " + (editable ? "editable" : "readonly"));
            if (AvoidGUILock(obj))
                editable = true;
            if (obj != null && !IsBuiltinAsset(obj.GetAssetPath()) && !EditorUtility.IsPersistent(obj) &&
                !(obj is GameObject && PrefabHelper.IsPrefabParent(obj))) // Do not modify object flags for Project-Prefab GameObjects
            {
                if (editable && !IsEditable(obj)) obj.hideFlags &= ~HideFlags.NotEditable;
                if (!editable && IsEditable(obj)) obj.hideFlags |= HideFlags.NotEditable;
            }
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

        public static bool IsBuiltinAsset(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath) || assetPath.EndsWith("unity_builtin_extra");
        }

        public static bool LockScene(string assetPath)
        {
            return VCSettings.LockScenes && assetPath.ToLowerInvariant().Contains(VCSettings.LockScenesFilter.ToLowerInvariant());
        }

        internal static void RefreshEditableObject(GameObject gameObject)
        {
            if (!EditorUtility.IsPersistent(gameObject))
            {
                bool editable = ShouleBeEditable(gameObject);
                SetEditable(gameObject, editable);
                foreach (var componentIt in gameObject.GetComponents<Component>())
                {
                    RefreshEditableComponent(gameObject, componentIt);
                }
            }
        }

        private static bool ShouleBeEditable(GameObject gameObject)
        {
            var assetPath = gameObject.GetAssetPath();
            if (assetPath == "") return true;
            var assetStatus = gameObject.GetAssetStatus();
            if (!VCUtility.ManagedByRepository(assetStatus)) return true;
            bool isPrefab = ObjectUtilities.ChangesStoredInPrefab(gameObject);
            if (isPrefab)
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
            var assetPath = material.GetAssetPath();
            var assetStatus = VCCommands.Instance.GetAssetStatus(assetPath);
            bool materialStoredInScene = VCUtility.MaterialStoredInScene(material);
            bool shouldLock = (materialStoredInScene ? gameObjectLocked : (VCUtility.ManagedByRepository(assetStatus) && !VCUtility.HaveAssetControl(assetStatus))) && VCSettings.LockAssets;
            SetEditable(material, !shouldLock);
        }
    }
}
