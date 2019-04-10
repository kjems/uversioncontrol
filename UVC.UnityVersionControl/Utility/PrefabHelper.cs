// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UVC.Extensions;

namespace UVC
{
    public static class PrefabHelper
    {
        public static bool IsPrefabParent(Object go)
        {
            if (!go) return false;
            return go == PrefabUtility.GetCorrespondingObjectFromSource(go);
        }

        public static bool IsPrefabRoot(Object obj)
        {
            var gameObject = obj as GameObject;
            if (gameObject && PrefabUtility.GetPrefabAssetType(obj) != PrefabAssetType.NotAPrefab)
            {
                return PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject) == gameObject;
            }
            return false;
        }

        public static bool IsPrefab(Object obj, bool includeRegular = true, bool includeModels = true)
        {
            if (!obj) return false;
            var assetType = PrefabUtility.GetPrefabAssetType(obj);
            return
                (includeRegular && assetType == PrefabAssetType.Regular || assetType == PrefabAssetType.Variant || assetType == PrefabAssetType.MissingAsset) ||
                (includeModels && assetType == PrefabAssetType.Model);
        }

        public static bool IsPartofPrefabStage(GameObject gameObject)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() == null)
                return false;
            
            return PrefabStageUtility.GetCurrentPrefabStage().IsPartOfPrefabContents(gameObject);
        }

        public static bool IsAssetPathOpenAsPrefabStage(string assetpath)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() == null)
                return false;
            
            return PrefabStageUtility.GetCurrentPrefabStage().prefabAssetPath == assetpath;
        }

        public static void SaveOpenPrefabStage()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() == null)
                return;

            typeof(PrefabStage).GetMethod("SavePrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(PrefabStageUtility.GetCurrentPrefabStage(), null);
        }

        public static void ApplyPrefab(GameObject prefabInstance)
        {
            GameObject go = PrefabUtility.GetOutermostPrefabInstanceRoot(prefabInstance);
            var prefabParent = PrefabUtility.GetCorrespondingObjectFromSource(go);
            var prefabtype = PrefabUtility.GetPrefabAssetType(prefabParent);
            if (prefabParent && (prefabtype == PrefabAssetType.Regular || prefabtype == PrefabAssetType.Variant))
            {
                Undo.RecordObject(go, "Apply Prefab");
                PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabParent.GetAssetPath(), InteractionMode.AutomatedAction);
            }
        }
    }
}
