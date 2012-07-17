// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Collections.Generic;

namespace VersionControl
{
    public enum ConflictResolution
    {
        Mine,
        Theirs,
        Ignore
    }
    public enum StatusLevel
    {
        Local,
        Remote
    }
    public enum DetailLevel
    {
        Normal,
        Verbose
    }
    public enum OperationMode
    {
        Normal,
        Force
    }

    /// <summary>
    /// IVersionControlCommands is the centerpoint for the Version Control. This interface represents all actions that
    /// can be performed on the underlying version control system. 
    /// * All version control backends implement this interface.
    /// * All VCCDecorator's decorates this interface
    /// </summary>
    public interface IVersionControlCommands
    {
        void Start();
        void Stop();
        bool IsReady();
        void SetWorkingDirectory(string workingDirectory);
        void SetUserCredentials(string userName, string password);
        VersionControlStatus GetAssetStatus(string assetPath);
        IEnumerable<string> GetFilteredAssets(Func<string, VersionControlStatus, bool> filter);
        bool Status(StatusLevel statusLevel, DetailLevel detailLevel);
        bool Status(IEnumerable<string> assets, StatusLevel statusLevel);
        bool RequestStatus(IEnumerable<string> assets, StatusLevel statusLevel);
        bool RequestStatus(string asset, StatusLevel statusLevel);
        bool Update(IEnumerable<string> assets = null);
        bool Commit(IEnumerable<string> assets, string commitMessage = "");
        bool Add(IEnumerable<string> assets);
        bool Revert(IEnumerable<string> assets);
        bool Delete(IEnumerable<string> assets, OperationMode mode);
        bool GetLock(IEnumerable<string> assets, OperationMode mode);
        bool ReleaseLock(IEnumerable<string> assets);
        bool ChangeListAdd(IEnumerable<string> assets, string changelist);
        bool ChangeListRemove(IEnumerable<string> assets);
        bool Resolve(IEnumerable<string> assets, ConflictResolution conflictResolution);
        bool Checkout(string url, string path = "");
        bool Move(string from, string to);
        string GetBasePath(string assetPath);
        bool CleanUp();
        void ClearDatabase();
        void RemoveFromDatabase(IEnumerable<string> assets);
        
        event Action<string> ProgressInformation;
        event Action StatusCompleted;
    }
}