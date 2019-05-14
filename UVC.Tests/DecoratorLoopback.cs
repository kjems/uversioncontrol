// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnitySVN@gmail.com>
using System;
using System.Collections.Generic;
using System.Linq;
#pragma warning disable 0067 // event progressInformation not used, but needed to implement interface

namespace UVC.UnitTests
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    internal class DataCarrier
    {
        public List<string> assets;
    }

    internal class DecoratorLoopback : IVersionControlCommands
    {
        private readonly StatusDatabase statusDatabase;
        private readonly DataCarrier dataCarrier;
        public DecoratorLoopback(DataCarrier carrier, StatusDatabase statusDatabase)
        {
            dataCarrier = carrier;
            this.statusDatabase = statusDatabase;
        }

        public void Dispose()
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public virtual void ActivateRefreshLoop()
        {
        }

        public virtual void DeactivateRefreshLoop()
        {
        }

        public bool IsReady()
        {
            return true;
        }

        public bool HasValidLocalCopy()
        {
            return false;
        }

        public void SetWorkingDirectory(string workingDirectory)
        {
        }

        public bool SetUserCredentials(string userName, string password, bool cacheCredentials)
        {
            return true;
        }

        public VersionControlStatus GetAssetStatus(string assetPath)
        {
            return GetAssetStatus(new ComposedString(assetPath));
        }

        public VersionControlStatus GetAssetStatus(ComposedString assetPath)
        {
            return statusDatabase[assetPath];
        }

        public virtual InfoStatus GetInfo(string path)
        {
            return null;
        }

        public IEnumerable<VersionControlStatus> GetFilteredAssets(Func<VersionControlStatus, bool> filter)
        {
            return statusDatabase.Values.Where(filter).ToList();
        }

        public virtual bool RequestStatus(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            return true;
        }

        public bool Status(StatusLevel statusLevel, DetailLevel detailLevel)
        {
            return true;
        }

        public virtual bool Status(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            return true;
        }

        public virtual bool SetStatusRequestRule(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            return true;
        }

        public bool RequestStatus(IEnumerable<string> assets)
        {
            return true;
        }

        public bool Update(IEnumerable<string> assets)
        {
            if (assets != null)
            {
                List<string> list = assets.ToList();
                dataCarrier.assets = list;
            }
            return true;
        }
        
        public bool Update(int revision, IEnumerable<string> assets)
        {
            if (assets != null)
            {
                List<string> list = assets.ToList();
                dataCarrier.assets = list;
            }
            return true;
        }

        public bool Commit(IEnumerable<string> assets, string commitMessage = "")
        {
            dataCarrier.assets = assets.ToList();
            return true;
        }

        public bool Commit(string commitMessage = "")
        {
            return true;
        }

        public bool Add(IEnumerable<string> assets)
        {
            dataCarrier.assets = assets.ToList();
            return true;
        }

        public bool Revert(IEnumerable<string> assets)
        {
            dataCarrier.assets = assets.ToList();
            return true;
        }

        public bool Delete(IEnumerable<string> assets, OperationMode mode)
        {
            dataCarrier.assets = assets.ToList();
            return true;
        }

        public bool GetLock(IEnumerable<string> assets, OperationMode mode)
        {
            dataCarrier.assets = assets.ToList();
            return true;
        }

        public bool ReleaseLock(IEnumerable<string> assets)
        {
            dataCarrier.assets = assets.ToList();
            return true;
        }

        public bool ChangeListAdd(IEnumerable<string> assets, string changelist)
        {
            dataCarrier.assets = assets.ToList();
            return true;
        }

        public bool ChangeListRemove(IEnumerable<string> assets)
        {
            dataCarrier.assets = assets.ToList();
            return true;
        }

        public bool Checkout(string url, string path = "")
        {
            return true;
        }

        public bool CreateBranch(string from, string to)
        {
            return true;
        }

        public bool MergeBranch(string url, string path = "")
        {
            return true;
        }

        public bool SwitchBranch(string url, string path = "")
        {
            return true;
        }

        public string GetCurrentBranch()
        {
            return null;
        }

        public string GetBranchDefaultPath()
        {
            return null;
        }

        public string GetTrunkPath()
        {
            return null;
        }

        public List<BranchStatus> RemoteList(string path)
        {
            return null;
        }

        public bool AllowLocalEdit(IEnumerable<string> assets)
        {
            dataCarrier.assets = assets.ToList();
            return true;
        }

        public bool SetLocalOnly(IEnumerable<string> assets)
        {
            dataCarrier.assets = assets.ToList();
            return true;
        }

        public bool Resolve(IEnumerable<string> assets, ConflictResolution conflictResolution)
        {
            dataCarrier.assets = assets.ToList();
            return true;
        }

        public bool Move(string from, string to)
        {
            return true;
        }

        public bool SetIgnore(string path, IEnumerable<string> assets)
        {
            return true;
        }

        public IEnumerable<string> GetIgnore(string path)
        {
            return null;
        }

        public virtual int GetRevision()
        {
            return 0;
        }

        public virtual string GetBasePath(string assetPath)
        {
            return "";
        }

        public virtual bool GetConflict(string assetPath, out string basePath, out string yours, out string theirs)
        {
            basePath = null;
            yours = null;
            theirs = null;
            return false;
        }

        public bool CleanUp()
        {
            return true;
        }

        public void ClearDatabase()
        {
        }

        public void RemoveFromDatabase(IEnumerable<string> assets)
        {
        }

        public event Action<string> ProgressInformation;
        public event Action StatusCompleted;
    }
}
