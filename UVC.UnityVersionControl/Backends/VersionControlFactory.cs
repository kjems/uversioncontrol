// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using UnityEngine;
using UnityEditor;
using System;
using UVC.Backend.Noop;
using UVC.Backend.SVN;

namespace UVC
{
    using Logging;
    public static class VersionControlFactory
    {
        public static event Action<IVersionControlCommands> VersionControlBackendChanged;

        private static void OnVersionControlBackendChanged(IVersionControlCommands vcc)
        {
            if (VersionControlBackendChanged != null)
            {
                VersionControlBackendChanged(vcc);
            }
        }
        public static IVersionControlCommands GetDefaultImplementation()
        {
            return new NoopCommands();
        }
        public static bool CreateVersionControlCommands(VCSettings.EVersionControlBackend backend)
        {
            string workDirectory = Application.dataPath.Remove(Application.dataPath.LastIndexOf("/Assets", StringComparison.Ordinal));
            bool noopSelected = backend == VCSettings.EVersionControlBackend.None;
            bool svnSelected = backend == VCSettings.EVersionControlBackend.Svn;
            /*P4_DISABLED bool p4Selected = backend == VCSettings.EVersionControlBackend.P4_Beta;*/
            IVersionControlCommands uvc = null;
            bool success = false;

            if(svnSelected && CreateVersionControl<SVNCommands>(() => new SVNCommands(), workDirectory, out uvc))
            {
                //D.Log(backend + " backend initialized successfully");
                OnVersionControlBackendChanged(uvc);
                success = true;
            }
            /*P4_DISABLED 
            else if (p4Selected && CreateVersionControl<P4Commands>(() => new P4Commands(), workDirectory, out uvc))
            {
                //D.Log(backend + " backend initialized successfully");
                OnVersionControlBackendChanged(uvc);
                success = true;
            }*/
            else if (noopSelected)
            {
                //D.Log(backend + " backend initialized successfully");
                OnVersionControlBackendChanged(GetDefaultImplementation());
                success = true;
            }

            GoogleAnalytics.LogUserEvent("Backend", $"{backend.ToString()}_{(success ? "success" : "failed")}");

            if (!success)
            {
                DebugLog.LogWarning(backend + " backend initialization failed!");
            }
            
            return success;
        }

        private static bool CreateVersionControl<T>(Func<T> factory, string workDirectory, out IVersionControlCommands uvc) where T : IVersionControlCommands
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            uvc = null;
            bool valid = false;
            try
            {
                uvc = AddDecorators(factory());
                uvc.SetWorkingDirectory(workDirectory);
                valid = uvc.HasValidLocalCopy();
            }
            catch (Exception e)
            {
                DebugLog.ThrowException(e);
            }
            finally
            {
                if (!valid && uvc != null) uvc.Dispose();
            }
            DebugLog.Log("CreateVersionControl took : " + watch.ElapsedMilliseconds + "ms");
            return valid;
        }

        private static bool PromptUserForBackend(VCSettings.EVersionControlBackend backend)
        {
            return UserDialog.DisplayDialog("Use " + backend + " ?", "The only valid version control found is '" + backend + "'. \nUse " + backend + " as version control?", "Yes", "No");
        }

        private static IVersionControlCommands AddDecorators(IVersionControlCommands vcc)
        {
            return new VCCAddMetaFiles(new VCCFilteredAssets(vcc));
        }
    }
}
