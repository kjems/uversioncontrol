// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VersionControl
{
    /// <summary>
    /// Responsibility: Decorate an underlying IVersionControlCommands with filtering of assets to 
    /// remove redundant or invalid calls.
    /// Examples of things avoided : 
    /// * Revert an unversioned file 
    /// * Commit an unversioned file without adding it first
    /// * Unlock a file that is not locked
    /// </summary>
    [System.Serializable]
    public class VCCFilteredAssets : VCCDecorator
    {
        public VCCFilteredAssets(IVersionControlCommands vcc)
            : base(vcc)
        {
        }

        public override bool Status(StatusLevel statusLevel, DetailLevel detailLevel)
        {
            return base.Status(statusLevel, detailLevel);
        }

        public override VersionControlStatus GetAssetStatus(string assetPath)
        {
            if (InUnversionedParentFolder(assetPath)) return new VersionControlStatus() { assetPath = assetPath, fileStatus = VCFileStatus.Unversioned };
            return vcc.GetAssetStatus(assetPath);
        }

        public override bool Status(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            assets = NonPending(InVersionedFolder(NonEmpty(assets))).ToList();
            return assets.Any() ? base.Status(assets, statusLevel) : false;
        }

        public override bool RequestStatus(IEnumerable<string> assets)
        {
            if (assets == null) return true;
            assets = NonPending(InVersionedFolder(NonEmpty(assets))).ToList();
            return assets.Any() ? base.RequestStatus(assets) : true;
        }

        public override bool Update(IEnumerable<string> assets = null)
        {
            return base.Update((assets != null ? Versioned(assets) : null));
        }

        public override bool Commit(IEnumerable<string> assets, string commitMessage = "")
        {
            var filesInFolders = AddFilesInFolders(assets, true);
            var toBeCommited = filesInFolders.Where(a => vcc.GetAssetStatus(a).fileStatus != VCFileStatus.Normal || Directory.Exists(a));
            return
                base.Add(UnversionedInVersionedFolder(filesInFolders)) &&
                base.Delete(Missing(filesInFolders), OperationMode.Normal) &&
                base.Commit(ShortestFirst(toBeCommited), commitMessage) &&
                ReleaseLock(assets);
        }

        public override bool Add(IEnumerable<string> assets)
        {
            return base.Add(UnversionedInVersionedFolder(assets));
        }

        public override bool Revert(IEnumerable<string> assets)
        {
            return base.Revert(NonNormal(assets));
        }

        public override bool Delete(IEnumerable<string> assets, OperationMode mode)
        {
            return base.Delete(Versioned(assets), mode);
        }

        public override bool GetLock(IEnumerable<string> assets, OperationMode mode)
        {
            try
            {
                return base.GetLock(mode == OperationMode.Force ? Versioned(assets) : NotLocked(assets), mode);
            }
            catch(VCLockedByOther e)
            {
                D.Log("Locked by other, so requesting remote status on : " + assets.Aggregate((a,b) => a + ", " + b) + "\n" + e.Message);
                Status(assets, StatusLevel.Remote);
                return false;
            }
        }
        
        public override bool Resolve(IEnumerable<string> assets, ConflictResolution conflictResolution)
        {
            return base.Resolve(FilesExist(assets), conflictResolution);
        }

        public override bool ReleaseLock(IEnumerable<string> assets)
        {
            return base.ReleaseLock(Locked(assets));
        }

        public override bool ChangeListAdd(IEnumerable<string> assets, string changelist)
        {
            assets = Versioned(NonEmpty(assets));
            return assets.Any() ? base.ChangeListAdd(assets, changelist) : false;
        }

        public override bool ChangeListRemove(IEnumerable<string> assets)
        {
            assets = Versioned(OnChangeList(NonEmpty(assets)));
            return assets.Any() ? base.ChangeListRemove(Versioned(OnChangeList(assets))) : false;
        }

        public override bool Move(string from, string to)
        {
            if (vcc.GetAssetStatus(from).fileStatus == VCFileStatus.Unversioned) return false;
            if (InUnversionedParentFolder(to)) return false;
            return base.Move(from, to);
        }

        #region Filters

        IEnumerable<string> NonPending(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).reflectionLevel != VCReflectionLevel.Pending);
        }
        IEnumerable<string> NonEmpty(IEnumerable<string> assets)
        {
            return assets.Where(a => !string.IsNullOrEmpty(a));
        }
        IEnumerable<string> Unversioned(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned || InUnversionedParentFolder(a));
        }
        IEnumerable<string> InVersionedFolder(IEnumerable<string> assets)
        {
            return assets.Where(a => !InUnversionedParentFolder(a));
        }
        IEnumerable<string> UnversionedInVersionedFolder(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned && !InUnversionedParentFolder(a));
        }
        IEnumerable<string> Versioned(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus != VCFileStatus.Unversioned && !InUnversionedParentFolder(a));
        }
        IEnumerable<string> OnChangeList(IEnumerable<string> assets)
        {
            return assets.Where(a => !string.IsNullOrEmpty(vcc.GetAssetStatus(a).changelist));
        }
        IEnumerable<string> Missing(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Missing);
        }
        IEnumerable<string> NonNormal(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus != VCFileStatus.Normal);
        }
        IEnumerable<string> Normal(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Normal);
        }
        IEnumerable<string> Modified(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Modified);
        }
        IEnumerable<string> Deleted(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Deleted);
        }
        IEnumerable<string> Conflicted(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Conflicted);
        }
        IEnumerable<string> Locked(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).lockStatus == VCLockStatus.LockedHere);
        }
        IEnumerable<string> NotLocked(IEnumerable<string> assets)
        {
            return
                assets.Where(a => vcc.GetAssetStatus(a).fileStatus != VCFileStatus.Unversioned &&
                vcc.GetAssetStatus(a).lockStatus == VCLockStatus.NoLock);
        }
        IEnumerable<string> FilesExist(IEnumerable<string> assets)
        {
            return assets.Where(File.Exists);
        }
        static IEnumerable<string> ShortestFirst(IEnumerable<string> assets)
        {
            return assets.OrderBy(s => s.Length);
        }
        IEnumerable<string> AddFolders(IEnumerable<string> assets)
        {
            return assets
                .Select(a => Path.GetDirectoryName(a))
                .Where(d => GetAssetStatus(d).fileStatus != VCFileStatus.Normal)
                .Concat(assets)
                .Distinct();
        }

        bool InUnversionedParentFolder(string asset)
        {
            return ParentFolders(asset).Any(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned);
        }

        static IEnumerable<string> ParentFolders(string asset)
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

        IEnumerable<string> AddFilesInFolders(IEnumerable<string> assets, bool versionedFoldersOnly = false)
        {
            foreach (var assetIt in new List<string>(assets))
            {
                if (Directory.Exists(assetIt) && (!versionedFoldersOnly || GetAssetStatus(assetIt).fileStatus != VCFileStatus.Unversioned))
                {
                    assets = assets
                        .Concat(Directory.GetFiles(assetIt, "*", SearchOption.AllDirectories)
                        .Where(a => File.Exists(a) && !a.Contains("/.") && !a.Contains("\\.") && (File.GetAttributes(a) & FileAttributes.Hidden) == 0)
                        .Select(s => s.Replace("\\", "/")));
                }
            }
            return assets;
        }

        IEnumerable<string> RemoveFolders(IEnumerable<string> assets)
        {
            return assets.Where(a => !Directory.Exists(a));
        }

        IEnumerable<string> RemoveFilesUnderUnversionedFolders(IEnumerable<string> assets)
        {
            var folders = assets.Where(a => Directory.Exists(a) && GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned);
            assets = assets.Where(a => !folders.Any(f => a.StartsWith(f) && a != f));
            return assets;
        }
        #endregion
    }

}
