using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace VersionControl
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
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

        internal static IEnumerable<string> LocalModified(IEnumerable<string> assets)
        {
            return assets                
                .Where(d => VCCommands.Instance.GetAssetStatus(d).ModifiedOrLocalEditAllowed())                
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
                                    .Where(a => File.Exists(a) && !a.Contains(VCCAddMetaFiles.metaStr) && !a.Contains("/.") && !a.Contains("\\.") && (File.GetAttributes(a) & FileAttributes.Hidden) == 0)
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
                .SelectMany(d => VCCommands.Instance.GetFilteredAssets(status => (status.fileStatus == VCFileStatus.Deleted || status.fileStatus == VCFileStatus.Missing) && status.assetPath.StartsWith(new ComposedString(d))))
                .Select(status => status.assetPath.Compose())
                .ToArray();
            return assetPaths.Concat(deletedInFolders).ToArray();
        }

        internal static IEnumerable<string> AddMoveMatches(IEnumerable<string> assetPaths)
        {
            List<string> moveMatches = new List<string>();
            var allDeleted = VCCommands.Instance.GetFilteredAssets(status => status.fileStatus == VCFileStatus.Deleted);
            var allAdded = VCCommands.Instance.GetFilteredAssets(status => status.fileStatus == VCFileStatus.Added);
            var commitDeleted = assetPaths.Where(a => VCCommands.Instance.GetAssetStatus(a).fileStatus == VCFileStatus.Deleted);
            var commitAdded = assetPaths.Where(a => VCCommands.Instance.GetAssetStatus(a).fileStatus == VCFileStatus.Added);
            foreach (var deleted in allDeleted)
            {
                var deletedPath = deleted.assetPath.Compose();
                if (commitAdded.Count(added => added.EndsWith(Path.GetFileName(deletedPath))) > 0)
                {
                    moveMatches.Add(deletedPath);
                }
                if (commitAdded.Count(added => added.StartsWith(Path.GetDirectoryName(deletedPath)) && Path.GetExtension(deletedPath) == Path.GetExtension(added)) > 0)
                {
                    moveMatches.Add(deletedPath);
                }

            }
            foreach (var added in allAdded)
            {
                var addedPath = added.assetPath.Compose();
                if (commitDeleted.Count(deleted => deleted.EndsWith(Path.GetFileName(addedPath))) > 0)
                {
                    moveMatches.Add(addedPath);
                }
                if (commitDeleted.Count(deleted => deleted.StartsWith(Path.GetDirectoryName(addedPath)) && Path.GetExtension(addedPath) == Path.GetExtension(deleted)) > 0)
                {
                    moveMatches.Add(addedPath);
                }
            }
            return assetPaths.Concat(moveMatches).ToArray();
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
