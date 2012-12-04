// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;


namespace VersionControl
{
    [InitializeOnLoad]
    internal static class VCExceptionHandler
    {
        static VCExceptionHandler()
        {
            D.writeErrorCallback += Debug.LogError;
            D.exceptionCallback += HandleException;
        }

        public static void HandleException(VCException e)
        {
            OnNextUpdate.Do(() =>
            {
                if (e is VCConnectionTimeoutException) HandleConnectionTimeOut(e as VCConnectionTimeoutException);
                else if (e is VCLocalCopyLockedException) HandleLocalCopyLocked(e as VCLocalCopyLockedException);
                else if (e is VCNewerVersionException) HandleNewerVersion(e as VCNewerVersionException);
                else if (e is VCOutOfDate) HandleOutOfDate(e as VCOutOfDate);
                else if (e is VCCriticalException) HandleCritical(e as VCCriticalException);
                else HandleBase(e);
            });
        }
        
        private static void HandleConnectionTimeOut(VCConnectionTimeoutException e)
        {
            D.LogWarning(e.ErrorMessage);
            if (EditorUtility.DisplayDialog("Connection Timeout", "Connection to the server timed out.\n\nTurn Off Version Control?", "Yes", "No"))
            {
                VCSettings.VCEnabled = false;
            }
        }

        private static void HandleLocalCopyLocked(VCLocalCopyLockedException e)
        {
            D.Log("Repository locked, issuing cleanup");
            VCCommands.Instance.CleanUp();
        }

        private static void HandleNewerVersion(VCNewerVersionException e)
        {
            D.Log(e.ErrorMessage);
            if (EditorUtility.DisplayDialog("Newer Version", "There is a newer version of the file and need to update first and then try again.\n\nUpdate Version Control?", "Yes", "No"))
            {
                VCCommands.Instance.UpdateTask();
            }
        }

        private static void HandleOutOfDate(VCOutOfDate e)
        {
            D.Log(e.ErrorMessage);
            if (EditorUtility.DisplayDialog("Repository out of date", "The repository is out of date and you need to update first and then try again.\n\nUpdate Version Control?", "Yes", "No"))
            {
                VCCommands.Instance.UpdateTask();
            }
        }

        private static void HandleCritical(VCCriticalException e)
        {
            D.LogError("Exception catched! : " + e.ErrorDetails + "\n\n" + e.ErrorMessage);
            if (EditorUtility.DisplayDialog("Version Control Exception", e.ErrorMessage + "\n\nTurn Off Version Control?", "Yes", "No"))
            {
                VCSettings.VCEnabled = false;
            }
        }

        private static void HandleBase(VCException e)
        {
            D.LogError("Exception catched! : " + e.ErrorDetails + "\n\n" + e.ErrorMessage);
            if (VCSettings.BugReport)
            {
                var report = EditorUtility.DisplayDialog("Version Control Exception", e.ErrorMessage, "Report", "Close");
                if (report) ReportError(e);
            }
            else
            {
                EditorUtility.DisplayDialog("Version Control Exception", e.ErrorMessage, "OK");
            }
        }
        
        private static void ReportError(VCException e)
        {
            if (VCSettings.BugReport)
            {
                string title = Environment.UserName + "@" + Environment.MachineName + " : (" + VCUtility.GetCurrentVersion() + "):\n" + e.ErrorMessage;
                string description = "\n" + e.ErrorDetails;

                if (!string.IsNullOrEmpty(Application.loadedLevelName)) description += "\n\nScene Name: " + Application.loadedLevelName;
                var conflicts =
                    VCCommands.Instance.GetFilteredAssets(
                        svnStatus =>
                        svnStatus.treeConflictStatus != VCTreeConflictStatus.Normal || svnStatus.fileStatus == VCFileStatus.Conflicted ||
                        svnStatus.fileStatus == VCFileStatus.Obstructed);

                if (conflicts != null && conflicts.Any()) description += "\n\nSVN Conflicts:\n" + conflicts.Select(status => status.assetPath).Aggregate((a, b) => a + "\n" + b);

                FogbugzUtilities.SubmitAutoBug(title, description);
            }
        }
    }
}
