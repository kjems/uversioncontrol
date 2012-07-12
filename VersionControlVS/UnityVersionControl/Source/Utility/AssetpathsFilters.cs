using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace VersionControl
{
    internal class AssetpathsFilters
    {
        internal static IEnumerable<string> AddMeta(IEnumerable<string> assets, bool includeNormal = false)
        {
            if (!assets.Any()) return assets;
            var metaFiles = new List<string>();
            foreach (var assetPathIt in assets)
            {
                if (!assetPathIt.EndsWith(".meta"))
                {
                    var metaAssetPath = assetPathIt + ".meta";
                    var metaStatus = VCCommands.Instance.GetAssetStatus(assetPathIt).MetaStatus();
                    if (includeNormal || metaStatus.fileStatus != VCFileStatus.Normal)
                    {
                        metaFiles.Add(metaAssetPath);
                    }
                }
            }
            return assets.Concat(metaFiles).Distinct().OrderByDescending(a => a.EndsWith(".meta"));
        }

        internal static IEnumerable<string> RemoveMetaPostFix(IEnumerable<string> assets)
        {
            return assets.Select(a => a.EndsWith(".meta") ? a.Remove(a.Length - 5) : a).Distinct();
        }

        internal static IEnumerable<string> AddFolders(IEnumerable<string> assets)
        {
            return assets
                .Select(a => Path.GetDirectoryName(a))
                .Where(d => VCCommands.Instance.GetAssetStatus(d).fileStatus != VCFileStatus.Normal)
                .Concat(assets)
                .Distinct();
        }

        internal static IEnumerable<string> AddFilesInFolders(IEnumerable<string> assets)
        {
            foreach (var assetIt in new List<string>(assets))
            {
                if (Directory.Exists(assetIt))
                {
                    assets = assets
                        .Concat(Directory.GetFiles(assetIt, "*", SearchOption.AllDirectories)
                                    .Where(a => File.Exists(a) && !a.Contains(".meta") && !a.Contains("/.") && !a.Contains("\\.") && (File.GetAttributes(a) & FileAttributes.Hidden) == 0)
                                    .Select(s => s.Replace("\\", "/")));
                }
            }
            return assets;
        }

        internal static IEnumerable<string> AddDeletedInFolders(IEnumerable<string> assetPaths)
        {
            var deletedInFolders = assetPaths
                .Where(Directory.Exists)
                .SelectMany(d => VCCommands.Instance.GetFilteredAssets((assetPath, status) =>
                                                                       (status.fileStatus == VCFileStatus.Deleted || status.fileStatus == VCFileStatus.Missing) && assetPath.StartsWith(d)));
            return assetPaths.Concat(deletedInFolders);
        }

        internal static IEnumerable<string> GetDependencies(IEnumerable<string> assetPaths)
        {
            return AssetDatabase.GetDependencies(assetPaths.ToArray())
                .Where(dep => VCCommands.Instance.GetAssetStatus(dep).fileStatus != VCFileStatus.Normal)
                .Except(assetPaths.Select(ap => ap.ToLowerInvariant()));
        }
    }
}
