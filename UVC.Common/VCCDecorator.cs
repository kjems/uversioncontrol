// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Collections.Generic;

namespace UVC
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;

    [Serializable]
    public class VCCDecorator : IVersionControlCommands
    {
        protected VCCDecorator(IVersionControlCommands vcc)
        {
            this.vcc = vcc;
            vcc.ProgressInformation += progress => { if (ProgressInformation != null) ProgressInformation(progress); };
            vcc.StatusCompleted += () => { if (StatusCompleted != null) StatusCompleted(); };
        }

        protected readonly IVersionControlCommands vcc;

        public void Dispose()
        {
            vcc.Dispose();
        }

        public virtual void Start()
        {
            vcc.Start();
        }

        public virtual void Stop()
        {
            vcc.Stop();
        }

        public virtual void ActivateRefreshLoop()
        {
            vcc.ActivateRefreshLoop();
        }

        public virtual void DeactivateRefreshLoop()
        {
            vcc.DeactivateRefreshLoop();
        }

        public virtual bool IsReady()
        {
            return vcc.IsReady();
        }

        public virtual bool HasValidLocalCopy()
        {
            return vcc.HasValidLocalCopy();
        }

        public virtual void SetWorkingDirectory(string workingDirectory)
        {
            vcc.SetWorkingDirectory(workingDirectory);
        }

        public virtual bool SetUserCredentials(string userName, string password, bool cacheCredentials)
        {
            return vcc.SetUserCredentials(userName, password, cacheCredentials);
        }

        public virtual VersionControlStatus GetAssetStatus(string assetPath)
        {
            return vcc.GetAssetStatus(assetPath);
        }

        public virtual VersionControlStatus GetAssetStatus(ComposedString assetPath)
        {
            return vcc.GetAssetStatus(assetPath);
        }

        public virtual InfoStatus GetInfo(string path)
        {
            return vcc.GetInfo(path);
        }

        public virtual IEnumerable<VersionControlStatus> GetFilteredAssets(Func<VersionControlStatus, bool> filter)
        {
            return vcc.GetFilteredAssets(filter);
        }

        public virtual bool RequestStatus(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            return vcc.RequestStatus(assets, statusLevel);
        }

        public virtual bool Status(StatusLevel statusLevel, DetailLevel detailLevel)
        {
            return vcc.Status(statusLevel, detailLevel);
        }

        public virtual bool Status(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            return vcc.Status(assets, statusLevel);
        }

        public virtual bool Update(IEnumerable<string> assets = null)
        {
            return vcc.Update(assets);
        }
        
        public virtual bool Update(int revision, IEnumerable<string> assets = null)
        {
            return vcc.Update(revision, assets);
        }

        public virtual bool Commit(IEnumerable<string> assets, string commitMessage = "")
        {
            return vcc.Commit(assets, commitMessage);
        }

        public bool Commit(string commitMessage = "")
        {
            return vcc.Commit(commitMessage);
        }

        public virtual bool Add(IEnumerable<string> assets)
        {
            return vcc.Add(assets);
        }

        public virtual bool Revert(IEnumerable<string> assets)
        {
            return vcc.Revert(assets);
        }

        public virtual bool Delete(IEnumerable<string> assets, OperationMode mode)
        {
            return vcc.Delete(assets, mode);
        }

        public virtual bool GetLock(IEnumerable<string> assets, OperationMode mode)
        {
            return vcc.GetLock(assets, mode);
        }

        public virtual bool ReleaseLock(IEnumerable<string> assets)
        {
            return vcc.ReleaseLock(assets);
        }

        public virtual bool ChangeListAdd(IEnumerable<string> assets, string changelist)
        {
            return vcc.ChangeListAdd(assets, changelist);
        }

        public virtual bool ChangeListRemove(IEnumerable<string> assets)
        {
            return vcc.ChangeListRemove(assets);
        }

        public virtual bool Resolve(IEnumerable<string> assets, ConflictResolution conflictResolution)
        {
            return vcc.Resolve(assets, conflictResolution);
        }

        public virtual bool Checkout(string url, string path = "")
        {
            return vcc.Checkout(url, path);
        }

        public virtual bool CreateBranch(string from, string to)
        {
            return vcc.CreateBranch(from, to);
        }

        public virtual bool MergeBranch(string url, string path = "")
        {
            return vcc.MergeBranch(url, path);
        }

        public virtual bool SwitchBranch(string url, string path = "")
        {
            return vcc.SwitchBranch(url, path);
        }

        public virtual string GetCurrentBranch()
        {
            return vcc.GetCurrentBranch();
        }

        public virtual string GetBranchDefaultPath()
        {
            return vcc.GetBranchDefaultPath();
        }

        public virtual string GetTrunkPath()
        {
            return vcc.GetTrunkPath();
        }

        public List<BranchStatus> RemoteList(string path)
        {
            return vcc.RemoteList(path);
        }

        public virtual bool AllowLocalEdit(IEnumerable<string> assets)
        {
            return vcc.AllowLocalEdit(assets);
        }

        public virtual bool SetLocalOnly(IEnumerable<string> assets)
        {
            return vcc.SetLocalOnly(assets);
        }

        public virtual bool Move(string from, string to)
        {
            return vcc.Move(from, to);
        }

        public virtual bool SetIgnore(string path, IEnumerable<string> assets)
        {
            return vcc.SetIgnore(path, assets);
        }

        public IEnumerable<string> GetIgnore(string path)
        {
            return vcc.GetIgnore(path);
        }

        public virtual int GetRevision()
        {
            return vcc.GetRevision();
        }

        public virtual string GetBasePath(string assetPath)
        {
            return vcc.GetBasePath(assetPath);
        }

        public virtual bool GetConflict(string assetPath, out string basePath, out string yours, out string theirs)
        {
            return vcc.GetConflict(assetPath, out basePath, out yours, out theirs);
        }

        public virtual bool CleanUp()
        {
            return vcc.CleanUp();
        }

        public virtual void ClearDatabase()
        {
            vcc.ClearDatabase();
        }

        public virtual void RemoveFromDatabase(IEnumerable<string> assets)
        {
            vcc.RemoveFromDatabase(assets);
        }

        public event Action<string> ProgressInformation;
        public event Action StatusCompleted;
    }
}
