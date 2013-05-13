// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Collections.Generic;

namespace VersionControl.Backend.Noop
{

    public class NoopCommands : IVersionControlCommands
    {
        readonly VersionControlStatus defaultStatus = new VersionControlStatus();
        public NoopCommands() { }
        public void Dispose() { }
        public virtual void Start() { }
        public virtual void Stop() { }
        public virtual void ActivateRefreshLoop() { }
        public virtual void DeactivateRefreshLoop() { }
        public virtual bool IsReady() { return false; }
        public virtual bool HasValidLocalCopy() { return true; }
        public virtual void SetWorkingDirectory(string workingDirectory) { }
        public virtual void SetUserCredentials(string userName, string password) { }
        public virtual VersionControlStatus GetAssetStatus(string assetPath)
        {
            return defaultStatus;
        }
        public virtual VersionControlStatus GetAssetStatus(ComposedString assetPath)
        {
            return defaultStatus;
        }
        public virtual IEnumerable<VersionControlStatus> GetFilteredAssets(Func<VersionControlStatus, bool> filter)
        {
            return new VersionControlStatus[0];
        }
        public virtual bool RequestStatus(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            return true;
        }
        public virtual bool Status(StatusLevel statusLevel, DetailLevel detailLevel)
        {
            return true;
        }
        public virtual bool Status(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            return true;
        }
        public virtual bool Update(IEnumerable<string> assets = null)
        {
            return true;
        }
        public virtual bool Commit(IEnumerable<string> assets, string commitMessage = "")
        {
            return true;
        }
        public virtual bool Add(IEnumerable<string> assets)
        {
            return true;
        }
        public virtual bool Revert(IEnumerable<string> assets)
        {
            return true;
        }
        public virtual bool Delete(IEnumerable<string> assets, OperationMode mode)
        {
            return true;
        }
        public virtual bool GetLock(IEnumerable<string> assets, OperationMode mode)
        {
            return true;
        }
        public virtual bool ReleaseLock(IEnumerable<string> assets)
        {
            return true;
        }
        public virtual bool ChangeListAdd(IEnumerable<string> assets, string changelist)
        {
            return true;
        }
        public virtual bool ChangeListRemove(IEnumerable<string> assets)
        {
            return true;
        }
        public virtual bool Resolve(IEnumerable<string> assets, ConflictResolution conflictResolution)
        {
            return true;
        }
        public virtual bool Checkout(string url, string path = "")
        {
            return true;
        }
        public virtual bool Move(string from, string to)
        {
            return true;
        }
        public virtual string GetBasePath(string assetPath)
        {
            return "";
        }
        public virtual bool CleanUp()
        {
            return true;
        }
        public virtual void ClearDatabase() { }
        public virtual void RemoveFromDatabase(IEnumerable<string> assets) { }
        public event Action<string> ProgressInformation;
        public event Action StatusCompleted;
    }
}
