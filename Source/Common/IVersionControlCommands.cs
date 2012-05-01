// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnitySVN@gmail.com>
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
    /// <summary>
    /// IVersionControlCommands is the centerpoint for the Version Control. This interface represents all actions that
    /// can be performed on the underlying version control system. 
    /// * All version control backends implement this interface.
    /// * All VCCDecorator's decorates this interface
    /// </summary>
    public interface IVersionControlCommands
    {
        bool IsReady();
        void SetWorkingDirectory(string workingDirectory);
        void SetUserCredentials(string userName, string password);
        VersionControlStatus GetAssetStatus(string assetPath);
        IEnumerable<string> GetFilteredAssets(Func<string, VersionControlStatus, bool> filter);
        bool Status(bool remote, bool full);
        bool Status(IEnumerable<string> assets, bool remote);
        bool Update(IEnumerable<string> assets = null, bool force = true);
        bool Commit(IEnumerable<string> assets, string commitMessage = "");
        bool Add(IEnumerable<string> assets);
        bool Revert(IEnumerable<string> assets);
        bool Delete(IEnumerable<string> assets, bool force = false);
        bool GetLock(IEnumerable<string> assets, bool force = false);
        bool ReleaseLock(IEnumerable<string> assets);
        bool ChangeListAdd(IEnumerable<string> assets, string changelist);
        bool ChangeListRemove(IEnumerable<string> assets);
        bool Resolve(IEnumerable<string> assets, ConflictResolution conflictResolution);
        bool Checkout(string url, string path = "");
        bool Move(string from, string to);
        string GetBasePath(string assetPath);
        bool CleanUp();
        void ClearDatabase();
        event Action<string> ProgressInformation;
    }
}