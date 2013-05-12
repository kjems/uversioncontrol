// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using VersionControl.Backend.SVN;
using VersionControl.Backend.P4;

namespace VersionControl
{
    public static class VersionControlFactory
    {
        public static IVersionControlCommands CreateVersionControlCommands()
        {
            //return CreateP4Commands();
            return CreateSVNCommands();
        }

        private static IVersionControlCommands CreateSVNCommands()
        {
            return new VCCFilteredAssets(new VCCAddMetaFiles(new SVNCommands()));
        }

        private static IVersionControlCommands CreateP4Commands()
        {
            return new VCCFilteredAssets(new VCCAddMetaFiles(new P4Commands()));
        }

        /*private static IVersionControlCommands CreateAppDomainSVNCommands()
        {
            var svnDomain = BuildChildDomain(AppDomain.CurrentDomain, "SVNDomain");
            var svnCommands = (IVersionControlCommands)svnDomain.CreateInstanceAndUnwrap("SVNBackend", "VersionControl.Backend.SVN.SVNCommands");
            return new VCCFilteredAssets(svnCommands);
        }

        private static AppDomain BuildChildDomain(AppDomain parentDomain, string name)
        {
            var evidence = new Evidence(parentDomain.Evidence);
            var setup = parentDomain.SetupInformation;
            return AppDomain.CreateDomain(name, evidence, setup);
        }*/
    }
}
