// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
#pragma warning disable CS4014


namespace UVC
{
    using Logging;
    using UserInterface;

    [InitializeOnLoad]
    internal static class VCExceptionHandler
    {
        static VCExceptionHandler()
        {
            DebugLog.writeErrorCallback += Debug.LogError;
            DebugLog.exceptionCallback += HandleException;
        }

        public static void HandleException(VCException e)
        {
            if (e.InnerException is AggregateException)
            {
                var aggregateException = e.InnerException as AggregateException;
                foreach (var exception in aggregateException.InnerExceptions)
                {
                    if (exception is VCException) HandleException((VCException)exception);
                    else HandleException(new VCException(exception.Message, exception.StackTrace, exception));
                }
            }
            else
            {
                OnNextUpdate.Do(() =>
                {
                    if (e is VCConnectionTimeoutException) HandleConnectionTimeOut(e as VCConnectionTimeoutException);
                    else if (e is VCLocalCopyLockedException) HandleLocalCopyLocked(e as VCLocalCopyLockedException);
                    else if (e is VCNewerVersionException) HandleNewerVersion(e as VCNewerVersionException);
                    else if (e is VCMixedRevisionException) HandleMixedRevision(e as VCMixedRevisionException);
                    else if (e is VCOutOfDate) HandleOutOfDate(e as VCOutOfDate);
                    else if (e is VCCriticalException) HandleCritical(e as VCCriticalException);
                    else if (e is VCMissingCredentialsException) HandleUserCredentials();
                    else if (e is VCMonoDebuggerAttachedException) HandleMonoDebuggerAttached(e as VCMonoDebuggerAttachedException);
                    else HandleBase(e);
                });
            }
        }

        private static void HandleConnectionTimeOut(VCConnectionTimeoutException e)
        {
            DebugLog.LogWarning(e.ErrorMessage);
            var dialog = CustomDialogs.CreateExceptionDialog("Connection Timeout", "Connection to the server timed out", e);
            dialog.AddButton("Turn UVC Off", () => { VCSettings.VCEnabled = false; dialog.Close(); });
            dialog.ShowUtility();
        }

        private static void HandleMonoDebuggerAttached(VCMonoDebuggerAttachedException e)
        {
            DebugLog.LogWarning(e.ErrorMessage);
            var dialog = CustomDialogs.CreateExceptionDialog("Mono Debugger Attached Bug", "When the Mono debugger is attached a conflict in calling command-line operations occur, so either detach Mono Debugger or turn off UVC", e);
            dialog.AddButton("Turn UVC Off", () => { VCSettings.VCEnabled = false; dialog.Close(); });
            dialog.ShowUtility();
        }

        private static void HandleLocalCopyLocked(VCLocalCopyLockedException e)
        {
            DebugLog.Log("Repository locked, issuing cleanup");
            VCCommands.Instance.CleanUp();
        }

        private static void HandleNewerVersion(VCNewerVersionException e)
        {
            var answer = UserDialog.DisplayDialog("Newer Version", "There is a newer version of the file on the server so you need to 'Update' first and then try again", "Update", "Cancel");
            if (answer)
            {
                VCCommands.Instance.UpdateTask();
            }
        }

        private static void HandleMixedRevision(VCMixedRevisionException e)
        {
            var answer = UserDialog.DisplayDialog("Mixed Revision", "Cannot merge into mixed-revision working copy, try updating first", "Update", "Cancel");
            if (answer)
            {
                VCCommands.Instance.UpdateTask();
            }
        }

        private static void HandleOutOfDate(VCOutOfDate e)
        {
            DebugLog.Log(e.ErrorMessage);
            var dialog = CustomDialogs.CreateExceptionDialog("Repository out of date", "The repository is out of date and you need to update first and then try again", e);
            dialog.AddButton("Update", () => { VCCommands.Instance.UpdateTask(); dialog.Close(); });
            dialog.ShowUtility();
        }

        private static void HandleCritical(VCCriticalException e)
        {
            Debug.LogException(e.InnerException != null ? e.InnerException : e);

            if (!string.IsNullOrEmpty(e.ErrorMessage))
                GoogleAnalytics.LogUserEvent("CriticalException", e.ErrorMessage);

            var dialog = CustomDialogs.CreateExceptionDialog("UVC Critical Exception", e);
            dialog.AddButton("Turn UVC Off", () => { VCSettings.VCEnabled = false; dialog.Close(); });
            dialog.ShowUtility();

            EditorUtility.ClearProgressBar();
        }

        private static void HandleUserCredentials()
        {
            UserInterface.VCCredentialsWindow.Init();
        }

        private static void HandleBase(VCException e)
        {
            Debug.LogException(e.InnerException != null ? e.InnerException : e);

            if(!string.IsNullOrEmpty(e.ErrorMessage))
                GoogleAnalytics.LogUserEvent("Exception", e.ErrorMessage);

            var dialog = CustomDialogs.CreateExceptionDialog("UVC Exception", e);
            if (VCSettings.BugReport)
            {
                dialog.AddButton("Report", () => ReportError(e));
            }
            dialog.ShowUtility();

            EditorUtility.ClearProgressBar();
        }

        private static void ReportError(VCException e)
        {
            if (VCSettings.BugReport)
            {
                string title = Environment.UserName + "@" + Environment.MachineName + " : (" + VCUtility.GetCurrentVersion() + "):\n" + e.ErrorMessage;
                string description = "\n" + e.ErrorDetails;

                var conflicts =
                    VCCommands.Instance.GetFilteredAssets(
                        svnStatus =>
                        svnStatus.treeConflictStatus != VCTreeConflictStatus.Normal || svnStatus.fileStatus == VCFileStatus.Conflicted ||
                        svnStatus.fileStatus == VCFileStatus.Obstructed);

                if (conflicts != null && conflicts.Any()) description += "\n\nSVN Conflicts:\n" + conflicts.Select(status => status.assetPath.Compose()).Aggregate((a, b) => a + "\n" + b);

                FogbugzUtilities.SubmitAutoBug(title, description);
            }
        }
    }
}
