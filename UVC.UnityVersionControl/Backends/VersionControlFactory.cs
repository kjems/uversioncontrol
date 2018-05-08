// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using VersionControl.Backend.Noop;
using VersionControl.Backend.SVN;
using VersionControl.Backend.P4;
using UnityEngine;
using UnityEditor;
using System;

namespace VersionControl
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

            GoogleAnalytics.LogUserEvent("Backend", string.Format("{0}_{1}", backend.ToString(), (success ? "success" : "failed")));

            if (!success)
            {
                D.LogWarning(backend + " backend initialization failed!");
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
                D.ThrowException(e);
            }
            finally
            {
                if (!valid && uvc != null) uvc.Dispose();
            }
            D.Log("CreateVersionControl took : " + watch.ElapsedMilliseconds + "ms");
            return valid;
        }

        private static bool PromptUserForBackend(VCSettings.EVersionControlBackend backend)
        {
            return EditorUtility.DisplayDialog("Use " + backend + " ?", "The only valid version control found is '" + backend + "'. \nUse " + backend + " as version control?", "Yes", "No");
        }

        private static IVersionControlCommands AddDecorators(IVersionControlCommands vcc)
        {
            return new VCCFilteredAssets(new VCCAddMetaFiles(vcc));
        }
    }
}
