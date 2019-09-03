// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;

namespace UVC
{
    internal class RefreshOnNewAsset : AssetPostprocessor
    {
        private static List<string> changedAssets = new List<string>();
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (VCCommands.Active)
            {
                //DebugLog.Log("OnPostprocessAllAssets : imported: " + importedAssets.Length + ", deleted: " + deletedAssets.Length + ", moved: " + movedAssets.Length + ", movedFrom: " + movedAssets.Length);

                changedAssets.AddRange(importedAssets);
                changedAssets.AddRange(movedAssets);
                changedAssets.AddRange(deletedAssets);
                changedAssets.AddRange(movedFromAssetPaths);
                if (changedAssets.Count > 0)
                {
                    changedAssets = changedAssets.Distinct().ToList();
                    VCCommands.Instance.RemoveFromDatabase(changedAssets);
                    VCCommands.Instance.RequestStatus(changedAssets, StatusLevel.Previous);
                    changedAssets.Clear();
                }
            }
            GameObjectToAssetPathCache.ClearObjectToAssetPathCache();
        }
    }

    [InitializeOnLoad]
    internal static class GameObjectToAssetPathCache
    {
        private static readonly Dictionary<int, string> gameObjectToAssetPath = new Dictionary<int, string>();

        static GameObjectToAssetPathCache()
        {
            VCCommands.Instance.StatusCompleted += ClearObjectToAssetPathCache;
            EditorApplication.hierarchyChanged += ClearObjectToAssetPathCache;
        }

        public static void ClearObjectToAssetPathCache()
        {
            gameObjectToAssetPath.Clear();
        }

        public static bool TryGetValue(Object obj, out string assetPath)
        {
            return gameObjectToAssetPath.TryGetValue(obj.GetInstanceID(), out assetPath);
        }

        public static void Add(Object obj, string assetPath)
        {
            if (!string.IsNullOrEmpty(assetPath)) gameObjectToAssetPath.Add(obj.GetInstanceID(), assetPath);
        }
    }
}


