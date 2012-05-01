// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnitySVN@gmail.com>
using System.IO;
using NUnit.Framework;
using VersionControl.Backend.SVN;

namespace VersionControl.UnitTests
{
    [TestFixture]
    public class FunctionalTest
    {
        private const string urlToEmptyRepo = "svn://lillefyr.selfip.org:2345/unitysvn_root/FunctionalTest";
        private const string localPathForTest = "d:\\develop\\test";
        private IVersionControlCommands vcc;
        
        [SetUp]
        public void Init()
        {
            vcc = new VCCFilteredAssets(new SVNCommands());
            vcc.SetWorkingDirectory(localPathForTest);
            Directory.SetCurrentDirectory(localPathForTest);
            vcc.ProgressInformation += D.Log;
        }

        [Test]
        public void TestCheckout()
        {
            vcc.SetUserCredentials("kjems", "dingo");
            vcc.Checkout(urlToEmptyRepo, localPathForTest);
            Assert.IsTrue(Directory.Exists(localPathForTest));
        }

        [Test]
        public void AddAndRemoveFile()
        {
            const string fileA = "fileA.txt";
            var fs = File.Create(localPathForTest + "\\" + fileA, 10);
            fs.Close();
            
            vcc.Status(false, false);
            var status = vcc.GetAssetStatus(fileA);
            Assert.IsTrue(status.assetPath == fileA, "AssetPath mismatch: " + status.assetPath + "!=" + fileA);
            Assert.IsTrue(status.fileStatus == VCFileStatus.Unversioned, "Unversioned");
            
            vcc.Add(new[]{fileA});
            vcc.Status(false, false);
            status = vcc.GetAssetStatus(fileA);
            Assert.IsTrue(status.fileStatus == VCFileStatus.Added, "Added");
            
            vcc.Commit(new[] {fileA}, "AddFile Test 1/2");
            vcc.Status(false, false);
            status = vcc.GetAssetStatus(fileA);
            Assert.IsTrue(status.fileStatus == VCFileStatus.Normal, "Normal");

            var basePath = vcc.GetBasePath(fileA);
            Assert.That(File.Exists(basePath), Is.True, "Base path exist: " + basePath);
            
            vcc.Delete(new[] {fileA});
            vcc.Status(false, false);
            status = vcc.GetAssetStatus(fileA);
            Assert.IsTrue(status.fileStatus == VCFileStatus.Deleted, "Deleted");

            vcc.Commit(new[] { fileA }, "AddFile Test 2/2");
            vcc.Status(false, false);
            status = vcc.GetAssetStatus(fileA);
            Assert.That(status.Reflected, Is.False, "fileA is not present in repo");
            Assert.IsTrue(!File.Exists(localPathForTest + "\\" + fileA), "File removed again");
        }

        [Test]
        public void DirectoryActionsFile()
        {
            const string folderA = "FolderA";
            const string fileA = folderA + "\\" + "fileA.txt";
            const string fileB = folderA + "\\" + "fileB.txt";
            Directory.CreateDirectory(folderA);
            var fa = File.Create(localPathForTest + "\\" + fileA, 10);
            fa.Close();

            bool result = vcc.Status(false, false);
            Assert.That(result, Is.True, "Status #1");
            var folderAstatus = vcc.GetAssetStatus(folderA);
            Assert.That(folderAstatus.Reflected, Is.True, "The unversioned folder dirA is reflected");
            Assert.That(folderAstatus.assetPath, Is.EqualTo(folderA), "AssetPath mismatch");
            Assert.That(folderAstatus.fileStatus, Is.EqualTo(VCFileStatus.Unversioned), folderA);

            var fileAstatus = vcc.GetAssetStatus(fileA);
            Assert.That(fileAstatus.Reflected, Is.False, "No reflection on file in unversioned directory");


            result = vcc.Commit(new[] { folderA }, "Directory Test 1/3");
            Assert.That(result, Is.True, "Commit #1");
            result = vcc.Status(true, true);
            Assert.That(result, Is.True, "Status #2");
            fileAstatus = vcc.GetAssetStatus(fileA);
            Assert.That(fileAstatus.Reflected, Is.True, "fileA is reflected");
            Assert.That(fileAstatus.fileStatus, Is.EqualTo(VCFileStatus.Normal), "Commit fileA success");

            var fb = File.Create(localPathForTest + "\\" + fileB, 10);
            fb.Close();
            File.Delete(localPathForTest + "\\" + fileA);
            result = vcc.Status(true, true);
            Assert.That(result, Is.True, "Status #3");
            fileAstatus = vcc.GetAssetStatus(fileA);
            var fileBstatus = vcc.GetAssetStatus(fileB);
            Assert.That(fileAstatus.Reflected, Is.True, "fileA is reflected");
            Assert.That(fileAstatus.fileStatus, Is.EqualTo(VCFileStatus.Missing), "fileA");
            Assert.That(fileBstatus.Reflected, Is.True, "fileB is reflected");
            Assert.That(fileBstatus.fileStatus, Is.EqualTo(VCFileStatus.Unversioned), "fileB");

            result = vcc.Commit(new[] { folderA, fileA }, "Directory Test 2/3");
            Assert.That(result, Is.True, "Commit #2");
            result = vcc.Status(true, true);
            Assert.That(result, Is.True, "Status #4");

            Assert.That(!File.Exists(localPathForTest + "\\" + fileA), "Missing file deleted");
            fileAstatus = vcc.GetAssetStatus(fileA);
            Assert.That(fileAstatus.Reflected, Is.False, "fileA should be deleted and gone from repo");
            fileBstatus = vcc.GetAssetStatus(fileB);
            Assert.That(fileBstatus.Reflected, Is.True, "fileB is reflected after commited");
            Assert.That(fileBstatus.fileStatus, Is.EqualTo(VCFileStatus.Normal), "Unversioned file Added and Commited");

            result = vcc.Delete(new[] { folderA });
            Assert.That(result, Is.True, "Delete #1");
            
            result = vcc.Status(false, false);
            Assert.That(result, Is.True, "Status #5");
            fileBstatus = vcc.GetAssetStatus(fileB);
            Assert.That(fileBstatus.Reflected && fileBstatus.fileStatus == VCFileStatus.Deleted, "FileB Deleted");

            result = vcc.Commit(new[] { folderA }, "Directory Test 3/3");
            Assert.That(result, Is.True, "Commit #3");

            result = vcc.Update(new[] { folderA });
            Assert.That(result, Is.True, "Update #1");

            Assert.That(!Directory.Exists(folderA), "Directory removed again");
        }
    }
   
}