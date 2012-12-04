// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnitySVN@gmail.com>

using System.IO;
using CommandLineExecution;
using NUnit.Framework;
using VersionControl.Backend.SVN;

namespace VersionControl.UnitTests
{
    [TestFixture]
    public class TestCommandLine
    {
        private CommandLine commandLine;
        private const string workingDirectoryForSVNTests = @"d:\develop\VCUnitTest";

        [SetUp]
        public void Init()
        {
        }

        [Test]
        public void TestUnitTest()
        {
            Assert.AreEqual("hello", "hello", "hello matches");
        }

        [Test]
        public void TestEcho()
        {
            const string echoMsg = "Echo back what is given as argument";
            commandLine = new CommandLine("cmd", "/C echo " + echoMsg, workingDirectoryForSVNTests);
            var commandLineOutput = commandLine.Execute();
            Assert.IsFalse(commandLineOutput.Failed, "Should not fail echo");
            Assert.AreEqual(echoMsg, commandLineOutput.OutputStr, "Echo matches");
        }
    }

    [TestFixture]
    public class TestSVNCommands
    {
        private CommandLine commandLine;
        private const string workingDirectoryForSVNTests = @"d:\develop\VCUnitTest";

        [SetUp]
        public void Init()
        {
        }

        [Test]
        public void TestErrorHandling()
        {
            commandLine = new CommandLine("svn", "", workingDirectoryForSVNTests);
            var commandLineOutput = commandLine.Execute();
            Assert.AreEqual(1, commandLineOutput.Exitcode, "commandLineOutput.exitcode");
            Assert.AreEqual("Type 'svn help' for usage.", commandLineOutput.ErrorStr, "commandLineOutput.errorStr");
            Assert.IsTrue(commandLineOutput.Failed, "commandLineOut.failed");
        }

        [Test]
        public void TestRepository()
        {
            commandLine = new CommandLine("svn", "status --xml -v", workingDirectoryForSVNTests);
            var commandLineOutput = commandLine.Execute();
            Assert.Greater(commandLineOutput.OutputStr.Length, 0, "empty output");
            D.Log(commandLineOutput.OutputStr);
            var statusDatabase = SVNStatusXMLParser.SVNParseStatusXML(commandLineOutput.OutputStr);
            Assert.IsNotEmpty(statusDatabase.Keys, "statusDatabase not empty");
        }
    }

    [TestFixture]
    public class TestVCCFilteredAssets
    {
        DataCarrier carrier;
        DecoratorLoopback loopback;
        VCCFilteredAssets filtered;
        readonly StatusDatabase db = new StatusDatabase();

        [SetUp]
        public void Init()
        {
            db.Add(new VersionControlStatus { reflectionLevel = VCReflectionLevel.Local, fileStatus = VCFileStatus.Missing      , assetPath = "missing", });
            db.Add(new VersionControlStatus { reflectionLevel = VCReflectionLevel.Local, fileStatus = VCFileStatus.Unversioned  , assetPath = "unversioned"});
            db.Add(new VersionControlStatus { reflectionLevel = VCReflectionLevel.Local, fileStatus = VCFileStatus.Normal       , assetPath = "normal"});
            db.Add(new VersionControlStatus { reflectionLevel = VCReflectionLevel.Local, fileStatus = VCFileStatus.Deleted      , assetPath = "deleted"});
            db.Add(new VersionControlStatus { reflectionLevel = VCReflectionLevel.Local, fileStatus = VCFileStatus.Added        , assetPath = "added" });
            carrier = new DataCarrier();
            loopback = new DecoratorLoopback(carrier, db);
            filtered = new VCCFilteredAssets(loopback);
        }

        [Test]
        public void TestAdd()
        {
            var inAssets = new[] { "missing", "unversioned", "normal", "deleted", "added" };
            bool result = filtered.Add(inAssets);
            Assert.IsTrue(result, "Add completed successfully");
            Assert.IsTrue(!carrier.assets.Contains("missing"), "missing files are not added");
            Assert.IsTrue(carrier.assets.Contains("unversioned"), "unversioned files are added");
            Assert.IsTrue(!carrier.assets.Contains("normal"), "normal files are not added");
            Assert.IsTrue(!carrier.assets.Contains("deleted"), "deleted files are not added");
            Assert.IsTrue(!carrier.assets.Contains("added"), "added files are not added");
            
        }

        [Test]
        public void TestCommit()
        {
            var inAssets = new[] { "missing", "unversioned", "normal", "deleted", "added" };
            bool result = filtered.Commit(inAssets);
            Assert.IsTrue(result, "Commit completed successfully");
        }
    }
}