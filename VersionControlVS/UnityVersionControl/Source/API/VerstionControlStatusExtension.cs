// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Threading.Tasks;
using VersionControl;
using ComposedString = VersionControl.ComposedSet<string, VersionControl.FilesAndFoldersComposedStringDatabase>;

public static class VersionControlStatusExtension
{
    
    public static VersionControlStatus MetaStatus(this VersionControlStatus vcs)
    {
        return vcs.assetPath.EndsWith(VCCAddMetaFiles.meta) ? vcs : VCCommands.Instance.GetAssetStatus(vcs.assetPath + VCCAddMetaFiles.meta);
    }
    public static bool ModifiedWithoutLock(this VersionControlStatus vcs)
    {
        return (vcs.fileStatus == VCFileStatus.Modified && vcs.lockStatus != VCLockStatus.LockedHere && !VCUtility.IsMergableAsset(vcs.assetPath));
    }    
    public static bool LocalEditAllowed(this VersionControlStatus vcs)
    {
        return vcs.allowLocalEdit;
    }
    public static bool ModifiedOrLocalEditAllowed(this VersionControlStatus vcs)
    {
        return ModifiedWithoutLock(vcs) || LocalEditAllowed(vcs);
    }
    public static bool ModifiedWithoutRights(this VersionControlStatus vcs)
    {
        return ModifiedWithoutLock(vcs) && !LocalEditAllowed(vcs);
    }
}
