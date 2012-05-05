// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Security.Policy;
using VersionControl.Backend.SVN;

namespace VersionControl
{
    public static class VersionControlFactory
    {
        public static IVersionControlCommands CreateVersionControlCommands()
        {
            return CreateSVNCommands();
        }

        private static IVersionControlCommands CreateSVNCommands()
        {
            return new VCCFilteredAssets(new SVNCommands());
        }

        private static IVersionControlCommands CreateAppDomainSVNCommands()
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
        }
    }
}
