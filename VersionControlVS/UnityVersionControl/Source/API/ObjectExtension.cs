// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;
using VersionControl.UserInterface;


namespace VersionControl
{
    /*[InitializeOnLoad]
    internal static class GameObjectToAssetPathCache
    {
        static GameObjectToAssetPathCache()
        {
            VCCommands.Instance.StatusCompleted += () => gameObjectToAssetPath.Clear();
        }
        private static readonly Dictionary<Object, string> gameObjectToAssetPath = new Dictionary<Object, string>();

        public static bool TryGetValue(Object obj, out string assetPath)
        {
            return gameObjectToAssetPath.TryGetValue(obj, out assetPath);
        }

        public static void Add(Object obj, string assetPath)
        {
            if (!string.IsNullOrEmpty(assetPath)) gameObjectToAssetPath.Add(obj, assetPath);
        }
    }*/

    public static class ObjectUtilities
    {
        public static void SetSceneObjectToAssetPathCallback(System.Func<Object, string> sceneObjectToAssetPath)
        {
            ObjectUtilities.sceneObjectToAssetPath = sceneObjectToAssetPath;
        }
        public static string SceneObjectToAssetPath(Object obj)
        {
            return sceneObjectToAssetPath(obj);
        }
        private static System.Func<Object, string> sceneObjectToAssetPath = o => o.GetAssetPath();

        public static bool ChangesStoredInScene(Object obj)
        {
            return obj.GetAssetPath() == EditorApplication.currentScene;
        }
        public static bool ChangesStoredInPrefab(Object obj)
        {
            obj = AssetDatabase.LoadMainAssetAtPath(obj.GetAssetPath());
            return PrefabHelper.IsPrefabParent(obj) || PrefabHelper.IsPrefab(obj, true, false, true);
        }

        public static string ObjectToAssetPath(Object obj, bool includingPrefabs = true)
        {
            var redirectedAssetPath = SceneObjectToAssetPath(obj);
            if (!string.IsNullOrEmpty(redirectedAssetPath)) return redirectedAssetPath;
            if (includingPrefabs && PrefabHelper.IsPrefab(obj) && !PrefabHelper.IsPrefabParent(obj)) return AssetDatabase.GetAssetPath(PrefabHelper.GetPrefabParent(obj));
            return AssetDatabase.GetAssetOrScenePath(obj);
        }
    }

    internal static class ObjectExtension
    {
        public static VersionControlStatus GetAssetStatus(this Object obj)
        {
            return VCCommands.Instance.GetAssetStatus(GetAssetPath(obj));
        }

        public static IEnumerable<string> ToAssetPaths(this IEnumerable<Object> objects)
        {
            return objects.Select<UnityEngine.Object, string>(GetAssetPath).ToList();
        }

        public static IEnumerable<string> ToAssetPaths(this Object obj)
        {
            return new[] { obj.GetAssetPath() };
        }

        // The caching of AssetPaths caused too many problems with cache getting out of date.
        // The code is kept in if the performance is a problem at some point, but be aware of sublet errors due to failed cache
        public static string GetAssetPath(this Object obj)
        {
            return ObjectUtilities.ObjectToAssetPath(obj);
            /*
            if (obj == null) return "";
            string assetPath;
            if (!GameObjectToAssetPathCache.TryGetValue(obj, out assetPath))
            {
                assetPath = ObjectToAssetPath(obj);
                GameObjectToAssetPathCache.Add(obj, assetPath);                
            }
            return assetPath;
            */
        }
    }
}