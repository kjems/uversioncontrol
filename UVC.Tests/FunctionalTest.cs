// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnitySVN@gmail.com>
/* 
using System;
using System.IO;
using NUnit.Framework;
using VersionControl.Backend.SVN;

namespace VersionControl.UnitTests
{
    using Logging;
    [TestFixture]
    public class FunctionalTest
    {
        private const string urlToEmptyRepo = @"svn://192.168.9.175:2345/unitysvn_root/FunctionalTest";
        private const string workingDirectoryForSVNTests = @"c:\Develop\VCUnitTest";
        private IVersionControlCommands vcc;
        
        public FunctionalTest()
        {
            D.writeLogCallback += Console.WriteLine;
            D.exceptionCallback += e => { throw e; };
            D.writeErrorCallback += s => Console.WriteLine("ERROR: " + s);
        }

        [SetUp]
        public void Init()
        {
            if (Directory.Exists(workingDirectoryForSVNTests))
            {
                Directory.Delete(workingDirectoryForSVNTests, true);
            }
            Directory.CreateDirectory(workingDirectoryForSVNTests);
            
            vcc = new VCCFilteredAssets(new SVNCommands());
            vcc.SetWorkingDirectory(workingDirectoryForSVNTests);
            Directory.SetCurrentDirectory(workingDirectoryForSVNTests);
            vcc.ProgressInformation += s => D.Log(s);
            vcc.Start();
            vcc.SetUserCredentials("kjems", "dingo", true);
            vcc.Checkout(urlToEmptyRepo, workingDirectoryForSVNTests);
        }

        [TearDown] 
        public void Dispose()
        {
            vcc.Dispose();
            vcc = null;
        }

        [Test]
        public void TestCheckout()
        {
            Assert.IsTrue(Directory.Exists(workingDirectoryForSVNTests));
        }

        [Test]
        public void AddAndRemoveFile()
        {
            const string fileA = "fileA.txt";
            var fs = File.Create(workingDirectoryForSVNTests + "\\" + fileA, 10);
            fs.Close();
            
            vcc.Status(StatusLevel.Local, DetailLevel.Normal);
            var status = vcc.GetAssetStatus(fileA);
            Assert.IsTrue(status.assetPath.Compose() == fileA, "AssetPath mismatch: " + status.assetPath.Compose() + "!=" + fileA);
            Assert.IsTrue(status.fileStatus == VCFileStatus.Unversioned, "Unversioned");
            
            vcc.Add(new[]{fileA});
            vcc.Status(StatusLevel.Local, DetailLevel.Normal);
            status = vcc.GetAssetStatus(fileA);
            Assert.IsTrue(status.fileStatus == VCFileStatus.Added, "Added");
            
            vcc.Commit(new[] {fileA}, "AddFile Test 1/2");
            vcc.Status(StatusLevel.Local, DetailLevel.Normal);
            status = vcc.GetAssetStatus(fileA);
            Assert.IsTrue(status.fileStatus == VCFileStatus.Normal, "Normal");

            var basePath = vcc.GetBasePath(fileA);
            Assert.That(File.Exists(basePath), Is.True, "Base path exist: " + basePath);
            
            vcc.Delete(new[] {fileA}, OperationMode.Normal);
            vcc.Status(StatusLevel.Local, DetailLevel.Normal);
            status = vcc.GetAssetStatus(fileA);
            Assert.IsTrue(status.fileStatus == VCFileStatus.Deleted, "Deleted");

            vcc.Commit(new[] { fileA }, "AddFile Test 2/2");
            vcc.Status(StatusLevel.Local, DetailLevel.Normal);
            status = vcc.GetAssetStatus(fileA);
            Assert.That(status.Reflected, Is.False, "fileA is not present in repo");
            Assert.IsTrue(!File.Exists(workingDirectoryForSVNTests + "\\" + fileA), "File removed again");
        }

        [Test]
        public void DirectoryActionsFile()
        {
            const string folderA = "FolderA";
            const string fileA = folderA + "\\" + "fileA.txt";
            const string fileB = folderA + "\\" + "fileB.txt";
            Directory.CreateDirectory(folderA);
            var fa = File.Create(workingDirectoryForSVNTests + "\\" + fileA, 10);
            fa.Close();

            bool result = vcc.Status(StatusLevel.Local, DetailLevel.Normal);
            Assert.That(result, Is.True, "Status #1");
            var folderAstatus = vcc.GetAssetStatus(folderA);
            Assert.That(folderAstatus.reflectionLevel, Is.EqualTo(VCReflectionLevel.Local), "The unversioned folder dirA has reflection level Local");
            Assert.That(folderAstatus.Reflected, Is.True, "The unversioned folder dirA is reflected, reflectionLevel: " + folderAstatus.reflectionLevel);
            Assert.That(folderAstatus.assetPath.Compose(), Is.EqualTo(folderA), "AssetPath mismatch");
            Assert.That(folderAstatus.fileStatus, Is.EqualTo(VCFileStatus.Unversioned), folderA);

            var fileAstatus = vcc.GetAssetStatus(fileA);
            Assert.That(fileAstatus.Reflected, Is.False, "No reflection on file in unversioned directory");

            vcc.Update();
            result = vcc.Commit(new[] { folderA }, "Directory Test 1/3");
            Assert.That(result, Is.True, "Commit #1");
            result = vcc.Status(StatusLevel.Local, DetailLevel.Verbose);
            Assert.That(result, Is.True, "Status #2");
            fileAstatus = vcc.GetAssetStatus(fileA);
            Assert.That(fileAstatus.Reflected, Is.True, "fileA is reflected");
            Assert.That(fileAstatus.fileStatus, Is.EqualTo(VCFileStatus.Normal), "Commit fileA success");

            var fb = File.Create(workingDirectoryForSVNTests + "\\" + fileB, 10);
            fb.Close();
            File.Delete(workingDirectoryForSVNTests + "\\" + fileA);
            result = vcc.Status(StatusLevel.Local, DetailLevel.Verbose);
            Assert.That(result, Is.True, "Status #3");
            fileAstatus = vcc.GetAssetStatus(fileA);
            var fileBstatus = vcc.GetAssetStatus(fileB);
            Assert.That(fileAstatus.reflectionLevel, Is.EqualTo(VCReflectionLevel.Local), "The unversioned file fileA has reflection level Local");
            Assert.That(fileAstatus.Reflected, Is.True, "fileA is reflected");
            Assert.That(fileAstatus.fileStatus, Is.EqualTo(VCFileStatus.Missing), "fileA");
            Assert.That(fileBstatus.reflectionLevel, Is.EqualTo(VCReflectionLevel.Local), "The unversioned file fileB has reflection level Local");
            Assert.That(fileBstatus.Reflected, Is.True, "fileB is reflected");
            Assert.That(fileBstatus.fileStatus, Is.EqualTo(VCFileStatus.Unversioned), "fileB");

            vcc.Update();
            result = vcc.Commit(new[] { folderA, fileA }, "Directory Test 2/3");
            Assert.That(result, Is.True, "Commit #2");
            result = vcc.Status(StatusLevel.Local, DetailLevel.Verbose);
            Assert.That(result, Is.True, "Status #4");

            Assert.That(!File.Exists(workingDirectoryForSVNTests + "\\" + fileA), "Missing file deleted");
            fileAstatus = vcc.GetAssetStatus(fileA);
            Assert.That(fileAstatus.Reflected, Is.False, "fileA should be deleted and gone from repo");
            fileBstatus = vcc.GetAssetStatus(fileB);
            Assert.That(fileBstatus.Reflected, Is.True, "fileB is reflected after commited");
            Assert.That(fileBstatus.fileStatus, Is.EqualTo(VCFileStatus.Normal), "Unversioned file Added and Commited");

            result = vcc.Delete(new[] { folderA }, OperationMode.Normal);
            Assert.That(result, Is.True, "Delete #1");

            result = vcc.Status(StatusLevel.Local, DetailLevel.Verbose);
            Assert.That(result, Is.True, "Status #5");
            fileBstatus = vcc.GetAssetStatus(fileB);
            Assert.That(fileBstatus.Reflected && fileBstatus.fileStatus == VCFileStatus.Deleted, "FileB Deleted");

            vcc.Update();
            result = vcc.Commit(new[] { folderA }, "Directory Test 3/3");
            Assert.That(result, Is.True, "Commit #3");

            result = vcc.Update(new[] { folderA });
            Assert.That(result, Is.True, "Update #1");

            Assert.That(!Directory.Exists(folderA), "Directory removed again");
        }
    }
   
}*/