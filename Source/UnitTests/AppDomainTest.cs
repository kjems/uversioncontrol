// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnitySVN@gmail.com>

using System;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Threading;
using NUnit.Framework;
using VersionControl.Backend.SVN;

namespace VersionControl.UnitTests
{
    [TestFixture]
    public class AppDomainTest
    {
        private const string localPathForTest = @"d:\develop\Game2.4";

        [Test]
        public void TestStatus()
        {
            var vcc = new VCCFilteredAssets(CreateAppDomainSVNCommands());
            vcc.SetWorkingDirectory(localPathForTest);
            Directory.SetCurrentDirectory(localPathForTest);
            vcc.ProgressInformation += s => D.Log(s);
            vcc.Status(StatusLevel.Local, DetailLevel.Normal);
            vcc.ClearDatabase();
        }

        [Test]
        public void TestAppDomainUnload()
        {
            //D.writeLogCallback += Console.WriteLine;
            D.writeErrorCallback += s => Console.WriteLine("ERROR: " + s);
            D.exceptionCallback += exception => { throw exception; };

            for (int i = 0; i < 5; i++)
            {
                var vcc = SetupAppDomain();
                vcc.Start();
                vcc.Status(StatusLevel.Local, DetailLevel.Normal);
                QueueWork(vcc);
                Thread.Sleep(2000);
                UnloadAppdomain();
                //Thread.Sleep(100);
                Console.WriteLine(GC.GetTotalMemory(true));
            }
        }

        private static IVersionControlCommands SetupAppDomain()
        {
            var svnCommands = (SVNCommands)CreateAppDomainSVNCommands();
            svnCommands.SetWorkingDirectory(localPathForTest);
            Directory.SetCurrentDirectory(localPathForTest);
            svnCommands.ProgressInformation += s => D.Log(s);
            svnCommands.StatusCompleted += () => Console.Write("#");

            return new VCCFilteredAssets(svnCommands);
        }

        private void QueueWork(IVersionControlCommands vcc)
        {
            var files = Directory.GetFiles(localPathForTest, "*.asset", SearchOption.AllDirectories).Select(s => s.Replace("\\", "/"));
            vcc.SetStatusRequestRule(files, StatusLevel.Remote);
            vcc.RequestStatus(files);
        }

        private static AppDomain svnDomain = null;

        internal static IVersionControlCommands CreateSVNCommands()
        {
            return new SVNCommands();
        }

        internal static IVersionControlCommands CreateAppDomainSVNCommands()
        {
            svnDomain = BuildChildDomain(AppDomain.CurrentDomain, "SVNDomain");
            var svnCommands = (IVersionControlCommands)svnDomain.CreateInstanceAndUnwrap("SVNBackend", "VersionControl.Backend.SVN.SVNCommands");
            return svnCommands;
        }

        internal static void UnloadAppdomain()
        {
            if(svnDomain != null)
            {
                AppDomain.Unload(svnDomain);
            }
        }

        internal static AppDomain BuildChildDomain(AppDomain parentDomain, string name)
        {
            var evidence = new Evidence(parentDomain.Evidence);
            var setup = parentDomain.SetupInformation;
            return AppDomain.CreateDomain(name, evidence, setup);
        }
    }
}