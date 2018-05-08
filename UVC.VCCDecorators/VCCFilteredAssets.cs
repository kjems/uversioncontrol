// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VersionControl.AssetPathFilters;

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
            if (assetPath.InUnversionedParentFolder(vcc)) return new VersionControlStatus() { assetPath = new ComposedString(assetPath), fileStatus = VCFileStatus.Unversioned };
            return vcc.GetAssetStatus(assetPath);
        }

        public override bool Status(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            assets = assets.NonEmpty().InVersionedFolder(vcc);
            return assets.Any() ? base.Status(assets, statusLevel) : true;
        }

        public override bool RequestStatus(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            if (assets == null) return true;
            assets = assets.NonEmpty();
            return assets.Any() ? base.RequestStatus(assets, statusLevel) : true;
        }

        public override bool Update(IEnumerable<string> assets = null)
        {
            return base.Update((assets != null ? assets.Versioned(vcc) : null));
        }

        public override bool Commit(IEnumerable<string> assets, string commitMessage = "")
        {
            var filesInFolders = assets.AddFilesInFolders(vcc, true).AddedOrUnversionedParentFolders(vcc).ToArray();
            var deletedInFolders = assets.AddDeletedInFolders(vcc);

            D.Log("Deleted In Folders: " + deletedInFolders.AggregateString());

            bool result =
                base.Add(filesInFolders.UnversionedInVersionedFolder(vcc)) &&
                base.Delete(filesInFolders.Missing(vcc), OperationMode.Normal) &&
                base.Commit(filesInFolders.ShortestFirst(), commitMessage) &&
                Status(assets, StatusLevel.Local) &&
                ReleaseLock(assets);

            if (result)
                RemoveFromDatabase(deletedInFolders);

            return result;
        }

        public override bool Add(IEnumerable<string> assets)
        {
            return base.Add(assets.UnversionedInVersionedFolder(vcc));
        }

        public override bool Revert(IEnumerable<string> assets)
        {
            assets = assets.ShortestFirst();
            return assets.Any() ? base.Revert(assets) : true;
        }

        public override bool Delete(IEnumerable<string> assets, OperationMode mode)
        {
            return base.Delete(assets.Versioned(vcc), mode);
        }

        public override bool GetLock(IEnumerable<string> assets, OperationMode mode)
        {
            try
            {
                return base.GetLock(mode == OperationMode.Force ? assets.Versioned(vcc) : assets.NotLocked(vcc), mode);
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
            return base.Resolve(assets.FilesExist(), conflictResolution);
        }

        public override bool ReleaseLock(IEnumerable<string> assets)
        {
            return base.ReleaseLock(assets.Locked(vcc));
        }

        public override bool ChangeListAdd(IEnumerable<string> assets, string changelist)
        {
            assets = assets.NonEmpty().Versioned(vcc);
            return assets.Any() ? (base.ChangeListAdd(assets, changelist) && Status(assets, StatusLevel.Local)) : false;
        }

        public override bool ChangeListRemove(IEnumerable<string> assets)
        {
            assets = assets.NonEmpty().Versioned(vcc).OnChangeList(vcc);
            return assets.Any() ? base.ChangeListRemove(assets.FilesExist().OnChangeList(vcc).Versioned(vcc)) && Status(assets, StatusLevel.Local) : false;
        }

        public override bool Move(string from, string to)
        {
            if (vcc.GetAssetStatus(from).fileStatus == VCFileStatus.Unversioned) return false;
            if (to.InUnversionedParentFolder(vcc)) return false;
            return base.Move(from, to);
        }

    }

}
