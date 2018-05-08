// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace VersionControl.AssetPathFilters
{
    public static class UnityAssetpathsFilters
    {
        public static IEnumerable<string> LocalModified(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            return assets
                .Where(d => vcc.GetAssetStatus(d).ModifiedOrLocalEditAllowed())
                .ToArray();
        }

        public static IEnumerable<string> AddFilesInFolders(this IEnumerable<string> assets)
        {
            foreach (var assetIt in new List<string>(assets))
            {
                if (Directory.Exists(assetIt))
                {
                    assets = assets
                        .Concat(Directory.GetFiles(assetIt, "*", SearchOption.AllDirectories)
                                    .Where(a => File.Exists(a) && !a.Contains(VCCAddMetaFiles.metaStr) && !a.Contains("/.") && !a.Contains("\\.") && (File.GetAttributes(a) & FileAttributes.Hidden) == 0)
                                    .Select(s => s.Replace("\\", "/")))
                        .ToArray();
                }
            }
            return assets;
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
