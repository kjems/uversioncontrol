// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UVC.AssetPathFilters
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    public static partial class AssetpathsFilters
    {
        public static IEnumerable<string> Unversioned(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned || a.InUnversionedParentFolder(vcc));
        }
        public static IEnumerable<string> InVersionedFolder(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            return assets.Where(a => !a.InUnversionedParentFolder(vcc)).ToArray();
        }
        public static IEnumerable<string> UnversionedInVersionedFolder(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned && !a.InUnversionedParentFolder(vcc));
        }
        public static IEnumerable<string> Versioned(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus != VCFileStatus.Unversioned && !a.InUnversionedParentFolder(vcc));
        }
        public static IEnumerable<string> OnChangeList(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).changelist != null);
        }
        public static IEnumerable<string> Missing(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Missing);
        }
        public static IEnumerable<string> Normal(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Normal);
        }
        public static IEnumerable<string> Modified(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Modified);
        }
        public static IEnumerable<string> Deleted(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Deleted);
        }
        public static IEnumerable<string> Conflicted(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Conflicted);
        }
        public static IEnumerable<string> Locked(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).lockStatus == VCLockStatus.LockedHere);
        }
        public static IEnumerable<string> NotLocked(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            return
                assets.Where(a => vcc.GetAssetStatus(a).fileStatus != VCFileStatus.Unversioned &&
                vcc.GetAssetStatus(a).lockStatus == VCLockStatus.NoLock);
        }
        public static IEnumerable<string> AddFolders(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            return assets
                .Select(GetDirectoryNameForwardSlash)
                .Where(d => vcc.GetAssetStatus(d).fileStatus != VCFileStatus.Normal)
                .Concat(assets)
                .Distinct()
                .ToArray();
        }
        
        public static void AddFolders(ref List<string> assets, IVersionControlCommands vcc)
        {
            for (int i = assets.Count - 1; i >= 0; i--)
            {
                string directory = GetDirectoryNameForwardSlash(assets[i]);
                if (!assets.Contains(directory) && vcc.GetAssetStatus(directory).fileStatus != VCFileStatus.Normal)
                {
                    assets.Add(directory);
                }
            }
        }
        public static IEnumerable<string> AddedOrUnversionedParentFolders(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            return assets.Concat(assets.SelectMany(ParentFolders).Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned || vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Added)).Distinct().ToArray();
        }
        public static bool InUnversionedParentFolder(this string asset, IVersionControlCommands vcc)
        {
            return ParentFolders(asset).Any(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned);
        }
        public static bool InIgnoredParentFolder(this string asset, IVersionControlCommands vcc)
        {
            return ParentFolders(asset).Any(a => 
            {
                return vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Ignored; 
            });
        }
        public static IEnumerable<string> ParentFolders(this string asset)
        {
            const char pathSeparator = '/';
            var parentFolders = new List<string>();
            if (!string.IsNullOrEmpty(asset))
            {
                string currentFolder = "";
                foreach (var folderIt in GetDirectoryNameForwardSlash(asset).Split(pathSeparator))
                {
                    currentFolder += folderIt + pathSeparator;
                    parentFolders.Add(currentFolder.TrimEnd(pathSeparator));
                }
            }
            return parentFolders;
        }
        public static IEnumerable<string> AddFilesInFolders(this IEnumerable<string> assets, IVersionControlCommands vcc, bool versionedFoldersOnly = false)
        {
            foreach (var assetIt in new List<string>(assets))
            {
                var status = vcc.GetAssetStatus(assetIt);
                if (Directory.Exists(assetIt) && (!versionedFoldersOnly || status.fileStatus != VCFileStatus.Unversioned) && status.property != VCProperty.Modified)
                {
                    assets = assets
                        .Concat(Directory.GetFiles(assetIt, "*", SearchOption.AllDirectories)
                        .Where(a => File.Exists(a) && !a.Contains("/.") && !a.Contains("\\.") && (File.GetAttributes(a) & FileAttributes.Hidden) == 0)
                        .Select(s => s.Replace("\\", "/")))
                        .ToArray();
                }
            }
            return assets;
        }
        public static IEnumerable<string> RemoveFolders(this IEnumerable<string> assets)
        {
            return assets.Where(a => !Directory.Exists(a)).ToArray();
        }
        public static IEnumerable<string> RemoveFilesUnderUnversionedFolders(this IEnumerable<string> assets, IVersionControlCommands vcc)
        {
            var folders = assets.Where(a => Directory.Exists(a) && vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned);
            assets = assets.Where(a => !folders.Any(f => a.StartsWith(f) && a != f));
            return assets.ToArray();
        }

        public static void AddMoveMatches(ref List<string> assetPaths, IVersionControlCommands vcc)
        {
            List<string> moveMatches = new List<string>();
            //moveMatches.AddRange(assetPaths.Select(vcc.GetAssetStatus).Where(status => !ComposedString.IsNullOrEmpty(status.movedFrom)).Select(status => status.movedFrom.Compose()));
            //UnityEngine.Debug.Log(moveMatches.Count > 0 ? moveMatches.AggregateString() : "Empty move match");
            
            var allDeleted = vcc.GetFilteredAssets(status => status.fileStatus == VCFileStatus.Deleted);
            var allAdded = vcc.GetFilteredAssets(status => status.fileStatus == VCFileStatus.Added);
            var commitDeleted = assetPaths.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Deleted);
            var commitAdded = assetPaths.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Added);
            foreach (var deleted in allDeleted)
            {
                var deletedPath = deleted.assetPath.Compose();
                if (commitAdded.Count(added => added.EndsWith(Path.GetFileName(deletedPath))) > 0)
                {
                    moveMatches.Add(deletedPath);
                }
                if (commitAdded.Count(added => added.StartsWith(GetDirectoryNameForwardSlash(deletedPath)) && Path.GetExtension(deletedPath) == Path.GetExtension(added)) > 0)
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
                if (commitDeleted.Count(deleted => deleted.StartsWith(GetDirectoryNameForwardSlash(addedPath)) && Path.GetExtension(addedPath) == Path.GetExtension(deleted)) > 0)
                {
                    moveMatches.Add(addedPath);
                }
            }
            assetPaths.AddRange(moveMatches.Distinct());
        }

        public static string GetDirectoryNameForwardSlash(string path)
        {
            return Path.GetDirectoryName(path).Replace("\\", "/");
        }

        public static IEnumerable<string> AddDeletedInFolders(this IEnumerable<string> assetPaths, IVersionControlCommands vcc)
        {
            var deletedInFolders = assetPaths
                .SelectMany(d => vcc.GetFilteredAssets(status => (status.fileStatus == VCFileStatus.Deleted || status.fileStatus == VCFileStatus.Missing) && status.assetPath.StartsWith(new ComposedString(d))))
                .Select(status => status.assetPath.Compose())
                .ToArray();
            return assetPaths.Concat(deletedInFolders).ToArray();
        }

        public static IEnumerable<string> NonEmpty(this IEnumerable<string> assets)
        {
            return assets.Where(a => !string.IsNullOrEmpty(a)).ToArray();
        }

        public static IEnumerable<string> ShortestFirst(this IEnumerable<string> assets)
        {
            return assets.OrderBy(s => s.Length);
        }
        
        public static IEnumerable<string> LongestFirst(this IEnumerable<string> assets)
        {
            return assets.OrderByDescending(s => s.Length);
        }

        public static IEnumerable<string> FilesExist(this IEnumerable<string> assets)
        {
            return assets.Where(File.Exists).ToArray();
        }

        public static string AggregateString(this IEnumerable<string> assets)
        {
            return !assets.Any() ? "" : assets.Aggregate((a, b) => a + ", " + b);
        }
    }
}