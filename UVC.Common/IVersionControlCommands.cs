// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Collections.Generic;

namespace UVC
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;

    public enum ConflictResolution
    {
        Mine,
        Theirs,
        Working,
        Ignore
    }
    public enum StatusLevel
    {
        Local,
        Remote,
        Previous
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
    public interface IVersionControlCommands : IDisposable
    {
        void Start();
        void Stop();
        void ActivateRefreshLoop();
        void DeactivateRefreshLoop();
        bool IsReady();
        bool HasValidLocalCopy();
        void SetWorkingDirectory(string workingDirectory);
        bool SetUserCredentials(string userName, string password, bool cacheCredentials);
        VersionControlStatus GetAssetStatus(string assetPath);
        VersionControlStatus GetAssetStatus(ComposedString assetPath);
        InfoStatus GetInfo(string path);
        IEnumerable<VersionControlStatus> GetFilteredAssets(Func<VersionControlStatus, bool> filter);
        bool RequestStatus(IEnumerable<string> assets, StatusLevel statusLevel);
        bool Status(StatusLevel statusLevel, DetailLevel detailLevel);
        bool Status(IEnumerable<string> assets, StatusLevel statusLevel);
        bool Update(IEnumerable<string> assets = null);
        bool Update(int revision, IEnumerable<string> assets = null);
        bool Commit(IEnumerable<string> assets, string commitMessage = "");
        bool Commit(string commitMessage = "");
        bool Add(IEnumerable<string> assets);
        bool Revert(IEnumerable<string> assets);
        bool Delete(IEnumerable<string> assets, OperationMode mode);
        bool GetLock(IEnumerable<string> assets, OperationMode mode);
        bool ReleaseLock(IEnumerable<string> assets);
        bool ChangeListAdd(IEnumerable<string> assets, string changelist);
        bool ChangeListRemove(IEnumerable<string> assets);
        bool Resolve(IEnumerable<string> assets, ConflictResolution conflictResolution);
        bool Checkout(string url, string path = "");
        bool CreateBranch(string from, string to);
        bool MergeBranch(string url, string path = "");
        bool SwitchBranch(string url, string path = "");
        string GetCurrentBranch();
        string GetBranchDefaultPath();
        string GetTrunkPath();
        List<BranchStatus> RemoteList(string path);
        bool AllowLocalEdit(IEnumerable<string> assets);
        bool SetLocalOnly(IEnumerable<string> assets);
        bool Move(string from, string to);
        bool SetIgnore(string path, IEnumerable<string> assets);
        IEnumerable<string> GetIgnore(string path);
        int GetRevision();
        string GetBasePath(string assetPath);
        bool GetConflict(string assetPath, out string basePath, out string yours, out string theirs);
        bool CleanUp();
        void ClearDatabase();
        void RemoveFromDatabase(IEnumerable<string> assets);

        event Action<string> ProgressInformation;
        event Action StatusCompleted;
    }
}
