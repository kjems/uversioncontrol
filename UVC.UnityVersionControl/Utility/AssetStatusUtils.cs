// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using System.Collections.Generic;
using UnityEngine;
#pragma warning disable 414

namespace UVC.UserInterface
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    public static class AssetStatusUtils
    {
        private static readonly Color orange = new Color(0.85f, 0.45f, 0.05f);
        private static readonly Color pastelRed = new Color(0.85f, 0.4f, 0.4f);
        private static readonly Color pastelBlue = new Color(0.3f, 0.55f, 0.85f);
        private static readonly Color lightgrey = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color pink = new Color(1f, 0.6f, 1f);
        private static readonly Color black = Color.black;
        private static readonly Color border = new Color(0.1f, 0.1f, 0.1f);

        private static readonly Color addedColor = new Color(0.2f, 0.2f, 0.9f);
        private static readonly Color conflictedColor = Color.red;
        private static readonly Color missingColor = new Color(1.0f, 0.2f, 1.0f);
        private static readonly Color normalColor = new Color(0.9f, 0.9f, 0.9f, 0.05f);
        private static readonly Color lockedColor = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color lockedOtherColor = new Color(0.9f, 0.3f, 0.3f);
        private static readonly Color modifiedColor = pastelBlue;
        private static readonly Color modifiedNoLockColor = orange;
        private static readonly Color modifiedPropertyColor = new Color(0.29f, 1f, 0.94f);
        private static readonly Color localEditColor = new Color(1.0f, 1.0f, 0.2f);
        private static readonly Color unversionedColor = new Color(0.4f, 0.4f, 0.3f);
        private static readonly Color remoteModifiedColor = new Color(1.0f, 0.9f, 0.9f, 0.4f);
        private static readonly Color pendingColor = new Color(0.9f, 0.9f, 0.6f, 0.0f);
        private static readonly Color ignoreColor = new Color(0.7f, 0.7f, 0.7f, 0.1f);
        private static readonly Color deletedColor = new Color(0.3f, 0.3f, 0.3f, 0.1f);

        public static Color GetStatusColor(VersionControlStatus assetStatus, bool includeLockStatus)
        {
            if (assetStatus.treeConflictStatus == VCTreeConflictStatus.TreeConflict) return conflictedColor;
            if (assetStatus.fileStatus == VCFileStatus.Conflicted) return conflictedColor;
            if (assetStatus.fileStatus == VCFileStatus.Missing) return missingColor;
            if (assetStatus.fileStatus == VCFileStatus.Ignored) return ignoreColor;
            if (assetStatus.localOnly) return orange;
            if (assetStatus.LocalEditAllowed()) return localEditColor;
            if (assetStatus.ModifiedWithoutLock()) return modifiedNoLockColor;
            if (assetStatus.fileStatus == VCFileStatus.Deleted) return deletedColor;
            if (assetStatus.fileStatus == VCFileStatus.Added) return addedColor;

            if (includeLockStatus)
            {
                if (assetStatus.lockStatus == VCLockStatus.LockedHere) return lockedColor;
                if (assetStatus.lockStatus == VCLockStatus.LockedOther) return lockedOtherColor;
            }

            if (assetStatus.fileStatus == VCFileStatus.Modified) return modifiedColor;
            if (assetStatus.property == VCProperty.Modified) return modifiedPropertyColor;
            if (assetStatus.reflectionLevel == VCReflectionLevel.Pending) return pendingColor;
            if (assetStatus.fileStatus == VCFileStatus.Unversioned) return unversionedColor;
            if (assetStatus.remoteStatus == VCRemoteFileStatus.Modified) return remoteModifiedColor;
            if (assetStatus.fileStatus == VCFileStatus.Normal) return normalColor;

            return pink;
        }

        public static string GetStatusText(VersionControlStatus assetStatus)
        {
            if (assetStatus.reflectionLevel == VCReflectionLevel.Pending) return "Pending";
            if (assetStatus.fileStatus == VCFileStatus.Conflicted) return "Conflicted";
            if (assetStatus.fileStatus == VCFileStatus.Deleted) return "Deleted";
            if (assetStatus.lockStatus == VCLockStatus.LockedHere) return Terminology.getlock + (assetStatus.fileStatus == VCFileStatus.Modified?"*":"");
            if (assetStatus.localOnly) return "Local Only!";
            if (assetStatus.LocalEditAllowed()) return Terminology.allowLocalEdit + (assetStatus.fileStatus == VCFileStatus.Modified ? "*" : "");
            if (assetStatus.ModifiedWithoutLock()) return "Modified!";
            if (assetStatus.lockStatus == VCLockStatus.LockedOther) return Terminology.lockedBy + "'" + assetStatus.owner + "'";
            if (assetStatus.fileStatus == VCFileStatus.Modified) return "Modified";
            if (assetStatus.fileStatus == VCFileStatus.Unversioned) return Terminology.unversioned;
            if (assetStatus.fileStatus == VCFileStatus.Added) return "Added";
            if (assetStatus.property == VCProperty.Modified) return "[Merge Info]";
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
            if (assetStatus.LocalEditAllowed())
            {
                lockMessage = Terminology.allowLocalEdit;
                if ((assetStatus.lockStatus == VCLockStatus.LockedOther))
                {
                    lockMessage += " (" + Terminology.getlock + " By: " + assetStatus.owner + " )";
                }
            }
            if (assetStatus.fileStatus == VCFileStatus.Modified) lockMessage += "*";
            return lockMessage;
        }

        public static VersionControlStatus GetDominantStatus(IReadOnlyList<VersionControlStatus> statuses)
        {
            VersionControlStatus dominantStatus = new VersionControlStatus();
            foreach (var status in statuses)
            {
                if (status.lockStatus > dominantStatus.lockStatus)
                {
                    dominantStatus = status;
                    continue;
                }
                if (status.fileStatus > dominantStatus.fileStatus)
                {
                    dominantStatus = status;
                    continue;
                }
                if (status.allowLocalEdit && !dominantStatus.allowLocalEdit)
                {
                    dominantStatus = status;
                    continue;
                }
            }
            return dominantStatus;
        }
    }
}
