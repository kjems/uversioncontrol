// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Globalization;
using System.Xml;

namespace UVC.Backend.SVN
{
    public static class SVNInfoXMLParser
    {
        public static InfoStatus SVNParseInfoXML(string path, string svnInfoXML)
        {
            var xmlInfoDocument = new XmlDocument();
            xmlInfoDocument.LoadXml(svnInfoXML);
            return ParseInfoResult(path, xmlInfoDocument);
        }
        
        private static InfoStatus ParseInfoResult(string path, XmlDocument xmlDoc)
        {
            if (!xmlDoc.HasChildNodes) return null;
            
            var entry  = xmlDoc.GetElementsByTagName("entry").Item(0);
            var commit = entry["commit"];
            var repo   = entry["repository"];
            
            InfoStatus infoStatus= new InfoStatus();
                
            infoStatus.url                  = entry["url"].InnerText;
            infoStatus.relativeUrl          = entry["relative-url"].InnerText;
            infoStatus.uuid                 = repo["uuid"].InnerText;;
            infoStatus.repositoryRoot       = repo["root"].InnerText;;
            infoStatus.author               = commit["author"].InnerText;
            infoStatus.revision             = Int32.Parse(entry.Attributes["revision"].InnerText);
            infoStatus.lastChangedRevision  = Int32.Parse(commit.Attributes["revision"].InnerText);
            infoStatus.lastChangedDate      = DateTime.Parse(commit["date"].InnerText, null, DateTimeStyles.RoundtripKind);
            
            return infoStatus;
        }
    }
}