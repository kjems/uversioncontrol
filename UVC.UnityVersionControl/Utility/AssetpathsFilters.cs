// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace UVC.AssetPathFilters
{
    public static class UnityAssetpathsFilters
    {
        public static IEnumerable<string> LocalModified(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            return assets
                .Where(d => vcc.GetAssetStatus(d).ModifiedOrLocalEditAllowed())
                .ToArray();
        }
        
        public static IEnumerable<string> LocalOnly(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            return assets
                .Where(d => vcc.GetAssetStatus(d).localOnly)
                .ToArray();
        }
        
        public static void AddFilesInFolders(ref List<string> assets)
        {
            /*for (int i = assets.Count - 1; i >= 0; --i)
            {
                if (AssetDatabase.IsValidFolder(assets[i]))
                {
                    var filesInFolder = Directory.GetFiles(assets[i], "*", SearchOption.AllDirectories)
                    .Where(a => File.Exists(a) && !a.EndsWith(VCCAddMetaFiles.metaStr) && !a.Contains("/.") && !a.Contains("\\.") && (File.GetAttributes(a) & FileAttributes.Hidden) == 0)
                    .Select(s => s.Replace("\\", "/"));
                    
                    assets.AddRange(filesInFolder);
                }
            }*/
            var folders = assets.Where(AssetDatabase.IsValidFolder).ToArray();
            if (folders.Length > 0)
            {
                assets.AddRange(
                    AssetDatabase
                        .FindAssets("", folders)
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .Select(s => s.Replace("\\", "/"))
                        .Where(a => !a.EndsWith(VCCAddMetaFiles.metaStr))
                );
            }
        }

        public static IEnumerable<string> GetDependencies(this IEnumerable<string> assetPaths)
        {
            return AssetDatabase.GetDependencies(assetPaths.Where(a => !ignoreDependency.Contains(a)).ToArray())
                .Where(dep => VCCommands.Instance.GetAssetStatus(dep).fileStatus != VCFileStatus.Normal)
                .Except(assetPaths.Select(ap => ap.ToLowerInvariant()))
                .ToArray();
        }

        private static readonly List<string> ignoreDependency = new List<string>();
        public static void RemoveIgnoreDependencies(string assetPath)
        {
            ignoreDependency.Remove(assetPath);
        }
        public static void AddIgnoreDependencies(string assetPath)
        {
            if(!ignoreDependency.Contains(assetPath))
                ignoreDependency.Add(assetPath);
        }
    }
}
