// Copyright (c) <2013> <E-Line Media, LLC>
using System;

namespace UVC.Backend.P4
{
    using Logging;
    public class P4FStatData
    {
        // normally, we wouldn't make these public, but we're treating this class
        // like a struct for the purposes of convenience
        public string clientFile    = "";
        public string depotFile     = "";
        public string movedFile     = "";
        public bool shelved         = false;
        public int headRev          = -1;
        public int haveRev          = -1;
        public string action        = "";
        public string actionOwner   = "";
        public string change        = "";
        public int otherOpen        = -1;
        public string otherOwner    = "";
        public bool otherLock       = false;
        public bool ourLock         = false;
        public string type          = "";

        public P4FStatData()
        {
        }

        public void ReadFromLines(string[] lines)
        {
            // typical fstat formatting:
            //... depotFile //depot/A
            //... clientFile C:\Users\username\Perforce\workspace_name\A
            //... headRev 7
            //... haveRev 7
            //... action edit
            //... actionOwner username
            //... change default
            //... ourLock 
            //
            //... depotFile //depot/Artwork/HQ.psd
            //... clientFile C:\Users\username\Perforce\workspace_name\Artwork\HQ.psd
            //... headRev 6
            //... haveRev 6
            //... ... otherOpen0 other_username@other_workspace
            //... ... otherOpen 1
            //... ... otherLock 
            //
            //... depotFile //depot/Artwork/elvenchain_export.mb
            //... clientFile C:\Users\username\Perforce\workspace_name\Artwork\elvenchain_export.mb
            //... headRev 3
            //... haveRev 3
            //... type binary+l

            foreach (String line in lines)
            {
                if (!String.IsNullOrEmpty(line))
                {
                    string cleanedLine = line.Replace("... ", "");
                    int firstSpace = cleanedLine.IndexOf(" ");
                    string attrName = cleanedLine.Substring(0, firstSpace);
                    string val = cleanedLine.Substring(firstSpace + 1).Trim();

                    switch (attrName)
                    {
                        case "clientFile":
                            clientFile = val;
                            break;
                        case "depotFile":
                            depotFile = val;
                            break;
                        case "movedFile":
                            movedFile = val;
                            break;
                        case "shelved":
                            shelved = true;
                            break;
                        case "headRev":
                            headRev = Int32.Parse(val);
                            break;
                        case "haveRev":
                            haveRev = Int32.Parse(val);
                            break;
                        case "action":
                            action = val;
                            break;
                        case "actionOwner":
                            actionOwner = val;
                            break;
                        case "change":
                            change = val;
                            break;
                        case "otherOpen":
                            otherOpen = Int32.Parse(val);
                            break;
                        case "otherOpen0":
                            otherOwner = val.Split('@')[0];
                            break;
                        case "otherLock":
                            otherLock = true;
                            break;
                        case "ourLock":
                            ourLock = true;
                            break;
                        case "type":
                            type = val;
                            break;
                        default:
                            if (!line.EndsWith("- no such file(s)."))
                            {
                                DebugLog.LogError($"p4 fstat line unrecognized: {line}");
                            }
                            break;
                    }
                }
            }
        }
    }
}

