// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace UVC.Backend.SVN
{
    public static class SVNBranchXMLParser
    {
        public static List<BranchStatus> SVNParseBranchXML(string path, string svnBranchXML)
        {
            var xmlBranchDocument = new XmlDocument();
            xmlBranchDocument.LoadXml(svnBranchXML);
            return ParseBranchResult(path, xmlBranchDocument);
        }
        
        private static List<BranchStatus> ParseBranchResult(string path, XmlDocument xmlDoc)
        {
            if (!xmlDoc.HasChildNodes) return null;
            
            List<BranchStatus> branchStatuses = new List<BranchStatus>();
            XmlNodeList entries = xmlDoc.GetElementsByTagName("entry");
            foreach (XmlNode entryIt in entries)
            {
                if (entryIt != null && entryIt.Attributes["kind"].InnerText == "dir")
                {
                    var commitEntry = entryIt["commit"];
                    BranchStatus branchStatus = new BranchStatus
                    {
                        name     = path + entryIt["name"].InnerText,
                        author   = commitEntry["author"].InnerText,
                        date     = DateTime.Parse(commitEntry["date"].InnerText, null, DateTimeStyles.RoundtripKind),
                        revision = Int32.Parse(commitEntry.Attributes["revision"].InnerText)
                    };
                    branchStatuses.Add(branchStatus);
                }
            }
            return branchStatuses;
        }
    }
}