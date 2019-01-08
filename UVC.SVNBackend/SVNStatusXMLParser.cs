// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Unity.Profiling;

namespace UVC.Backend.SVN
{
    using Logging;
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    #region EnumMaps
    internal static class SVNToVersionControlStatusMap
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


    public static class SVNStatusXMLParser
    {
        private static ProfilerMarker svnParseStatusXMLMarker = new ProfilerMarker("UVC.SVNParseStatusXML");
        private static readonly ComposedString dot = new ComposedString(".");
        private static readonly ComposedString slash = new ComposedString("/");
        public static StatusDatabase SVNParseStatusXML(string svnStatusXML)
        {
            using (svnParseStatusXMLMarker.Auto())
            {
                var xmlStatusDocument = new XmlDocument();
                xmlStatusDocument.LoadXml(svnStatusXML);
                return ParseStatusResult(xmlStatusDocument);
            }
        }

        private static StatusDatabase ParseStatusResult(XmlDocument xmlDoc)
        {
            if (!xmlDoc.HasChildNodes) return null;

            var statusDatabase = new StatusDatabase();

            XmlNodeList entries = xmlDoc.GetElementsByTagName("entry");
            foreach (XmlNode entryIt in entries)
            {
                ComposedString assetPath = new ComposedString((entryIt.Attributes["path"].InnerText.Replace('\\', '/')).Trim());
                var status = ParseXMLNode(entryIt, assetPath);
                status.assetPath = assetPath;
                statusDatabase[assetPath] = status;
            }

            XmlNodeList changelists = xmlDoc.GetElementsByTagName("changelist");
            foreach (XmlNode changelistIt in changelists)
            {
                string changelist = changelistIt.Attributes["name"].InnerText;
                foreach (XmlNode entryIt in changelistIt.ChildNodes)
                {
                    ComposedString assetPath = new ComposedString((entryIt.Attributes["path"].InnerText.Replace('\\', '/')).Trim());
                    if (statusDatabase.ContainsKey(assetPath))
                    {
                        statusDatabase[assetPath].changelist = changelist;
                        if (changelist == SVNCommands.localEditChangeList)
                        {
                            statusDatabase[assetPath].allowLocalEdit = true;
                        }
                        if (changelist == SVNCommands.localOnlyChangeList)
                        {
                            statusDatabase[assetPath].localOnly = true;
                        }
                    }
                }
            }

            foreach (var assetPathIt in new List<ComposedString>(statusDatabase.Keys))
            {
                string assetPathStr = assetPathIt.Compose();
                if (Directory.Exists(assetPathStr))
                {
                    var status = statusDatabase[assetPathIt];
                    if (status.fileStatus == VCFileStatus.Unversioned)
                    {
                        foreach (var unversionedIt in GetFilesInFolder(assetPathStr))
                        {
                            var fileStatus = new VersionControlStatus
                            {
                                assetPath = unversionedIt,
                                fileStatus = VCFileStatus.Unversioned,
                            };
                            statusDatabase[unversionedIt] = fileStatus;
                        }
                    }
                }
            }
            return statusDatabase;
        }

        private static VersionControlStatus ParseXMLNode(XmlNode entryIt, ComposedString assetPath)
        {
            var versionControlStatus = new VersionControlStatus();
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
                if (wcStatus.Attributes["item"] == null || !SVNToVersionControlStatusMap.fileStatusMap.TryGetValue(wcStatus.Attributes["item"].InnerText, out versionControlStatus.fileStatus)) DebugLog.Log("SVN: Unknown file status: " + wcStatus.Attributes["item"].InnerText);
                if (wcStatus.Attributes["props"] == null || !SVNToVersionControlStatusMap.propertyMap.TryGetValue(wcStatus.Attributes["props"].InnerText, out versionControlStatus.property)) DebugLog.Log("SVN: Unknown property: " + wcStatus.Attributes["props"].InnerText);

                if (wcStatus.Attributes["revision"] != null) versionControlStatus.revision = Int32.Parse(wcStatus.Attributes["revision"].InnerText);
                if (wcStatus.Attributes["wc-locked"] != null && wcStatus.Attributes["wc-locked"].InnerText == "true") versionControlStatus.repositoryStatus = VCRepositoryStatus.Locked;
                if (wcStatus.Attributes["tree-conflicted"] != null) versionControlStatus.treeConflictStatus = (wcStatus.Attributes["tree-conflicted"].InnerText == "true") ? VCTreeConflictStatus.TreeConflict : VCTreeConflictStatus.Normal;
                /*if (wcStatus.Attributes["moved-from"] != null)
                {
                    var movedFrom = new ComposedString(wcStatus.Attributes["moved-from"].InnerText.Replace('\\', '/').Trim());
                    if (movedFrom.StartsWith(dot))
                    {
                        var lastIndex = assetPath.FindLastIndex(slash);
                        if (lastIndex != -1)
                        {
                            versionControlStatus.movedFrom = assetPath.GetSubset(0, lastIndex + 1) + movedFrom;
                        }
                    }
                    else
                    {
                        versionControlStatus.movedFrom = movedFrom;
                    }
                    Debug.Log($"Moved From {movedFrom} => {assetPath} : {versionControlStatus.movedFrom}");
                }*/

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
            return versionControlStatus;
        }

        private static IEnumerable<string> GetFilesInFolder(string assetPath)
        {
            if (!Directory.Exists(assetPath))
            {
                DebugLog.LogWarning("Directory not found: " + assetPath);
                return new string[] { };
            }

            return
                Directory.GetFiles(assetPath, "*", SearchOption.AllDirectories)
                    .Concat(Directory.GetDirectories(assetPath, "*", SearchOption.AllDirectories))
                    .Where(a => File.Exists(a) && !a.Contains("/.") && !a.Contains("\\.") && (File.GetAttributes(a) & FileAttributes.Hidden) == 0)
                    .Select(s => s.Replace("\\", "/"));
        }

    }
}



/*
 * 'SVN status --xml' schema
 *
# Licensed to the Apache Software Foundation (ASF) under one
# or more contributor license agreements.  See the NOTICE file
# distributed with this work for additional information
# regarding copyright ownership.  The ASF licenses this file
# to you under the Apache License, Version 2.0 (the
# "License"); you may not use this file except in compliance
# with the License.  You may obtain a copy of the License at
#
#   http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing,
# software distributed under the License is distributed on an
# "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
# KIND, either express or implied.  See the License for the
# specific language governing permissions and limitations
# under the License.
#
#
# XML RELAX NG schema for Subversion command-line client output
# For "svn status"

# The DTD compatibility annotations namespace, used for adding default
# attribute values.
namespace a = "http://relaxng.org/ns/compatibility/annotations/1.0"

include "common.rnc"

start = status

status = element status { (target | changelist)* }

target = element target { attlist.target, entry*, against? }
attlist.target &=
  ## The target path.
  attribute path { string }

changelist = element changelist { attlist.changelist, entry*, against? }
attlist.changelist &=
  ## The changelist name.
  attribute name { string }

## Status information for a path under the target.
entry = element entry { attlist.entry, wc-status, repos-status? }
attlist.entry &=
  ## Path inside the target.
  attribute path { text }

## Status of the entry in the working copy.
wc-status = element wc-status { attlist.wc-status, commit?, lock? }

attlist.wc-status &=
  ## Item/text status.
  attribute item {
    "added" | "conflicted" | "deleted" | "external" | "ignored" |
    "incomplete" | "merged" | "missing" | "modified" | "none" |
    "normal" | "obstructed" | "replaced" | "unversioned"
  },
  ## Properties status.
  attribute props { "conflicted" | "modified" | "normal" | "none" },
  ## Base revision number.
  attribute revision { revnum.type }?,
  ## WC directory locked.
  [ a:defaultValue = "false" ]
  attribute wc-locked { "true" | "false" }?,
  ## Add with history.
  [ a:defaultValue = "false" ]
  attribute copied { "true" | "false" }?,
  # Item switched relative to its parent.
  [ a:defaultValue = "false" ]
  attribute switched { "true" | "false" }?,
  ## Tree-conflict status of the item.
  [ a:defaultValue = "false" ]
  attribute tree-conflicted { "true" | "false" }?

## Status in repository (if --update was specified).
repos-status = element repos-status { attlist.repos-status, lock? }
attlist.repos-status &=
  ## Text/item status in the repository.
  attribute item {
    "added" | "deleted" | "modified" | "replaced" | "none"
  },
  ## Properties status in repository.
  attribute props { "modified" | "none" }

against = element against { attlist.against, empty }
attlist.against &=
  ## Revision number at which the repository information was obtained.
  attribute revision { revnum.type }
*/
