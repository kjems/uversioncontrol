// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Threading.Tasks;
using VersionControl;

public static class VersionControlStatusExtension
{    
    private static readonly ComposedString meta = new ComposedString(VCCAddMetaFiles.meta);
    public static VersionControlStatus MetaStatus(this VersionControlStatus vcs)
    {
        return vcs.assetPath.EndsWith(meta) ? vcs : VCCommands.Instance.GetAssetStatus(vcs.assetPath + meta);
    }
    public static bool ModifiedWithoutLock(this VersionControlStatus vcs)
    {
        return (vcs.fileStatus == VCFileStatus.Modified && vcs.lockStatus != VCLockStatus.LockedHere && !VCUtility.IsMergableTextAsset(vcs.assetPath));
    }    
    public static bool BypassRevisionControl(this VersionControlStatus vcs)
    {
        return vcs.allowLocalEdit;
    }
    public static bool ModifiedOrBypassed(this VersionControlStatus vcs)
    {
        return ModifiedWithoutLock(vcs) || BypassRevisionControl(vcs);
    }
}
