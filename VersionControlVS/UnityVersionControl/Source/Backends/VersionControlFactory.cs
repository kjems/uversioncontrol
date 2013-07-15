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
    public static class VersionControlFactory
    {
        public static IVersionControlCommands CreateVersionControlCommands(string workDirectory)
        {
			string cliEnding = "";
			if ( Application.platform == RuntimePlatform.OSXEditor ) {
				cliEnding = Environment.NewLine;
			}
			
            bool svnValid = true;
            bool p4Valid = true;
			
			IVersionControlCommands p4Commands = null;
			IVersionControlCommands svnCommands = null;
			
			try 
			{
				p4Commands = AddDecorators(new P4Commands(cliEnding));
    	        p4Commands.SetWorkingDirectory(workDirectory);
				p4Valid = p4Commands.HasValidLocalCopy();
			}
			catch (Exception)
			{
				p4Valid = false;
			}
			
			try
			{
	            svnCommands = AddDecorators(new SVNCommands(cliEnding));
    	        svnCommands.SetWorkingDirectory(workDirectory);
	            svnValid = svnCommands.HasValidLocalCopy();
			}
			catch (Exception)
			{
				svnValid = false;
			}
            
            bool svnSelected = VCSettings.VersionControlBackend == VCSettings.EVersionControlBackend.Svn;
            bool p4Selected = VCSettings.VersionControlBackend == VCSettings.EVersionControlBackend.Perforce;

            if (svnValid && !p4Valid)
            {
                if (svnSelected || PromptUserForBackend(VCSettings.EVersionControlBackend.Svn))
                {
                    VCSettings.VersionControlBackend = VCSettings.EVersionControlBackend.Svn;
                    p4Commands.Dispose();
                    return svnCommands;
                }
            }
            if (!svnValid && p4Valid)
            {
                if (p4Selected || PromptUserForBackend(VCSettings.EVersionControlBackend.Perforce))
                {
                    VCSettings.VersionControlBackend = VCSettings.EVersionControlBackend.Perforce;
                    svnCommands.Dispose();
                    return p4Commands;
                }
            }
            if (svnValid && p4Valid)
            {
                if (VCSettings.VersionControlBackend == VCSettings.EVersionControlBackend.Svn)
                {
                    p4Commands.Dispose();
                    return svnCommands;
                }
                if (VCSettings.VersionControlBackend == VCSettings.EVersionControlBackend.Perforce)
                {
                    svnCommands.Dispose();
                    return p4Commands;
                }
            }
            D.LogWarning("No valid version control local copy found, so version control is inactive");
            return new NoopCommands();
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
