// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;

namespace UVC
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;

    #region Enums
    public enum VCFileStatus
    {
        Normal,
        Unversioned,
        Added,
        Modified,
        Deleted,
        Ignored,
        Replaced,
        Missing,
        Conflicted,
        External,
        Incomplete,
        Merged,
        Obstructed,
        None,
    }

    public enum VCDirectoryStatus
    {
        NoModification,
        Conflicted,
        Modified
    }

    public enum VCRepositoryStatus
    {
        NotLocked,
        Locked
    }

    public enum VCRemoteFileStatus
    {
        Modified,
        None
    }

    public enum VCScheduleStatus
    {
        None,
        Commit
    }

    public enum VCLockStatus
    {
        NoLock,
        LockedHere,
        LockedOther,
        LockedButStolen,
        NoLockButHasToken
    }

    public enum VCTreeConflictStatus
    {
        Normal,
        TreeConflict
    }

    public enum VCProperty
    {
        None,
        Normal,
        Conflicted,
        Modified,
    }

    public enum VCReflectionLevel
    {
        None,
        Pending,
        Local,
        Repository,
    }
    #endregion

    [Serializable]
    public sealed class VersionControlStatus
    {
        public VersionControlStatus Clone()
        {
            return MemberwiseClone() as VersionControlStatus;
        }

        public VCReflectionLevel reflectionLevel = VCReflectionLevel.None;
        public VCFileStatus fileStatus = VCFileStatus.Normal;
        public VCDirectoryStatus directoryStatus = VCDirectoryStatus.NoModification;
        public VCRepositoryStatus repositoryStatus = VCRepositoryStatus.NotLocked;
        public VCRemoteFileStatus remoteStatus = VCRemoteFileStatus.None;
        public VCScheduleStatus scheduleStatus = VCScheduleStatus.None;
        public VCLockStatus lockStatus = VCLockStatus.NoLock;
        public VCProperty property = VCProperty.Normal;
        public VCTreeConflictStatus treeConflictStatus = VCTreeConflictStatus.Normal;
        public ComposedString assetPath;
        //public ComposedString movedFrom;
        //public ComposedString movedTo;
        public ComposedString changelist = ComposedString.empty;
        public string user;
        public string owner;
        public string lockToken;
        public int revision;
        public int lastModifiedRevision;
        public bool allowLocalEdit = false;
        public bool localOnly = false;
        public bool Reflected { get { return reflectionLevel == VCReflectionLevel.Local || reflectionLevel == VCReflectionLevel.Repository; } }
    }
}
