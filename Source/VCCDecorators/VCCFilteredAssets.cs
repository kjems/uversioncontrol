// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VersionControl.AssetFilters;

namespace VersionControl
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    using Logging;
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
            if (vcc.InUnversionedParentFolder(assetPath)) return new VersionControlStatus() {assetPath = new ComposedString(assetPath), fileStatus = VCFileStatus.Unversioned};
            return vcc.GetAssetStatus(assetPath);
        }

        public override bool Status(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            assets = vcc.InVersionedFolder(NonEmpty(assets));
            return assets.Any() ? base.Status(assets, statusLevel) : true;
        }

        public override bool RequestStatus(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            if (assets == null) return true;
            assets = NonEmpty(assets).ToList();
            return assets.Any() ? base.RequestStatus(assets, statusLevel) : true;
        }

        public override bool Update(IEnumerable<string> assets = null)
        {
            return base.Update((assets != null ? vcc.Versioned(assets) : null));
        }

        public override bool Commit(IEnumerable<string> assets, string commitMessage = "")
        {
            var filesInFolders = vcc.AddedOrUnversionedParentFolders(vcc.AddFilesInFolders(assets, true)).ToArray();
            return
                base.Add(vcc.UnversionedInVersionedFolder(filesInFolders)) &&
                base.Delete(vcc.Missing(filesInFolders), OperationMode.Normal) &&
                base.Commit(ShortestFirst(filesInFolders), commitMessage) &&
                Status(assets, StatusLevel.Local) &&
                ReleaseLock(assets);
        }

        public override bool Add(IEnumerable<string> assets)
        {
            return base.Add(vcc.UnversionedInVersionedFolder(assets));
        }

        public override bool Revert(IEnumerable<string> assets)
        {
            assets = ShortestFirst(assets);
            return assets.Any() ? base.Revert(assets) : true;
        }

        public override bool Delete(IEnumerable<string> assets, OperationMode mode)
        {
            return base.Delete(vcc.Versioned(assets), mode);
        }

        public override bool GetLock(IEnumerable<string> assets, OperationMode mode)
        {
            try
            {
                return base.GetLock(mode == OperationMode.Force ? vcc.Versioned(assets) : vcc.NotLocked(assets), mode);
            }
            catch (VCLockedByOther e)
            {
                D.Log("Locked by other, so requesting remote status on : " + assets.Aggregate((a, b) => a + ", " + b) + "\n" + e.Message);
                RequestStatus(assets, StatusLevel.Remote);
                return false;
            }
        }

        public override bool Resolve(IEnumerable<string> assets, ConflictResolution conflictResolution)
        {
            return base.Resolve(FilesExist(assets), conflictResolution);
        }

        public override bool ReleaseLock(IEnumerable<string> assets)
        {
            return base.ReleaseLock(vcc.Locked(assets));
        }

        public override bool ChangeListAdd(IEnumerable<string> assets, string changelist)
        {
            assets = vcc.Versioned(NonEmpty(assets));
            return assets.Any() ? (base.ChangeListAdd(assets, changelist) && Status(assets, StatusLevel.Local)) : false;
        }

        public override bool ChangeListRemove(IEnumerable<string> assets)
        {
            assets = vcc.Versioned(vcc.OnChangeList(NonEmpty(assets)));
            return assets.Any() ? base.ChangeListRemove(vcc.Versioned(vcc.OnChangeList(FilesExist(assets)))) && Status(assets, StatusLevel.Local) : false;
        }

        public override bool Move(string from, string to)
        {
            if (vcc.GetAssetStatus(from).fileStatus == VCFileStatus.Unversioned) return false;
            if (vcc.InUnversionedParentFolder(to)) return false;
            return base.Move(from, to);
        }

        static IEnumerable<string> NonEmpty(IEnumerable<string> assets)
        {
            return assets.Where(a => !string.IsNullOrEmpty(a)).ToArray();
        }

        static IEnumerable<string> ShortestFirst(IEnumerable<string> assets)
        {
            return assets.OrderBy(s => s.Length);
        }
        
        static IEnumerable<string> FilesExist(IEnumerable<string> assets)
        {
            return assets.Where(File.Exists).ToArray();
        }
    }

}
