// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnitySVN@gmail.com>
using System;
using System.Collections.Generic;
using System.Linq;
#pragma warning disable 0067 // event progressInformation not used, but needed to implement interface

namespace VersionControl.UnitTests
{
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

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public bool IsReady()
        {
            return true;
        }

        public void SetWorkingDirectory(string workingDirectory)
        {
        }

        public void SetUserCredentials(string userName, string password)
        {
        }

        public VersionControlStatus GetAssetStatus(string assetPath)
        {
            return statusDatabase[assetPath];
        }

        public IEnumerable<string> GetFilteredAssets(Func<string, VersionControlStatus, bool> filter)
        {
            return statusDatabase.Keys.Where(k => filter(k, statusDatabase[k])).ToList();
        }

        public bool Status(StatusLevel statusLevel, DetailLevel detailLevel)
        {
            return true;
        }

        public virtual bool Status(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            return true;
        }

        public bool RequestStatus(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            return true;
        }
        
        public bool RequestStatus(string asset, StatusLevel statusLevel)
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

        public bool Commit(IEnumerable<string> assets, string commitMessage = "")
        {
            dataCarrier.assets = assets.ToList();
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

        public bool Resolve(IEnumerable<string> assets, ConflictResolution conflictResolution)
        {
            dataCarrier.assets = assets.ToList();
            return true;
        }

        public bool Move(string from, string to)
        {
            return true;
        }

        public virtual string GetBasePath(string assetPath)
        {
            return "";
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