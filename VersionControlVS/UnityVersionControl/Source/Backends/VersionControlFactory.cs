// Copyright (c) <2012> <Playdead>
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
        public static IVersionControlCommands CreateVersionControlCommands(string workDirectory)
        {
            string cliEnding = (Application.platform == RuntimePlatform.OSXEditor) ? Environment.NewLine : "";          
            bool svnSelected = VCSettings.VersionControlBackend == VCSettings.EVersionControlBackend.Svn;
            bool p4Selected = VCSettings.VersionControlBackend == VCSettings.EVersionControlBackend.Perforce;
            IVersionControlCommands uvc = null;

            if(svnSelected && CreateVersionControl<SVNCommands>(() => new SVNCommands(cliEnding), workDirectory, out uvc))
            {
                return uvc; 
            }
            else if (p4Selected && CreateVersionControl<P4Commands>(() => new P4Commands(cliEnding), workDirectory, out uvc))
            {
                return uvc;
            }

            D.LogWarning("No valid version control local copy found, so version control is inactive");
            return new NoopCommands();
        }

        private static bool CreateVersionControl<T>(Func<T> factory, string workDirectory, out IVersionControlCommands uvc) where T : IVersionControlCommands
        {
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
                D.LogWarning(e.Message);
            }
            finally
            {
                if (!valid && uvc != null) uvc.Dispose();
            }
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
