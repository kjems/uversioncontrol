// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEngine;
using UnityEditor;

namespace VersionControl
{
    public static class PrefabHelper
    {
        #region UnityEditorProxys
        public static PrefabType GetPrefabType(Object obj) { return PrefabUtility.GetPrefabType(obj); }
        public static Object GetPrefabParent(Object obj) { return PrefabUtility.GetPrefabParent(obj); }
        public static GameObject FindPrefabRoot(GameObject go) { return PrefabUtility.FindPrefabRoot(go); }
        public static Object InstantiatePrefab(Object obj) { return PrefabUtility.InstantiatePrefab(obj); }
        public static bool ReconnectToLastPrefab(GameObject go) { return PrefabUtility.ReconnectToLastPrefab(go); }
        #endregion

        public static bool IsPrefabParent(Object go)
        {
            if (!go) return false;
            PrefabType pbtype = GetPrefabType(go);
            bool isPrefabParent =
                pbtype == PrefabType.ModelPrefab ||
                pbtype == PrefabType.Prefab;

            return isPrefabParent;
        }

        public static bool IsPrefabRoot(Object obj)
        {
            var gameObject = obj as GameObject;
            if (gameObject && IsPrefab(obj))
            {
                return FindPrefabRoot(gameObject) == gameObject;
            }
            return false;
        }

        public static bool IsPrefab(Object obj, bool includeRegular = true, bool includeModels = true, bool includeDisconnected = true)
        {
            if (!obj) return false;
            PrefabType pbtype = GetPrefabType(obj);
            bool isPrefab =
                (includeRegular && pbtype == PrefabType.Prefab) ||
                (includeRegular && pbtype == PrefabType.PrefabInstance) ||
                (includeRegular && includeDisconnected && pbtype == PrefabType.DisconnectedPrefabInstance) ||
                (includeModels && pbtype == PrefabType.ModelPrefab) ||
                (includeModels && pbtype == PrefabType.ModelPrefabInstance) ||
                (includeModels && includeDisconnected && pbtype == PrefabType.DisconnectedModelPrefabInstance);
            return isPrefab;
        }

        public static GameObject DisconnectPrefab(GameObject gameObject)
        {
            // instantiate prefab at prefab location, remove original prefab instance.
            var prefabRoot = FindPrefabRoot(gameObject);
            string prefabName = prefabRoot.name;

            var replacedPrefab = Object.Instantiate(prefabRoot, prefabRoot.transform.position, prefabRoot.transform.rotation) as GameObject;
            Undo.RegisterCreatedObjectUndo(replacedPrefab, "Disconnect Prefab");
            replacedPrefab.name = prefabName;
            replacedPrefab.transform.parent = prefabRoot.transform.parent;

            Undo.DestroyObjectImmediate(prefabRoot);
            return replacedPrefab;
        }

        public static void SelectPrefab(GameObject gameObject)
        {
            var prefabParent = GetPrefabParent(FindPrefabRoot(gameObject)) as GameObject;
            Selection.activeGameObject = prefabParent;
            EditorGUIUtility.PingObject(Selection.activeGameObject);
        }

        public static void ApplyPrefab(GameObject prefabInstance)
        {
            GameObject go = PrefabUtility.FindRootGameObjectWithSameParentPrefab(prefabInstance);
            var prefabParent = GetPrefabParent(prefabInstance) as GameObject;
            if (prefabParent && GetPrefabType(prefabParent) == PrefabType.Prefab)
            {
                Undo.RecordObject(go, "Apply Prefab");
                PrefabUtility.ReplacePrefab(go, prefabParent, ReplacePrefabOptions.ConnectToPrefab);
            }
        }
    }
}
