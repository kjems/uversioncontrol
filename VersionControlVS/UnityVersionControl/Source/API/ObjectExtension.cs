// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnitySVN@gmail.com>
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;


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

    internal static class ObjectExtension
    {
        public static bool ChangesStoredInScene(Object obj)
        {
            return ((obj is GameObject) && !PrefabHelper.IsPrefabParent(obj) && !PrefabHelper.IsPrefab(obj, true, false, true));
        }

        private static string ObjectToAssetPath(Object obj)
        {
            if (obj is Material) return AssetDatabase.GetAssetPath(obj);
            if (obj is TextAsset) return AssetDatabase.GetAssetPath(obj);
            if (ChangesStoredInScene(obj)) return EditorApplication.currentScene;
            if (PrefabHelper.IsPrefabParent(obj)) return AssetDatabase.GetAssetPath(obj);
            if (PrefabHelper.IsPrefab(obj)) return AssetDatabase.GetAssetPath(PrefabHelper.GetPrefabParent(obj));
            return AssetDatabase.GetAssetPath(obj);
        }

        

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
            return ObjectToAssetPath(obj);
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