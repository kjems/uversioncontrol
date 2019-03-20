// Copyright (c) <2013> <E-Line Media, LLC>
using System;
using System.Collections.Generic;
using System.Text;

namespace UVC.Backend.P4
{
    using Logging;
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    #region EnumMaps
    internal static class P4ToVersionControlStatusMap
    {
        internal static readonly Dictionary<string, VCFileStatus> fileStatusMap = new Dictionary<string, VCFileStatus>
        {
            {"normal", VCFileStatus.Normal},
            {"added", VCFileStatus.Added},
            {"conflicted", VCFileStatus.Conflicted},
            {"deleted", VCFileStatus.Deleted},
            {"ignored", VCFileStatus.Ignored},
            {"modified", VCFileStatus.Modified},
            {"replaced", VCFileStatus.Replaced},
            {"unversioned", VCFileStatus.Unversioned},
            {"missing", VCFileStatus.Missing},
            {"external", VCFileStatus.External},
            {"incomplete", VCFileStatus.Incomplete},
            {"merged", VCFileStatus.Merged},
            {"obstructed", VCFileStatus.Obstructed},
            {"none", VCFileStatus.None},
        };

        internal static readonly Dictionary<string, VCProperty> propertyMap = new Dictionary<string, VCProperty>
        {
            {"none", VCProperty.None},
            {"normal", VCProperty.Normal},
            {"conflicted", VCProperty.Conflicted},
            {"modified", VCProperty.Modified},
        };
    }
    #endregion


    public static class P4StatusParser
    {
        public static string DecodeFromUtf8(string utf8String)
        {
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(utf8String);
            byte[] unicodeBytes = Encoding.Convert(Encoding.UTF8, Encoding.Unicode, utf8Bytes);
            return Encoding.Unicode.GetString(unicodeBytes);
        }

        public static StatusDatabase P4ParseStatus(string p4Status, String username)
        {
            // p4 status command returns status of unversioned files
            var statusDatabase = new StatusDatabase();
            var lines = p4Status.Split(new Char[] { '\r', '\n' });
            foreach (String line in lines)
            {
                if (line.IndexOf(" - reconcile") == -1) continue;    // sometimes output may contain blank lines...
                var status = ParseStatusLine(line, username);
                string assetPath = line.Substring(0, line.IndexOf(" - reconcile")).Replace('\\', '/').Trim().Replace(P4Util.Instance.Vars.unixWorkingDirectory + "/", "");
                status.assetPath = new ComposedString(assetPath);
                statusDatabase[status.assetPath] = status;
            }

            return statusDatabase;
        }

        private static VersionControlStatus ParseStatusLine(String line, String username)
        {
            // typical p4 status return lines:
            // "path\to\readme.txt - reconcile to add //depot/path/to/readme#1"
            // "path\to\readme.txt - reconcile to edit //depot/path/to/readme#2"
            // "path\to\readme.txt - reconcile to delete //depot/path/to/readme#2"
            var versionControlStatus = new VersionControlStatus();

            versionControlStatus.revision = versionControlStatus.lastModifiedRevision = Int32.Parse(line.Substring(line.LastIndexOf("#") + 1));
            versionControlStatus.repositoryStatus = VCRepositoryStatus.NotLocked;
            //            versionControlStatus.treeConflictStatus = VCTreeConflictStatus.Normal;
            versionControlStatus.user = "";
            if (line.Contains("reconcile to add"))
            {
                // file is unversioned
                versionControlStatus.remoteStatus = VCRemoteFileStatus.None;
                versionControlStatus.fileStatus = VCFileStatus.Unversioned;
                versionControlStatus.user = username;
            }
            else if (line.Contains("reconcile to edit"))
            {
                // file is edited locally, but not checked out - bad user!
                //                versionControlStatus.remoteStatus = VCRemoteFileStatus.Modified;
                versionControlStatus.fileStatus = VCFileStatus.Modified;
                //                versionControlStatus.treeConflictStatus = VCTreeConflictStatus.TreeConflict;
            }
            else if (line.Contains("reconcile to delete"))
            {
                // file is versioned, but has been deleted locally
                //                versionControlStatus.remoteStatus = VCRemoteFileStatus.Modified;
                versionControlStatus.fileStatus = VCFileStatus.Deleted;
            }
            /*
            XmlElement reposStatus = entryIt["repos-status"];
            if (reposStatus != null)
            {
                if (reposStatus.Attributes["item"] != null && reposStatus.Attributes["item"].InnerText != "normal") versionControlStatus.remoteStatus = VCRemoteFileStatus.Modified;

                XmlElement lockStatus = reposStatus["lock"];
                if (lockStatus != null)
                {
                    if (lockStatus["owner"] != null) versionControlStatus.owner = lockStatus["owner"].InnerText;
                    versionControlStatus.lockStatus = VCLockStatus.LockedOther;
                }
            }

            XmlElement wcStatus = entryIt["wc-status"];
            if (wcStatus != null)
            {
                if (wcStatus.Attributes["item"] == null || !P4ToVersionControlStatusMap.fileStatusMap.TryGetValue(wcStatus.Attributes["item"].InnerText, out versionControlStatus.fileStatus)) D.Log("P4: Unknown file status: " + wcStatus.Attributes["item"].InnerText);
                if (wcStatus.Attributes["props"] == null || !P4ToVersionControlStatusMap.propertyMap.TryGetValue(wcStatus.Attributes["props"].InnerText, out versionControlStatus.property)) D.Log("P4: Unknown property: " + wcStatus.Attributes["props"].InnerText);

                if (wcStatus.Attributes["revision"] != null) versionControlStatus.revision = Int32.Parse(wcStatus.Attributes["revision"].InnerText);
                if (wcStatus.Attributes["wc-locked"] != null && wcStatus.Attributes["wc-locked"].InnerText == "true") versionControlStatus.repositoryStatus = VCRepositoryStatus.Locked;
                if (wcStatus.Attributes["tree-conflicted"] != null) versionControlStatus.treeConflictStatus = (wcStatus.Attributes["tree-conflicted"].InnerText == "true") ? VCTreeConflictStatus.TreeConflict : VCTreeConflictStatus.Normal;

                XmlElement commit = wcStatus["commit"];
                if (commit != null)
                {
                    if (commit.Attributes["revision"] != null) versionControlStatus.lastModifiedRevision = Int32.Parse(commit.Attributes["revision"].InnerText);
                    if (commit["author"] != null) versionControlStatus.user = commit["author"].InnerText;
                }

                XmlElement lockStatus = wcStatus["lock"];
                if (lockStatus != null)
                {
                    if (lockStatus["owner"] != null) versionControlStatus.owner = lockStatus["owner"].InnerText;
                    if (lockStatus["token"] != null) versionControlStatus.lockToken = lockStatus["token"].InnerText;
                    versionControlStatus.lockStatus = VCLockStatus.LockedHere;
                }
            }
*/

            return versionControlStatus;
        }

        private static void AddItemToDatabase(string[] fstatLines, int rootDirLength, string rootUnixDir, ref StatusDatabase statusDatabase)
        {
            if (fstatLines.Length > 0)
            {
                P4FStatData fileData = new P4FStatData();
                fileData.ReadFromLines(fstatLines);
                string unixPath = fileData.clientFile.Replace("\\", "/");
                if (unixPath.Length > rootDirLength)
                {
                    string assetPath = unixPath.Remove(0, rootDirLength + 1);
                    if (unixPath.Contains(rootUnixDir))
                    {
                        var status = PopulateFromFstatData(fileData);
                        status.assetPath = assetPath;
                        statusDatabase[new ComposedString(assetPath)] = status;
                    }
                }
            }
        }

        public static StatusDatabase P4ParseFstat(string p4Status, string rootDir)
        {
            var statusDatabase = new StatusDatabase();
            var lines = p4Status.Split(new Char[] { '\r', '\n' });
            int numLines = 0;
            int rootDirLength = rootDir.Length;
            string rootUnixDir = rootDir.Replace("\\", "/");
            List<string> fstatLines = new List<string>();
            while (numLines < lines.Length)
            {
                String line = lines[numLines++];

                if (line.StartsWith("... depotFile"))
                {
                    AddItemToDatabase(fstatLines.ToArray(), rootDirLength, rootUnixDir, ref statusDatabase);
                    fstatLines.Clear();
                }

                fstatLines.Add(line);
            }

            // make sure we get the last one
            if (fstatLines.Count > 0)
            {
                AddItemToDatabase(fstatLines.ToArray(), rootDirLength, rootUnixDir, ref statusDatabase);
            }

            return statusDatabase;
        }

        private static VersionControlStatus PopulateFromFstatData(P4FStatData fileData)
        {
            var versionControlStatus = new VersionControlStatus();

            versionControlStatus.remoteStatus = fileData.haveRev == fileData.headRev ? VCRemoteFileStatus.None : VCRemoteFileStatus.Modified;
            versionControlStatus.lastModifiedRevision = fileData.headRev == -1 ? 1 : fileData.headRev;
            versionControlStatus.revision = fileData.haveRev == -1 ? 1 : fileData.haveRev;
            versionControlStatus.repositoryStatus = VCRepositoryStatus.NotLocked;    // this is only regarding the local copy
            versionControlStatus.user = "";    // supposed to be the last person who checked the file in - don't have that info in p4 fstat
            // we could do another p4 call, but that would not be performant for most cases
            versionControlStatus.changelist = fileData.change;

            if (fileData.otherLock)
            {
                versionControlStatus.lockStatus = VCLockStatus.LockedOther;
                versionControlStatus.owner = fileData.otherOwner;
            }
            else if (fileData.ourLock)
            {
                versionControlStatus.lockStatus = VCLockStatus.LockedHere;
                versionControlStatus.repositoryStatus = VCRepositoryStatus.Locked;
                versionControlStatus.owner = fileData.actionOwner;
            }
            else
            {
                versionControlStatus.lockStatus = VCLockStatus.NoLock;
            }

            if (!String.IsNullOrEmpty(fileData.action))
            {
                switch (fileData.action)
                {
                    case "add":
                        versionControlStatus.fileStatus = VCFileStatus.Added;
                        versionControlStatus.owner = fileData.actionOwner;
                        break;
                    case "edit":
                        // if we have it checked out and this is a "+l" type file, it must be locked
                        if (fileData.type.IndexOf("+l") != -1)
                        {
                            versionControlStatus.lockStatus = VCLockStatus.LockedHere;
                            versionControlStatus.repositoryStatus = VCRepositoryStatus.Locked;
                        }
                        else if (versionControlStatus.lockStatus == VCLockStatus.NoLock)
                        {
                            versionControlStatus.allowLocalEdit = true;
                        }
                        versionControlStatus.owner = fileData.actionOwner;
                        break;
                    case "delete":
                        versionControlStatus.fileStatus = VCFileStatus.Deleted;
                        versionControlStatus.owner = fileData.actionOwner;
                        break;
                    default:
                        DebugLog.LogError($"Unexpected action type: {fileData.action} for file {fileData.clientFile} - status may be incorrect.");
                        break;
                }
            }

            versionControlStatus.treeConflictStatus = VCTreeConflictStatus.Normal;

            //            if (wcStatus.Attributes["tree-conflicted"] != null) versionControlStatus.treeConflictStatus = (wcStatus.Attributes["tree-conflicted"].InnerText == "true") ? VCTreeConflictStatus.TreeConflict : VCTreeConflictStatus.Normal;


            return versionControlStatus;
        }

    }
}
