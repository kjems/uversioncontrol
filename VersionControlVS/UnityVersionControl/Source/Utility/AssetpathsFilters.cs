using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace VersionControl
{
    internal class AssetpathsFilters
    {
        internal static IEnumerable<string> AddFolders(IEnumerable<string> assets)
        {
            return assets
                .Select(a => Path.GetDirectoryName(a))
                .Where(d => VCCommands.Instance.GetAssetStatus(d).fileStatus != VCFileStatus.Normal)
                .Concat(assets)
                .Distinct()
                .ToArray();
        }

        internal static IEnumerable<string> AddFilesInFolders(IEnumerable<string> assets)
        {
            foreach (var assetIt in new List<string>(assets))
            {
                if (Directory.Exists(assetIt))
                {
                    assets = assets
                        .Concat(Directory.GetFiles(assetIt, "*", SearchOption.AllDirectories)
                                    .Where(a => File.Exists(a) && !a.Contains(VCCAddMetaFiles.meta) && !a.Contains("/.") && !a.Contains("\\.") && (File.GetAttributes(a) & FileAttributes.Hidden) == 0)
                                    .Select(s => s.Replace("\\", "/")))
                        .ToArray();
                }
            }
            return assets;
        }

        internal static IEnumerable<string> AddDeletedInFolders(IEnumerable<string> assetPaths)
        {
            var deletedInFolders = assetPaths
                .Where(Directory.Exists)
                .SelectMany(d => VCCommands.Instance.GetFilteredAssets(status => (status.fileStatus == VCFileStatus.Deleted || status.fileStatus == VCFileStatus.Missing) && status.assetPath.StartsWith(d)))
                .Select(status => status.assetPath.ToString())
                .ToArray();
            return assetPaths.Concat(deletedInFolders).ToArray();
        }

        internal static IEnumerable<string> GetDependencies(IEnumerable<string> assetPaths)
        {
            return AssetDatabase.GetDependencies(assetPaths.ToArray())
                .Where(dep => VCCommands.Instance.GetAssetStatus(dep).fileStatus != VCFileStatus.Normal)
                .Except(assetPaths.Select(ap => ap.ToLowerInvariant()))
                .ToArray();
        }
    }
}
