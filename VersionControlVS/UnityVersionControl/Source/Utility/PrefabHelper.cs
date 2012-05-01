// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnitySVN@gmail.com>
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

internal static class PrefabHelper
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
        Undo.RegisterSceneUndo("Disconnect Prefab");
        var prefabRoot = FindPrefabRoot(gameObject);
        string prefabName = prefabRoot.name;

        var replacedPrefab = Object.Instantiate(prefabRoot, prefabRoot.transform.position, prefabRoot.transform.rotation) as GameObject;
        replacedPrefab.name = prefabName;
        replacedPrefab.transform.parent = prefabRoot.transform.parent;

        Object.DestroyImmediate(prefabRoot);
        EditorUtility.UnloadUnusedAssets();
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
        Undo.RegisterSceneUndo("Apply Prefab");
        GameObject go = PrefabUtility.FindRootGameObjectWithSameParentPrefab(prefabInstance);
        var prefabParent = GetPrefabParent(prefabInstance) as GameObject;
        if (prefabParent && GetPrefabType(prefabParent) == PrefabType.Prefab)
        {
            PrefabUtility.ReplacePrefab(go, prefabParent, ReplacePrefabOptions.ConnectToPrefab);
        }
    }

    /*// Prefab Apply that maintain nested prefabs in scenes
    public static void ApplyPrefab(GameObject prefabInstance)
    {
        Undo.RegisterSceneUndo("Apply Prefab");

        GameObject go = PrefabUtility.FindRootGameObjectWithSameParentPrefab(prefabInstance);
        var childs = go.GetComponentsInChildren<Transform>();
        var prefabChilds = from c in childs
                           where PrefabUtility.GetPrefabObject(c.gameObject) != null && PrefabUtility.FindRootGameObjectWithSameParentPrefab(c.gameObject) != go
                           select PrefabUtility.FindRootGameObjectWithSameParentPrefab(c.gameObject);

        var childList = new List<KeyValuePair<Transform, Transform>>();
        foreach (var c in prefabChilds.Distinct())
        {
            if (c.transform.parent)
            {
                childList.Add(new KeyValuePair<Transform, Transform>(c.transform, c.transform.parent));
                c.transform.parent = null;
            }
        }

        var prefabParent = GetPrefabParent(prefabInstance) as GameObject;
        if (prefabParent && GetPrefabType(prefabParent) == PrefabType.Prefab)
        {
            PrefabUtility.ReplacePrefab(go, prefabParent, ReplacePrefabOptions.ConnectToPrefab);
        }

        foreach (var pair in childList)
            pair.Key.transform.parent = pair.Value.transform;
    }*/
}
