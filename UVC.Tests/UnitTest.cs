// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnitySVN@gmail.com>
/*
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommandLineExecution;
using NUnit.Framework;
using System.Threading;
using VersionControl.Backend.SVN;

namespace VersionControl.UnitTests
{
    using Logging;
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    [TestFixture]
    public class TestCommandLine
    {
        private CommandLine commandLine;
        private const string workingDirectoryForSVNTests = @"c:\develop\VCUnitTest";

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
            Assert.AreEqual(echoMsg, commandLineOutput.OutputStr.TrimEnd(), "Echo matches");
        }
    }

    [TestFixture]
    public class TestSVNCommands
    {
        private CommandLine commandLine;
        private const string workingDirectoryForSVNTests = @"c:\develop\VCUnitTest";

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
            Assert.AreEqual("Type 'svn help' for usage.", commandLineOutput.ErrorStr.TrimEnd(), "commandLineOutput.errorStr");
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

    [TestFixture]
    public class TestComposedString
    {
        const string str1 = "Assets/_Tests/Kjems/Scripts/PhysXForcePush1.cs";
        const string str2 = "Assets/_Tests/Kjems/Scripts/PhysXForcePush2.cs";
        const string str3 = "Assets/_Tests/Kjems/Scripts/PhysXForcePush2.cs";
        const string str4 = "Assets/_Tests/Kjems/Test_Anim/Huddle@run.fbx";
        const string str5 = "Assets/_Tests/Kjems/Prefabs/���@%#���.prefab";
        const string meta = ".meta";
        const string metaDot = ".meta.";
        const string empty = "";
        const string str3meta = "Assets/_Tests/Kjems/Scripts/PhysXForcePush2.cs.meta";

        static readonly ComposedString cstr1 = new ComposedString(str1);

        [SetUp]
        public void Init()
        {
            D.writeLogCallback += System.Console.WriteLine;
        }

        [Test]
        public void TestComposeAndDecompose()
        {
            ComposedString cstr2 = new ComposedString(str2);
            ComposedString cstr3 = new ComposedString(str3);
            ComposedString cstr4 = new ComposedString(str4);
            ComposedString cstr5 = new ComposedString(str5);
            var cstr3meta = cstr3 + meta;
            Assert.AreEqual(str1, cstr1.Compose(), "compose/decompose mismatch");
            Assert.AreEqual(str5, cstr5.Compose(), "compose/decompose mismatch with Unicode characters");
            Assert.AreEqual(cstr2 , cstr3, "equal ComposedString");
            Assert.AreEqual(str3meta, cstr3meta.Compose(), "using operator + with string");
            Assert.True(cstr3meta.EndsWith(meta), "Endwith and implicit string conversion");
            Assert.False(cstr3meta.EndsWith(empty), "Endwith empty");
            Assert.False(cstr3meta.EndsWith(metaDot), "Endwith metaDot");
            Assert.True(cstr4.EndsWith("@run.fbx"), "Endwith @run.fbx");
            Assert.AreEqual(cstr3, cstr3meta.TrimEnd(meta), "Trim End");
            Assert.AreEqual(cstr3meta, cstr3 + meta, "Trim End does not modify original");
        }

        [Test]
        public void TestSequential()
        {
            const int testSize = 100000;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < testSize; ++i)
            {
                TestComposeAndDecompose();
            }
            D.Log("Time per test " + ((float)sw.ElapsedMilliseconds / testSize).ToString("0.0000ms") + ", total : " + sw.ElapsedMilliseconds + "ms");
        }

        [Test]
        public void TestMultiThreading()
        {
            const int testSize = 100000;
            Task[] tasks = new Task[testSize];
            for (int i = 0; i < testSize; ++i)
            {
                tasks[i] = new Task(TestComposeAndDecompose);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < testSize; ++i)
            {
                tasks[i].Start();
            }
            Task.WaitAll(tasks);
            D.Log("Time per test " + ((float)sw.ElapsedMilliseconds / testSize).ToString("0.0000ms") + ", total : " + sw.ElapsedMilliseconds + "ms");
        }
    }
}*/