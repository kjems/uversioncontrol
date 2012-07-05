// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using CommandLineExecution;

namespace VersionControl.Backend.SVN
{
    public class SVNCommands : IVersionControlCommands
    {
        private string workingDirectory = ".";
        private string userName;
        private string password;
        private string versionNumber;
        private StatusDatabase statusDatabase = new StatusDatabase();
        private bool operationActive = false;
        private readonly object operationActiveLockToken = new object();
        private readonly object statusDatabaseLockToken = new object();

        public SVNCommands()
        {
            RefreshStatusLoop();
        }

        private void RefreshStatusLoop()
        {
            ThreadPool.QueueUserWorkItem(o =>
            {
                while(true)
                {
                    Thread.Sleep(100);
                    RefreshStatusDatabase();
                }
            });
        }
        
        private bool RefreshStatusDatabase()
        {
            lock (statusDatabaseLockToken)
            {
                var requestLocal = statusDatabase.Where(a => a.Value.reflectionLevel == VCReflectionLevel.RequestLocal).Select(a => a.Key).ToList();
                var requestRepository = statusDatabase.Where(a => a.Value.reflectionLevel == VCReflectionLevel.RequestRepository).Select(a => a.Key).ToList();

                //if (requestLocal.Count > 0) D.Log("Local Status : " + requestLocal.Aggregate((a, b) => a + ", " + b));
                //if (requestRepository.Count > 0) D.Log("\nRemote Status : " + requestRepository.Aggregate((a, b) => a + ", " + b));

                //if (requestLocal.Count > 50) Status(true, false);
                if (requestLocal.Count > 0) Status(requestLocal, false);

                //if (requestRepository.Count > 50) Status(true, true);
                if (requestRepository.Count > 0) Status(requestRepository, true);
            }
            return true;
        }

        public bool IsReady()
        {
            return !operationActive;
        }

        public void SetWorkingDirectory(string workingDirectory)
        {
            this.workingDirectory = workingDirectory;
        }

        public void SetUserCredentials(string userName, string password)
        {
            if (!string.IsNullOrEmpty(userName)) this.userName = userName;
            if (!string.IsNullOrEmpty(password)) this.password = password;
        }

        public VersionControlStatus GetAssetStatus(string assetPath)
        {
            assetPath = assetPath.Replace("\\", "/");
            return statusDatabase[assetPath];
        }

        public IEnumerable<string> GetFilteredAssets(Func<string, VersionControlStatus, bool> filter)
        {
            return new List<string>(statusDatabase.Keys).Where(k => filter(k, statusDatabase[k])).ToList();
        }

        public bool Status(bool remote, bool full)
        {
            string arguments = "status --xml";
            if (remote) arguments += " -u";
            if (full) arguments += " -v";

            CommandLineOutput commandLineOutput;
            using (var svnStatusTask = CreateSVNCommandLine(arguments))
            {
                commandLineOutput = ExecuteCommandLine(svnStatusTask);
            }

            if (commandLineOutput.Failed) return false;
            try
            {
                statusDatabase = SVNStatusXMLParser.SVNParseStatusXML(commandLineOutput.OutputStr);
                StatusUpdated();
            }
            catch (XmlException)
            {
                return false;
            }
            return true;
        }

        public bool Status(IEnumerable<string> assets, bool remote)
        {
            string arguments = "status --xml -v ";
            if (remote) arguments += "-u ";
            else arguments += " --depth=empty ";
            arguments += ConcatAssetPaths(RemoveWorkingDirectoryFromPath(assets));
            foreach (var assetIt in assets)
            {
                statusDatabase[assetIt] = new VersionControlStatus { assetPath = assetIt, reflectionLevel = VCReflectionLevel.Pending };
            }
            CommandLineOutput commandLineOutput;
            using (var svnStatusTask = CreateSVNCommandLine(arguments))
            {
                commandLineOutput = ExecuteCommandLine(svnStatusTask);
            }
            if (commandLineOutput.Failed) return false;
            try
            {
                var db = SVNStatusXMLParser.SVNParseStatusXML(commandLineOutput.OutputStr);
                lock (statusDatabaseLockToken)
                {
                    foreach (var statusIt in db)
                    {
                        statusDatabase[statusIt.Key] = statusIt.Value;
                    }
                }
                StatusUpdated();
            }
            catch (XmlException)
            {
                return false;
            }
            return true;
        }

        private CommandLine CreateSVNCommandLine(string arguments)
        {
            arguments = "--non-interactive " + arguments;
            if (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(password))
            {
                arguments = " --username " + userName + " --password " + password + " --no-auth-cache " + arguments;
            }
            return new CommandLine("svn", arguments, workingDirectory);
        }

        private bool CreateOperation(string arguments)
        {
            CommandLineOutput commandLineOutput;
            using (var commandLineOperation = CreateSVNCommandLine(arguments))
            {
                commandLineOperation.OutputReceived += OnProgressInformation;
                commandLineOperation.ErrorReceived += OnProgressInformation;
                commandLineOutput = ExecuteCommandLine(commandLineOperation);
            }
            return !commandLineOutput.Failed;
        }

        private CommandLineOutput ExecuteCommandLine(CommandLine commandLine)
        {
            CommandLineOutput commandLineOutput;
            lock (operationActiveLockToken)
            {
                try
                {
                    operationActive = true;
                    D.Log(commandLine.ToString());
                    //System.Threading.Thread.Sleep(100); // emulate latency to SVN server
                    commandLineOutput = commandLine.Execute();
                }
                catch (Exception e)
                {
                    throw new VCCriticalException("Check that your commandline SVN client is installed corretly\n\n" + e.Message, commandLine.ToString(), e);
                }
                finally
                {
                    operationActive = false;
                }
            }
            if (commandLineOutput.Arguments.Contains("ExceptionTest.txt"))
            {
                throw new VCException("Test Exception cast due to ExceptionTest.txt being a part of arguments", commandLine.ToString());
            }
            if (!string.IsNullOrEmpty(commandLineOutput.ErrorStr))
            {
                var errStr = commandLineOutput.ErrorStr;
                if (errStr.Contains("E730060") || errStr.Contains("Unable to connect") || errStr.Contains("is unreachable") || errStr.Contains("Operation timed out") || errStr.Contains("Can't connect to"))
                    throw new VCConnectionTimeoutException(errStr, commandLine.ToString());
                if (errStr.Contains("W160042") || errStr.Contains("Newer Version"))
                    throw new VCNewerVersionException(errStr, commandLine.ToString());
                if (errStr.Contains("W155007") || errStr.Contains("'" + workingDirectory + "'" + " is not a working copy"))
                    throw new VCCriticalException(errStr, commandLine.ToString());
                if (errStr.Contains("E160028") || errStr.Contains("is out of date"))
                    throw new VCOutOfDate(errStr, commandLine.ToString());
                if (errStr.Contains("E155037") || errStr.Contains("E155004") || errStr.Contains("run 'svn cleanup'"))
                    throw new VCLocalCopyLockedException(errStr, commandLine.ToString());
                if (errStr.Contains("W160035") || errStr.Contains("run 'svn cleanup'"))
                    throw new VCLockedByOther(errStr, commandLine.ToString());
                throw new VCException(errStr, commandLine.ToString());
            }
            return commandLineOutput;
        }

        private bool CreateAssetOperation(string arguments, IEnumerable<string> assets)
        {
            if (assets == null || !assets.Any()) return true;
            return CreateOperation(arguments + ConcatAssetPaths(assets)) && RequestStatus(assets, false);
        }

        private static string FixAtChar(string asset)
        {
            return asset.Contains("@") ? asset + "@" : asset;
        }

        private IEnumerable<string> RemoveWorkingDirectoryFromPath(IEnumerable<string> assets)
        {
            return assets.Select(a => a.Replace(workingDirectory, ""));
        }

        private static string ConcatAssetPaths(IEnumerable<string> assets)
        {
            assets = assets.Select(a => a.Replace("\\", "/"));
            assets = assets.Select(FixAtChar);
            if (assets.Any()) return " \"" + assets.Aggregate((i, j) => i + "\" \"" + j) + "\"";
            return "";
        }

        public virtual bool RequestStatus(IEnumerable<string> assets, bool remote)
        {
            if (assets == null || assets.Count() == 0) return true;
            bool result = true;
            foreach (string asset in assets)
            {
                result &= RequestStatus(asset, remote);
            }
            return result;
        }

        public virtual bool RequestStatus(string asset, bool remote)
        {
            lock (statusDatabaseLockToken)
            {
                var assetStatus = statusDatabase[asset];
                assetStatus.reflectionLevel = remote ? VCReflectionLevel.RequestRepository : VCReflectionLevel.RequestLocal;
                statusDatabase[asset] = assetStatus;
                //D.Log("Request Status : " + asset + ", reflection level after : '" + statusDatabase[asset].reflectionLevel + "'");
            }
            return true;            
        }

        public bool Update(IEnumerable<string> assets = null, bool force = true)
        {
            if (assets == null || !assets.Any()) assets = new[] { workingDirectory };
            return CreateAssetOperation("update" + (force ? " --force" : ""), assets);
        }

        public bool Commit(IEnumerable<string> assets, string commitMessage = "")
        {
            return CreateAssetOperation("commit -m \"" + commitMessage + "\"", assets);
        }

        public bool Add(IEnumerable<string> assets)
        {
            return CreateAssetOperation("add", assets);
        }

        public bool Revert(IEnumerable<string> assets)
        {
            return CreateAssetOperation("revert --depth=infinity", assets);
        }

        public bool Delete(IEnumerable<string> assets, bool force = false)
        {
            return CreateAssetOperation("delete" + (force ? " --force" : ""), assets);
        }

        public bool GetLock(IEnumerable<string> assets, bool force)
        {
            return CreateAssetOperation("lock" + (force ? " --force" : ""), assets);
        }

        public bool ReleaseLock(IEnumerable<string> assets)
        {
            return CreateAssetOperation("unlock", assets);
        }

        public bool ChangeListAdd(IEnumerable<string> assets, string changelist)
        {
            return CreateAssetOperation("changelist " + changelist, assets);
        }

        public bool ChangeListRemove(IEnumerable<string> assets)
        {
            return CreateAssetOperation("changelist --remove", assets);
        }

        public bool Checkout(string url, string path = "")
        {
            return CreateOperation("checkout \"" + url + "\" \"" + (path == "" ? workingDirectory : path) + "\"");
        }

        public bool Resolve(IEnumerable<string> assets, ConflictResolution conflictResolution)
        {
            if (conflictResolution == ConflictResolution.Ignore) return true;
            string conflictparameter = conflictResolution == ConflictResolution.Theirs ? "--accept theirs-full" : "--accept mine-full";
            return CreateAssetOperation("resolve " + conflictparameter, assets);
        }

        public bool Move(string from, string to)
        {
            return CreateOperation("move \"" + from + "\" \"" + to + "\"") && RequestStatus(new[]{from, to}, false);
        }

        public string GetBasePath(string assetPath)
        {
            if (string.IsNullOrEmpty(versionNumber))
            {
                versionNumber = CreateSVNCommandLine("--version --quiet").Execute().OutputStr;
            }
            if (versionNumber.StartsWith("1.7"))
            {
                var svnInfo = CreateSVNCommandLine("info --xml " + assetPath).Execute();
                if (!svnInfo.Failed)
                {
                    var xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(svnInfo.OutputStr);
                    var checksumNode = xmlDoc.GetElementsByTagName("checksum").Item(0);
                    var rootPathNode = xmlDoc.GetElementsByTagName("wcroot-abspath").Item(0);

                    if (checksumNode != null && rootPathNode != null)
                    {
                        string checksum = checksumNode.InnerText;
                        string firstTwo = checksum.Substring(0, 2);
                        string rootPath = rootPathNode.InnerText;
                        string basePath = rootPath + "/.svn/pristine/" + firstTwo + "/" + checksum + ".svn-base";
                        if (File.Exists(basePath)) return basePath;
                    }
                }
            }
            if (versionNumber.StartsWith("1.6"))
            {
                return Path.GetDirectoryName(assetPath) + "/.svn/text-base/" + Path.GetFileName(assetPath) + ".svn-base";
            }
            return "";
        }

        public bool CleanUp()
        {
            return CreateOperation("cleanup");
        }

        public void ClearDatabase()
        {
            statusDatabase.Clear();
        }

        public void RemoveFromDatabase(IEnumerable<string> assets)
        {
            foreach(var assetIt in assets)
            {
                statusDatabase.Remove(assetIt);
            }
        }

        public event Action<string> ProgressInformation;
        private void OnProgressInformation(string info)
        {
            if (ProgressInformation != null) ProgressInformation(info);
        }

        public event Action StatusUpdated;
        private void OnStatusUpdated()
        {
            if (StatusUpdated != null) StatusUpdated();
        }
    }
}
