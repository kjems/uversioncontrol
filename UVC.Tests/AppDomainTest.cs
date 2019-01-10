// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnitySVN@gmail.com>
/*
using System;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Threading;
using NUnit.Framework;
using VersionControl.Backend.SVN;

namespace VersionControl.UnitTests
{
    using Logging;
    [TestFixture]
    public class AppDomainTest
    {
        private const string localPathForTest = @"c:\develop\Game2.4";

        [SetUp]
        public void Setup()
        {
            D.writeErrorCallback += Logging;
            D.exceptionCallback += HandleExceptions;
        }

        [TearDown]
        public void TearDown()
        {
            D.writeErrorCallback -= Logging;
            D.exceptionCallback -= HandleExceptions;
        }

        static void HandleExceptions(Exception e)
        {
            throw e;
        }
        static void Logging(string message)
        {
            Console.WriteLine(message);
        }


        [Test]
        public void TestStatus()
        {
            var vcc = new VCCFilteredAssets(CreateSVNCommands());
            vcc.SetWorkingDirectory(localPathForTest);
            Directory.SetCurrentDirectory(localPathForTest);
            vcc.ProgressInformation += s => D.Log(s);
            vcc.Status(StatusLevel.Local, DetailLevel.Normal);
            vcc.ClearDatabase();
        }

        [Test]
        public void TestMemoryUsage()
        {
            var vcc = new VCCFilteredAssets(CreateSVNCommands());
            vcc.SetWorkingDirectory(localPathForTest);
            Directory.SetCurrentDirectory(localPathForTest);
            vcc.ProgressInformation += s => D.Log(s);
            vcc.Start();
            vcc.Status(StatusLevel.Remote, DetailLevel.Verbose);

            var assets = vcc.GetFilteredAssets(status =>
                {
                    VersionControlStatus metaStatus = status;
                    if(!status.assetPath.EndsWith(".meta"))
                    {
                        metaStatus = vcc.GetAssetStatus(status.assetPath + ".meta");
                    }
                    return (status.fileStatus != VCFileStatus.Normal || metaStatus.fileStatus != VCFileStatus.Normal);
                });

            Logging(assets.Select(s => s.assetPath.Compose()).Aggregate((a, b) => a + "\n" + b));

            Logging("Memory Used : " + GC.GetTotalMemory(true));
        }

        //[Test]
        public void TestAppDomainUnload()
        {
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
            vcc.RequestStatus(files, StatusLevel.Remote);
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
}*/