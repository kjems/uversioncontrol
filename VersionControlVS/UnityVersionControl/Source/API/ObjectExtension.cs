// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace VersionControl
{
    using Extensions;
    public static class ObjectUtilities
    {
        public static void SetObjectIndirectionCallback(System.Func<Object, Object> objectIndirectionCallback)
        {
            indirection = objectIndirectionCallback;
        }
        public static Object GetObjectIndirection(Object obj)
        {
            return indirection(obj);
        }
        private static System.Func<Object, Object> indirection = o => o;

        public static bool ChangesStoredInScene(Object obj)
        {
            obj = GetObjectIndirection(obj);
            return obj.GetAssetPath() == UriEscapedUnityAPI.GetCurrentScenePath();
        }
        public static bool ChangesStoredInPrefab(Object obj)
        {
            obj = GetObjectIndirection(obj);
            return PrefabHelper.IsPrefabParent(obj) || PrefabHelper.IsPrefab(obj, true, false, true);
        }

        public static string ObjectToAssetPath(Object obj, bool includingPrefabs = true)
        {
            obj = GetObjectIndirection(obj);
            if (includingPrefabs && PrefabHelper.IsPrefab(obj) && !PrefabHelper.IsPrefabParent(obj))
            {
                return UriEscapedUnityAPI.GetAssetPath(PrefabHelper.GetPrefabParent(obj));
            }
            return UriEscapedUnityAPI.GetAssetOrScenePath(obj);
        }
    }

    public static class UriEscapedUnityAPI
    {
        public static string GetCurrentScenePath()
        {
            return System.Uri.EscapeUriString(EditorApplication.currentScene);
        }
        public static string GetAssetOrScenePath(Object obj)
        {
            return System.Uri.EscapeUriString(AssetDatabase.GetAssetOrScenePath(obj));
        }
        public static string GetAssetPath(Object obj)
        {
            return System.Uri.EscapeUriString(AssetDatabase.GetAssetPath(obj));
        }

        public static string GUIDToAssetPath(string guid)
        {
            return System.Uri.EscapeUriString(AssetDatabase.GUIDToAssetPath(guid));
        }
    }

    namespace Extensions
    {
        public static class ObjectExtension
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
                if (obj == null) return "";
                string assetPath;
                if (!GameObjectToAssetPathCache.TryGetValue(obj, out assetPath))
                {
                    assetPath = ObjectUtilities.ObjectToAssetPath(obj);
                    GameObjectToAssetPathCache.Add(obj, assetPath);
                }
                return assetPath;
                
            }
        }
    }
}
