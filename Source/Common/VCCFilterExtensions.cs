using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VersionControl.AssetFilters
{
    public static class AssetFilterExtensions
    {
        public static IEnumerable<string> Unversioned(this IVersionControlCommands vcc, IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned || vcc.InUnversionedParentFolder(a));
        }
        public static IEnumerable<string> InVersionedFolder(this IVersionControlCommands vcc, IEnumerable<string> assets)
        {
            return assets.Where(a => !vcc.InUnversionedParentFolder(a)).ToArray();
        }
        public static IEnumerable<string> UnversionedInVersionedFolder(this IVersionControlCommands vcc, IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned && !vcc.InUnversionedParentFolder(a));
        }
        public static IEnumerable<string> Versioned(this IVersionControlCommands vcc, IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus != VCFileStatus.Unversioned && !vcc.InUnversionedParentFolder(a));
        }
        public static IEnumerable<string> OnChangeList(this IVersionControlCommands vcc, IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).changelist != null);
        }
        public static IEnumerable<string> Missing(this IVersionControlCommands vcc, IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Missing);
        }
        public static IEnumerable<string> Normal(this IVersionControlCommands vcc, IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Normal);
        }
        public static IEnumerable<string> Modified(this IVersionControlCommands vcc, IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Modified);
        }
        public static IEnumerable<string> Deleted(this IVersionControlCommands vcc, IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Deleted);
        }
        public static IEnumerable<string> Conflicted(this IVersionControlCommands vcc, IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Conflicted);
        }
        public static IEnumerable<string> Locked(this IVersionControlCommands vcc, IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).lockStatus == VCLockStatus.LockedHere);
        }
        public static IEnumerable<string> NotLocked(this IVersionControlCommands vcc, IEnumerable<string> assets)
        {
            return
                assets.Where(a => vcc.GetAssetStatus(a).fileStatus != VCFileStatus.Unversioned &&
                vcc.GetAssetStatus(a).lockStatus == VCLockStatus.NoLock);
        }
        public static IEnumerable<string> AddFolders(this IVersionControlCommands vcc, IEnumerable<string> assets)
        {
            return assets
                .Select(a => Path.GetDirectoryName(a))
                .Where(d => vcc.GetAssetStatus(d).fileStatus != VCFileStatus.Normal)
                .Concat(assets)
                .Distinct()
                .ToArray();
        }
        public static IEnumerable<string> AddedOrUnversionedParentFolders(this IVersionControlCommands vcc, IEnumerable<string> assets)
        {
            return assets.Concat(assets.SelectMany(ParentFolders).Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned || vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Added)).Distinct().ToArray();
        }
        public static bool InUnversionedParentFolder(this IVersionControlCommands vcc, string asset)
        {
            return ParentFolders(asset).Any(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned);
        }
        public static bool InIgnoredParentFolder(this IVersionControlCommands vcc, string asset)
        {
            return ParentFolders(asset).Any(a => 
            {
                return vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Ignored; 
            });
        }
        public static IEnumerable<string> ParentFolders(string asset)
        {
            const char pathSeparator = '/';
            var parentFolders = new List<string>();
            if (!string.IsNullOrEmpty(asset))
            {
                string currentFolder = "";
                foreach (var folderIt in Path.GetDirectoryName(asset).Split(pathSeparator))
                {
                    currentFolder += folderIt + pathSeparator;
                    parentFolders.Add(currentFolder.TrimEnd(pathSeparator));
                }
            }
            return parentFolders;
        }
        public static IEnumerable<string> AddFilesInFolders(this IVersionControlCommands vcc, IEnumerable<string> assets, bool versionedFoldersOnly = false)
        {
            foreach (var assetIt in new List<string>(assets))
            {
                if (Directory.Exists(assetIt) && (!versionedFoldersOnly || vcc.GetAssetStatus(assetIt).fileStatus != VCFileStatus.Unversioned))
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
        public static IEnumerable<string> RemoveFolders(IEnumerable<string> assets)
        {
            return assets.Where(a => !Directory.Exists(a)).ToArray();
        }
        public static IEnumerable<string> RemoveFilesUnderUnversionedFolders(this IVersionControlCommands vcc, IEnumerable<string> assets)
        {
            var folders = assets.Where(a => Directory.Exists(a) && vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned);
            assets = assets.Where(a => !folders.Any(f => a.StartsWith(f) && a != f));
            return assets.ToArray();
        }
    }
}