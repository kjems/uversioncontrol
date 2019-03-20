// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;
using UnityEditor.Experimental.SceneManagement;

namespace UVC
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
            return obj.GetAssetPath() == SceneManagerUtilities.GetCurrentScenePath();
        }
        public static bool ChangesStoredInPrefab(Object obj)
        {
            obj = GetObjectIndirection(obj);
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                GameObject gameObject = null;
                if (obj is Component component) gameObject = component.gameObject;
                if (obj is GameObject go) gameObject = go;

                return gameObject != null && prefabStage.IsPartOfPrefabContents(gameObject);
            }
            return false;
        }

        public static string ObjectToAssetPath(Object obj, bool includingPrefabs = true)
        {
            obj = GetObjectIndirection(obj);
            if (includingPrefabs && ChangesStoredInPrefab(obj)) return PrefabStageUtility.GetCurrentPrefabStage().prefabAssetPath;
            return AssetDatabase.GetAssetOrScenePath(obj);
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

            public static string GetAssetPath(this Object obj)
            {
                if (obj == null) return "";
                if (!GameObjectToAssetPathCache.TryGetValue(obj, out var assetPath))
                {
                    assetPath = ObjectUtilities.ObjectToAssetPath(obj);
                    GameObjectToAssetPathCache.Add(obj, assetPath);
                }
                return assetPath;

            }
        }
    }
}
