using System;
using UnityEngine;

namespace VersionControl.UserInterface
{
    public static class AssetStatusUtils
    {
        private static readonly Color orange = new Color(1.0f, 0.65f, 0.0f);
        private static readonly Color pastelRed = new Color(0.85f, 0.4f, 0.4f);
        private static readonly Color pastelBlue = new Color(0.3f, 0.55f, 0.85f);
        private static readonly Color lightgrey = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color pink = new Color(1f, 0.6f, 1f);
        private static readonly Color black = Color.black;
        private static readonly Color border = new Color(0.1f, 0.1f, 0.1f);

        private static readonly Color addedColor = Color.blue;
        private static readonly Color conflictedColor = Color.red;
        private static readonly Color missingColor = new Color(1.0f, 0.2f, 1.0f);
        private static readonly Color normalColor = new Color(0.9f, 0.9f, 0.9f, 0.05f);
        private static readonly Color lockedColor = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color lockedOtherColor = new Color(0.9f, 0.3f, 0.3f);
        private static readonly Color modifiedColor = orange;
        private static readonly Color unversionedColor = new Color(0.4f, 0.4f, 1.0f);
        private static readonly Color remoteModifiedColor = new Color(1.0f, 0.9f, 0.9f, 0.4f);
        private static readonly Color pendingColor = new Color(0.9f, 0.9f, 0.6f, 0.3f);
        private static readonly Color ignoreColor = new Color(0.7f, 0.7f, 0.7f, 0.1f);
        private static readonly Color deletedColor = new Color(0.3f, 0.3f, 0.3f, 0.1f);

        public static Color GetStatusColor(VersionControlStatus assetStatus, bool includeLockStatus)
        {
            if (assetStatus.treeConflictStatus == VCTreeConflictStatus.TreeConflict) return conflictedColor;
            if (assetStatus.fileStatus == VCFileStatus.Conflicted) return conflictedColor;
            if (assetStatus.fileStatus == VCFileStatus.Missing) return missingColor;
            if (assetStatus.fileStatus == VCFileStatus.Ignored) return ignoreColor;
            if (assetStatus.bypassRevisionControl) return modifiedColor;
            if (assetStatus.fileStatus == VCFileStatus.Added) return addedColor;

            if (includeLockStatus)
            {
                if (assetStatus.lockStatus == VCLockStatus.LockedHere) return lockedColor;
                if (assetStatus.lockStatus == VCLockStatus.LockedOther) return lockedOtherColor;
            }

            if (assetStatus.fileStatus == VCFileStatus.Modified) return modifiedColor;
            if (assetStatus.reflectionLevel == VCReflectionLevel.Pending) return pendingColor;
            if (assetStatus.fileStatus == VCFileStatus.Deleted) return deletedColor;
            if (assetStatus.fileStatus == VCFileStatus.Unversioned) return unversionedColor;
            if (assetStatus.remoteStatus == VCRemoteFileStatus.Modified) return remoteModifiedColor;
            if (assetStatus.fileStatus == VCFileStatus.Normal) return normalColor;

            return pink;
        }

        public static string GetStatusText(VersionControlStatus assetStatus)
        {
            if (assetStatus.reflectionLevel == VCReflectionLevel.Pending) return "Pending";
            if (assetStatus.lockStatus == VCLockStatus.LockedHere) return Terminology.getlock;
            if (assetStatus.bypassRevisionControl) return "Bypass Lock";
            if (assetStatus.lockStatus == VCLockStatus.LockedOther) return Terminology.lockedBy + "'" + assetStatus.owner + "'\nShift click to force open";
            if (assetStatus.fileStatus == VCFileStatus.Modified || assetStatus.bypassRevisionControl) return Terminology.bypass;
            if (assetStatus.fileStatus == VCFileStatus.Unversioned) return Terminology.unversioned;
            if (assetStatus.fileStatus == VCFileStatus.Added) return "Added";
            if (assetStatus.fileStatus == VCFileStatus.Conflicted) return "Conflicted";
            if (assetStatus.fileStatus == VCFileStatus.Replaced) return "Replaced";
            if (assetStatus.fileStatus == VCFileStatus.Ignored) return "Ignored";
            if (assetStatus.remoteStatus == VCRemoteFileStatus.Modified) return "Modified on server";
            if (assetStatus.fileStatus == VCFileStatus.Normal) return "Normal";
            return "-";
        }

        public static string GetLockStatusMessage(VersionControlStatus assetStatus)
        {
            string lockMessage = assetStatus.lockStatus.ToString();
            if (assetStatus.lockStatus == VCLockStatus.LockedOther) lockMessage = Terminology.getlock + " by: " + assetStatus.owner;
            if (assetStatus.lockStatus == VCLockStatus.LockedHere) lockMessage = Terminology.getlock + " Here: " + assetStatus.owner;
            if (assetStatus.lockStatus == VCLockStatus.NoLock)
            {
                if (ComposedString.IsNullOrEmpty(assetStatus.assetPath)) lockMessage = "Not saved";
                else if (assetStatus.fileStatus == VCFileStatus.Added) lockMessage = "Added";
                else if (assetStatus.fileStatus == VCFileStatus.Replaced) lockMessage = "Replaced";
                else lockMessage = VCUtility.ManagedByRepository(assetStatus) ? "Not " + Terminology.getlock : "Not on Version Control";
            }
            if (assetStatus.bypassRevisionControl)
            {
                lockMessage = Terminology.bypass;
                if ((assetStatus.lockStatus == VCLockStatus.LockedOther))
                {
                    lockMessage += " (" + Terminology.getlock + " By: " + assetStatus.owner + " )";
                }
            }
            if (assetStatus.fileStatus == VCFileStatus.Modified) lockMessage += "*";
            return lockMessage;
        }
    }
}
