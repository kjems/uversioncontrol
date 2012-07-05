// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Collections.Generic;

namespace VersionControl
{
    public class VCCDecorator : IVersionControlCommands
    {
        protected VCCDecorator(IVersionControlCommands vcc)
        {
            this.vcc = vcc;
            vcc.ProgressInformation += progress => { if (ProgressInformation != null) ProgressInformation(progress); };
            vcc.StatusCompleted += () => { if (StatusCompleted != null) StatusCompleted(); };
        }

        protected readonly IVersionControlCommands vcc;

        public virtual bool IsReady()
        {
            return vcc.IsReady();
        }

        public virtual void SetWorkingDirectory(string workingDirectory)
        {
            vcc.SetWorkingDirectory(workingDirectory);
        }

        public virtual void SetUserCredentials(string userName, string password)
        {
            vcc.SetUserCredentials(userName, password);
        }

        public virtual VersionControlStatus GetAssetStatus(string assetPath)
        {
            return vcc.GetAssetStatus(assetPath);
        }

        public virtual IEnumerable<string> GetFilteredAssets(Func<string, VersionControlStatus, bool> filter)
        {
            return vcc.GetFilteredAssets(filter);
        }

        public virtual bool Status(bool remote, bool full)
        {
            return vcc.Status(remote, full);
        }

        public virtual bool Status(IEnumerable<string> assets, bool remote)
        {
            return vcc.Status(assets, remote);
        }

        public virtual bool RequestStatus(IEnumerable<string> assets, bool remote)
        {
            return vcc.RequestStatus(assets, remote);
        }

        public virtual bool RequestStatus(string asset, bool remote)
        {
            return vcc.RequestStatus(asset, remote);
        }

        public virtual bool Update(IEnumerable<string> assets = null, bool force = true)
        {
            return vcc.Update(assets, force);
        }

        public virtual bool Commit(IEnumerable<string> assets, string commitMessage = "")
        {
            return vcc.Commit(assets, commitMessage);
        }

        public virtual bool Add(IEnumerable<string> assets)
        {
            return vcc.Add(assets);
        }

        public virtual bool Revert(IEnumerable<string> assets)
        {
            return vcc.Revert(assets);
        }

        public virtual bool Delete(IEnumerable<string> assets, bool force = false)
        {
            return vcc.Delete(assets, force);
        }
        
        public virtual bool GetLock(IEnumerable<string> assets, bool force = false)
        {
            return vcc.GetLock(assets, force);
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

        public virtual bool Move(string from, string to)
        {
            return vcc.Move(from, to);
        }

        public virtual string GetBasePath(string assetPath)
        {
            return vcc.GetBasePath(assetPath);
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