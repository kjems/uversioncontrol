// Copyright (c) <2013> <E-Line Media, LLC>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CommandLineExecution;
using System.Timers;

namespace UVC.Backend.P4
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    using Logging;
    public class P4Commands : MarshalByRefObject, IVersionControlCommands
    {
        internal class P4QueueItem
        {
            public StatusLevel level;
            public string path;

            public P4QueueItem(StatusLevel _level, string _path)
            {
                level = _level;
                path = _path;
            }
        }

        // Custom comparer for the QueueItem class
        class P4QueueItemComparer : IEqualityComparer<P4QueueItem>
        {
            // P4QueueItems are equal if their status levels and paths are equal.
            public bool Equals(P4QueueItem x, P4QueueItem y)
            {
                //Check whether the compared objects reference the same data.
                if (Object.ReferenceEquals(x, y)) return true;

                //Check whether any of the compared objects is null.
                if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                    return false;

                //Check whether the P4QueueItem's properties are equal.
                return x.level == y.level && x.path == y.path;
            }

            // If Equals() returns true for a pair of objects
            // then GetHashCode() must return the same value for these objects.

            public int GetHashCode(P4QueueItem item)
            {
                //Check whether the object is null
                if (Object.ReferenceEquals(item, null)) return 0;

                //Get hash code for the path field if it is not null.
                int hashItemPath = item.path == null ? 0 : item.path.GetHashCode();

                //Get hash code for the level field.
                int hashItemLevel = item.level.GetHashCode();

                //Calculate the hash code for the product.
                return hashItemPath ^ hashItemLevel;
            }

        }

        private static string fstatAttributes = "clientFile,depotFile,movedFile,shelved,headRev,haveRev,action,actionOwner,change,otherOpen,otherOpen0,otherLock,ourLock,type";
        private string rootPath = "";
        private string versionNumber;
        private Dictionary<string, string> depotToDir = null;
        private readonly StatusDatabase statusDatabase = new StatusDatabase();
        private bool OperationActive { get { return currentExecutingOperation != null; } }
        private CommandLine currentExecutingOperation = null;
        private Thread refreshThread = null;
        private System.Timers.Timer remoteRefreshTimer = null;
        private readonly object p4QueueLockToken = new object();
        private readonly object requestQueueLockToken = new object();
        private readonly object statusDatabaseLockToken = new object();
        private readonly List<string> localRequestQueue = new List<string>();
        private readonly List<string> remoteRequestQueue = new List<string>();
        private volatile bool active = false;
        private volatile bool refreshLoopActive = false;
        private volatile bool requestRefreshLoopStop = false;
        private DirectoryCrawler dirStatus = new DirectoryCrawler();
        private List<P4QueueItem> p4OpQueue = new List<P4QueueItem>();    // not using a Queue<> because we need to insert high-priority items

        public P4Commands()
        {
            InitializeP4Connection();
            StartRefreshLoop();
            AppDomain.CurrentDomain.DomainUnload += Unload;
            AppDomain.CurrentDomain.ProcessExit += Unload;
        }

        private void Unload(object sender, EventArgs args)
        {
            TerminateRefreshLoop();
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.DomainUnload -= Unload;
            AppDomain.CurrentDomain.ProcessExit -= Unload;
            TerminateRefreshLoop();
        }

        private void RefreshLoop()
        {
            try
            {
                while (!requestRefreshLoopStop)
                {
                    if (active && refreshLoopActive)
                    {
                        // make sure p4 data is initialized
                        if (P4Util.Instance.P4Initialized || InitializeP4Connection())
                        {
                            // refresh status
                            RefreshStatusDatabase();

                            lock (p4QueueLockToken)
                            {
                                // run p4 ops
                                if (p4OpQueue.Count > 0)
                                {
                                    P4QueueItem item = p4OpQueue[0];
                                    p4OpQueue.RemoveAt(0);
                                    GetStatus(item.level, "fstat -T " + fstatAttributes, item.path);
                                }
                            }
                        }
                    }
                }
            }
            catch (ThreadAbortException) { }
            catch (AppDomainUnloadedException) { }
            catch (Exception e)
            {
                DebugLog.ThrowException(e);
            }
            if (!requestRefreshLoopStop) RefreshLoop();
        }

        private void StartRefreshLoop()
        {
            if (refreshThread == null)
            {
                refreshThread = new Thread(RefreshLoop);
                refreshThread.Start();
            }
        }

        // This should only be used during termination of the host AppDomain or Process
        private void TerminateRefreshLoop()
        {
            active = false;
            DeactivateRefreshLoop();
            requestRefreshLoopStop = true;
            if (currentExecutingOperation != null)
            {
                currentExecutingOperation.Dispose();
                currentExecutingOperation = null;
            }
            if (refreshThread != null)
            {
                refreshThread.Abort();
                refreshThread = null;
            }
        }

        public void Start()
        {
            active = true;
        }

        public void Stop()
        {
            active = false;
        }

        private void DestroyTimer()
        {
            // destroy timer if it already exists
            if (remoteRefreshTimer != null)
            {
                remoteRefreshTimer.Enabled = false;
                remoteRefreshTimer.Elapsed -= new ElapsedEventHandler(OnTimerExpired);
                remoteRefreshTimer.Stop();
                remoteRefreshTimer = null;
            }
        }

        public void ActivateRefreshLoop()
        {
            refreshLoopActive = true;

            DestroyTimer();

            // only allow refreshing remote status every 5 minutes
            remoteRefreshTimer = new System.Timers.Timer(300000);

            // Hook up the Elapsed event for the timer.
            remoteRefreshTimer.Elapsed += new ElapsedEventHandler(OnTimerExpired);

            remoteRefreshTimer.Start();
            remoteRefreshTimer.Enabled = true;
            remoteRefreshTimer.AutoReset = true;
        }

        private void OnTimerExpired(object source, ElapsedEventArgs e)
        {
            Status(StatusLevel.Remote, DetailLevel.Normal);
        }

        public void DeactivateRefreshLoop()
        {
            refreshLoopActive = false;
            DestroyTimer();
        }


        private void RefreshStatusDatabase()
        {
            List<string> localCopy = null;
            List<string> remoteCopy = null;

            lock (requestQueueLockToken)
            {
                if (localRequestQueue.Count > 0)
                {
                    localCopy = new List<string>(localRequestQueue.Except(remoteRequestQueue).Distinct());
                    localRequestQueue.Clear();
                }
                if (remoteRequestQueue.Count > 0)
                {
                    remoteCopy = new List<string>(remoteRequestQueue.Distinct());
                    remoteRequestQueue.Clear();
                }
            }
            //if (localCopy != null && localCopy.Count > 0) D.Log("Local Status : " + localCopy.Aggregate((a, b) => a + ", " + b));
            //if (remoteCopy != null && remoteCopy.Count > 0) D.Log("Remote Status : " + remoteCopy.Aggregate((a, b) => a + ", " + b));
            if (localCopy != null && localCopy.Count > 0) Status(localCopy, StatusLevel.Local);
            if (remoteCopy != null && remoteCopy.Count > 0) Status(remoteCopy, StatusLevel.Remote);
        }

        private bool InitializeP4Connection()
        {
            CommandLineOutput commandLineOutput;

            P4Util.Instance.InitVars();

            // get directory info
            using (var p4WhereTask = P4Util.Instance.CreateP4CommandLine("client -o"))
            {
                commandLineOutput = P4Util.Instance.ExecuteOperation(p4WhereTask);
                if (commandLineOutput == null || commandLineOutput.Failed)
                {
                    throw new VCInitializationException("Perforce Initialization failed", commandLineOutput.ErrorStr);
                }
                depotToDir = new Dictionary<string, string>();
                // sample output:
                //# A Perforce Client Specification.
                //#
                //#  Client:      The client name.
                //#  Update:      The date this specification was last modified.
                //#  Access:      The date this client was last used in any way.
                //#  Owner:       The Perforce user name of the user who owns the client
                //#               workspace. The default is the user who created the
                //#               client workspace.
                //#  Host:        If set, restricts access to the named host.
                //#  Description: A short description of the client (optional).
                //#  Root:        The base directory of the client workspace.
                //#  AltRoots:    Up to two alternate client workspace roots.
                //#  Options:     Client options:
                //#                      [no]allwrite [no]clobber [no]compress
                //#                      [un]locked [no]modtime [no]rmdir
                //#  SubmitOptions:
                //#                      submitunchanged/submitunchanged+reopen
                //#                      revertunchanged/revertunchanged+reopen
                //#                      leaveunchanged/leaveunchanged+reopen
                //#  LineEnd:     Text file line endings on client: local/unix/mac/win/share.
                //#  ServerID:    If set, restricts access to the named server.
                //#  View:        Lines to map depot files into the client workspace.
                //#  Stream:      The stream to which this client's view will be dedicated.
                //#               (Files in stream paths can be submitted only by dedicated
                //#               stream clients.) When this optional field is set, the
                //#               View field will be automatically replaced by a stream
                //#               view as the client spec is saved.
                //#
                //# Use 'p4 help client' to see more about client views and options.
                //
                //Client: workspace_name
                //
                //Update: 2013/04/23 21:52:59
                //
                //Access: 2013/04/24 06:15:05
                //
                //Owner:  username
                //
                //Host:   machine_name
                //
                //Description:
                //        Created by username.
                //
                //Root:   C:\Users\username\Perforce\workspace_name
                //
                //Options:        noallwrite noclobber nocompress unlocked nomodtime normdir
                //
                //SubmitOptions:  submitunchanged
                //
                //LineEnd:        local
                //
                //View:
                //        //depot/... //workspace_name/...
                //        -//depot/Temp/... //workspace_name/Temp/...
                //        -//depot/Library/... //workspace_name/Library/...
                string output = commandLineOutput.OutputStr;
                var lines = output.Split(new Char[] { '\r', '\n' });
                foreach (String line in lines)
                {
                    //D.Log( line );
                    if (line.StartsWith("Root:"))
                    {
                        rootPath = line.Substring("Root:".Length).Trim().Replace("\\", "/");
                    }
                    else if (line.Trim().StartsWith("//"))
                    {
                        if (line.IndexOf("...") != -1)
                        {
                            string repoPath = line.Substring(0, line.IndexOf("...")).Trim();
                            //D.Log( "Repo Path: " + repoPath );
                            int clientPathStart = repoPath.Length + "...".Length + 1;
                            string clientPath = line.Substring(clientPathStart, line.IndexOf("...", clientPathStart) - clientPathStart).Trim();
                            //D.Log( "Client Path: " + clientPath );
                            string localPath = clientPath.Replace("//" + P4Util.Instance.Vars.clientSpec, rootPath);
                            //D.Log( "Local Path: " + localPath );
                            depotToDir.Add(repoPath, localPath);
                        }
                    }
                }
            }

            P4Util.Instance.GetIgnoreStrings(rootPath);

            return P4Util.Instance.P4Initialized;
        }

        public bool IsReady()
        {
            return !OperationActive && active;
        }

        public bool HasValidLocalCopy()
        {
            return P4Util.Instance.P4Initialized;
        }

        public void SetWorkingDirectory(string workingDirectory)
        {
            P4Util.Instance.Vars.workingDirectory = workingDirectory;
            P4Util.Instance.Vars.unixWorkingDirectory = workingDirectory.Replace("\\", "/");
            dirStatus.SearchDepth = 2;
            dirStatus.IgnoreStrings = P4Util.Instance.IgnoreStrings;
            dirStatus.StoreRelativePaths = true;
            dirStatus.Directory = workingDirectory.Replace("/", Path.DirectorySeparatorChar.ToString());
        }

        public bool SetUserCredentials(string userName, string password, bool cacheCredentials)
        {
            if (!string.IsNullOrEmpty(userName)) P4Util.Instance.Vars.userName = userName;
            if (!string.IsNullOrEmpty(password)) P4Util.Instance.Vars.password = password;
            return true;
        }

        public VersionControlStatus GetAssetStatus(string assetPath)
        {
            assetPath = assetPath.Replace("\\", "/");
            return GetAssetStatus(new ComposedString(assetPath));
        }

        public VersionControlStatus GetAssetStatus(ComposedString assetPath)
        {
            lock (statusDatabaseLockToken)
            {
                return statusDatabase[assetPath];
            }
        }

        public IEnumerable<VersionControlStatus> GetFilteredAssets(Func<VersionControlStatus, bool> filter)
        {
            lock (statusDatabaseLockToken)
            {
                return new List<VersionControlStatus>(statusDatabase.Values.Where(filter).Where(s => !Directory.Exists(s.assetPath.Compose())));
            }
        }

        public InfoStatus GetInfo(string path)
        {
            return null;
        }

        public bool GetStatus(StatusLevel statusLevel, string fstatArgs, string path)
        {
            //D.Log( "Processing " + path );

            string arguments = "status -aedf \"" + path + "\"";

            CommandLineOutput statusCommandLineOutput = null;
            if (statusLevel == StatusLevel.Local)
            {
                using (var p4StatusTask = P4Util.Instance.CreateP4CommandLine(arguments))
                {
                    statusCommandLineOutput = P4Util.Instance.ExecuteOperation(p4StatusTask);
                }
            }

            arguments = fstatArgs + " \"" + path + "\"";
            CommandLineOutput fstatCommandLineOutput = null;
            using (var p4FstatTask = P4Util.Instance.CreateP4CommandLine(arguments))
            {
                fstatCommandLineOutput = P4Util.Instance.ExecuteOperation(p4FstatTask);
            }

            if (statusCommandLineOutput == null || statusCommandLineOutput.Failed || string.IsNullOrEmpty(statusCommandLineOutput.OutputStr) || !active) return false;
            if (fstatCommandLineOutput == null || fstatCommandLineOutput.Failed || string.IsNullOrEmpty(fstatCommandLineOutput.OutputStr) || !active) return false;
            try
            {
                var statusDB = statusCommandLineOutput != null ? P4StatusParser.P4ParseStatus(statusCommandLineOutput.OutputStr, P4Util.Instance.Vars.userName) : null;
                var fstatDB = P4StatusParser.P4ParseFstat(fstatCommandLineOutput.OutputStr, P4Util.Instance.Vars.workingDirectory);
                lock (statusDatabaseLockToken)
                {
                    if (statusDB != null)
                    {
                        foreach (var statusIt in statusDB)
                        {
                            var status = statusIt.Value;
                            status.reflectionLevel = statusLevel == StatusLevel.Remote ? VCReflectionLevel.Repository : VCReflectionLevel.Local;
                            statusDatabase[new ComposedString(statusIt.Key.Compose().Replace(P4Util.Instance.Vars.workingDirectory + "/", ""))] = status;
                        }
                    }

                    foreach (var statusIt in fstatDB)
                    {
                        VersionControlStatus status = null;
                        ComposedString aPath = new ComposedString(statusIt.Key.Compose().Replace(P4Util.Instance.Vars.workingDirectory + "/", ""));
                        statusDatabase.TryGetValue(aPath, out status);
                        if (status == null || status.reflectionLevel == VCReflectionLevel.Pending)
                        {
                            // no previous status or previous status is pending, so set it here
                            status = statusIt.Value;
                        }
                        else
                        {
                            // probably got this status from the "status -a -e -d" command, merge it with whatever we got back from fstat
                            if (status.fileStatus == VCFileStatus.Modified && statusIt.Value.remoteStatus == VCRemoteFileStatus.Modified)
                            {
                                // we have modified locally and file is out of date with server - mark as a conflict (might not be, but at
                                // least this will raise a flag with the user to make sure they get up to date before going any further)
                                status.fileStatus = VCFileStatus.Conflicted;
                                status.treeConflictStatus = VCTreeConflictStatus.TreeConflict;
                            }
                        }
                        status.reflectionLevel = statusLevel == StatusLevel.Remote ? VCReflectionLevel.Repository : VCReflectionLevel.Local;
                        statusDatabase[aPath] = status;
                    }
                }
                lock (requestQueueLockToken)
                {
                    if (statusDB != null)
                    {
                        foreach (var assetIt in statusDB.Keys)
                        {
                            if (statusLevel == StatusLevel.Remote) remoteRequestQueue.Remove(assetIt.Compose());
                            localRequestQueue.Remove(assetIt.Compose());
                        }
                    }
                    foreach (var assetIt in fstatDB.Keys)
                    {
                        if (statusLevel == StatusLevel.Remote) remoteRequestQueue.Remove(assetIt.Compose());
                        localRequestQueue.Remove(assetIt.Compose());
                    }
                }
                OnStatusCompleted();
            }
            catch (Exception e)
            {
                DebugLog.ThrowException(e);
                return false;
            }

            return true;
        }

        public bool Status(StatusLevel statusLevel, DetailLevel detailLevel)
        {
            if (!active) return false;

            //D.Log(statusLevel.ToString() + " " + detailLevel.ToString() + " - ALL");

            string diffRootToWorking = P4Util.Instance.Vars.workingDirectory.Replace("\\", Path.DirectorySeparatorChar.ToString()).Replace(rootPath, "");

            lock (p4QueueLockToken)
            {
                // add all directories to p4 queue
                foreach (DirectoryCrawler.DirectoryData dd in dirStatus.Directories)
                {
                    string p4Path = dd.fullName.Replace("\\", "/").Replace(P4Util.Instance.Vars.workingDirectory, "");
                    if (!string.IsNullOrEmpty(p4Path) && !p4Path.EndsWith("/"))
                    {
                        p4Path = p4Path + "/";
                    }
                    if (dd.parent != null && dd.directories.Count == 0)
                    {
                        // if this is a tree leaf, get status for it and everything below it
                        p4Path = p4Path + "...";
                    }
                    else if (dd.parent == null || dd.directories.Count > 0)
                    {
                        // if this is the root or a non-leaf directory, just get its non-recursive status
                        p4Path = p4Path + "*";
                    }
                    p4OpQueue.Add(new P4QueueItem(statusLevel, "//" + P4Util.Instance.Vars.clientSpec + diffRootToWorking + "/" + p4Path));
                }
                p4OpQueue = new List<P4QueueItem>(p4OpQueue.Distinct(new P4QueueItemComparer()));
            }

            return true;
        }

        public bool Status(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            //D.Log(statusLevel.ToString());
            if (!active) return false;
            if (statusLevel == StatusLevel.Previous)
            {
                statusLevel = StatusLevel.Local;
                foreach (var assetIt in assets)
                {
                    if (GetAssetStatus(assetIt).reflectionLevel == VCReflectionLevel.Repository && statusLevel == StatusLevel.Local)
                    {
                        statusLevel = StatusLevel.Remote;
                    }
                }
            }

            SetPending(assets);

            string diffRootToWorking = P4Util.Instance.Vars.workingDirectory.Replace("\\", Path.DirectorySeparatorChar.ToString()).Replace(rootPath, "");

            lock (p4QueueLockToken)
            {
                // add all assets to p4 queue (in reverse order since we're going to be inserting them at the beginning)
                string[] _assets = assets.ToArray();
                for (int i = _assets.Length - 1; i >= 0; i--)
                {
                    string a = _assets[i];
                    if (a.StartsWith("/")) a = a.Substring(1);
                    if (Directory.Exists(rootPath + diffRootToWorking + "/" + a))
                    {
                        // only check files within the directory
                        a = a + "/*";
                    }
                    else
                    {
                        // let all scenes through so that the scene view GUI is updated as quickly as possible
                        if (!a.ToLower().EndsWith(".unity"))
                        {
                            // single file - make sure it's not already covered by a directory in the queue (or if it's a sibling of
                            // something already in there, remove that item and check the whole directory instead)
                            string qItemPath = "//" + P4Util.Instance.Vars.clientSpec + diffRootToWorking + "/" + a.Replace("//", "/");
                            int lastIndexOfSlash = qItemPath.LastIndexOf("/");
                            P4QueueItem itemToRemove = null;
                            bool found = false;
                            foreach (P4QueueItem item in p4OpQueue)
                            {
                                if (item.level == statusLevel)
                                {
                                    if (item.path.LastIndexOf("/") == lastIndexOfSlash)
                                    {
                                        // same leaf node
                                        if (item.path.Contains("*") || item.path.Contains("..."))
                                        {
                                            // already covered by a directory entry
                                            found = true;
                                            break;
                                        }
                                        else
                                        {
                                            // this is a sibling of the desired item, flag it for removal
                                            itemToRemove = item;

                                            // add the entire directory instead and continue (don't worry about duplicate adds, the
                                            // Distinct() call below will filter them out
                                            a = a.Substring(0, a.LastIndexOf("/") + 1) + "*";
                                            break;
                                        }
                                    }
                                }
                            }

                            // if we need to remove an item, do so here after we're done with the loop
                            if (itemToRemove != null) p4OpQueue.Remove(itemToRemove);

                            // don't insert if it's already in the queue
                            if (found) continue;
                        }
                    }
                    // this is a high-priority operation since specific assets are being asked for, put it at the front of the queue
                    p4OpQueue.Insert(0, new P4QueueItem(statusLevel, "//" + P4Util.Instance.Vars.clientSpec + diffRootToWorking + "/" + a.Replace("//", "/")));
                }
                p4OpQueue = new List<P4QueueItem>(p4OpQueue.Distinct(new P4QueueItemComparer()));
            }

            return true;
        }

        private bool CreateOperation(string arguments)
        {
            if (!active) return false;

            CommandLineOutput commandLineOutput;
            using (var commandLineOperation = P4Util.Instance.CreateP4CommandLine(arguments))
            {
                commandLineOperation.OutputReceived += OnProgressInformation;
                commandLineOperation.ErrorReceived += OnProgressInformation;
                commandLineOutput = P4Util.Instance.ExecuteOperation(commandLineOperation);
            }
            return !(commandLineOutput == null || commandLineOutput.Failed);
        }

        private bool CreateAssetOperation(string arguments, IEnumerable<string> assets)
        {
            if (assets == null || !assets.Any()) return true;
            bool success = true;
            foreach (String asset in assets)
            {
                success &= CreateOperation(arguments + " \"" + FormatAssetPath(asset) + "\"") && RequestStatus(new String[] { asset }, StatusLevel.Previous);
            }
            return success;
        }

        private string FormatAssetPath(string assetpath)
        {
            return assetpath
                .Replace("\\", "/")
                .Replace("%", "%25")
                .Replace("@", "%40")
                .Replace("#", "%23")
                .Replace("*", "%2A");
        }

        private static string FixAtChar(string asset)
        {
            return asset.Contains("@") ? asset + "@" : asset;
        }

        private static string ReplaceCommentChar(string commitMessage)
        {
            return commitMessage.Replace('"', '\'');
        }

        private IEnumerable<string> RemoveWorkingDirectoryFromPath(IEnumerable<string> assets)
        {
            return assets.Select(a => a.Replace(P4Util.Instance.Vars.workingDirectory, ""));
        }

        private static string ConcatAssetPaths(IEnumerable<string> assets)
        {
            assets = assets.Select(a => a.Replace("\\", "/"));
            assets = assets.Select(FixAtChar);
            if (assets.Any()) return " \"" + assets.Aggregate((i, j) => i + "\" \"" + j) + "\"";
            return "";
        }

        private void SetPending(IEnumerable<string> assets)
        {
            lock (statusDatabaseLockToken)
            {
                foreach (var assetIt in assets)
                {
                    if (GetAssetStatus(assetIt).reflectionLevel != VCReflectionLevel.Pending)
                    {
                        ComposedString asset = new ComposedString(assetIt);
                        var status = statusDatabase[asset];
                        status.reflectionLevel = VCReflectionLevel.Pending;
                        statusDatabase[asset] = status;
                    }
                }
                //D.Log("Set Pending : " + assets.Aggregate((a, b) => a + ", " + b));
            }
        }

        private void AddToRemoteStatusQueue(string asset)
        {
            //D.Log("Remote Req : " + asset);
            if (!remoteRequestQueue.Contains(asset)) remoteRequestQueue.Add(asset);
        }

        private void AddToLocalStatusQueue(string asset)
        {
            //D.Log("Local Req : " + asset);
            if (!localRequestQueue.Contains(asset)) localRequestQueue.Add(asset);
        }

        public virtual bool RequestStatus(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            if (assets == null || assets.Count() == 0) return true;

            lock (requestQueueLockToken)
            {
                foreach (string assetIt in assets)
                {
                    var currentReflectionLevel = GetAssetStatus(assetIt).reflectionLevel;
                    if (currentReflectionLevel == VCReflectionLevel.Pending) continue;
                    if (statusLevel == StatusLevel.Remote)
                    {
                        AddToRemoteStatusQueue(assetIt);
                    }
                    else if (statusLevel == StatusLevel.Local)
                    {
                        AddToLocalStatusQueue(assetIt);
                    }
                    else if (statusLevel == StatusLevel.Previous)
                    {
                        if (currentReflectionLevel == VCReflectionLevel.Repository) AddToRemoteStatusQueue(assetIt);
                        else if (currentReflectionLevel == VCReflectionLevel.Local) AddToLocalStatusQueue(assetIt);
                        else if (currentReflectionLevel == VCReflectionLevel.None) AddToLocalStatusQueue(assetIt);
                        else DebugLog.LogWarning("Unhandled previous state");
                    }
                }
            }
            SetPending(assets);
            return true;
        }
       
        public bool Update(IEnumerable<string> assets = null)
        {
            if (assets == null || !assets.Any()) assets = new[] { P4Util.Instance.Vars.workingDirectory };
            return CreateAssetOperation("update", assets);
        }
        
        public bool Update(int revision, IEnumerable<string> assets = null)
        {
            if (assets == null || !assets.Any()) assets = new[] { P4Util.Instance.Vars.workingDirectory };
            return CreateAssetOperation("update", assets);
        }

        public bool Commit(IEnumerable<string> assets, string commitMessage = "")
        {
            string arguments = "change -i";
            CommandLineOutput statusCommandLineOutput;
            string changeNum = "";

            // create a new changelist
            // TODO: custom comments
            string input = "Change: new\n\nClient: " + P4Util.Instance.Vars.clientSpec + "\n\nUser: " + P4Util.Instance.Vars.userName + "\n\nStatus: new\n\nDescription:\n\t" + commitMessage + "\n\nFiles:\n";
            using (var p4ChangeTask = P4Util.Instance.CreateP4CommandLine(arguments, input))
            {
                statusCommandLineOutput = P4Util.Instance.ExecuteOperation(p4ChangeTask);
            }

            // get the last/new changelist number
            arguments = "changes -m 1 -u " + P4Util.Instance.Vars.userName;
            using (var p4ChangesTask = P4Util.Instance.CreateP4CommandLine(arguments))
            {
                statusCommandLineOutput = P4Util.Instance.ExecuteOperation(p4ChangesTask);
                changeNum = statusCommandLineOutput.OutputStr.Substring("Change ".Length);
                changeNum = changeNum.Substring(0, changeNum.IndexOf(" ")).Trim();
            }

            if (String.IsNullOrEmpty(changeNum)) return false;

            // "reopen" files in that changelist
            bool success = true;
            foreach (var asset in assets)
            {
                success &= CreateAssetOperation("reopen -c " + changeNum, new String[] { P4Util.Instance.Vars.workingDirectory.Replace("\\", "/") + "/" + asset });
            }

            // submit the changelist
            arguments = "submit -c " + changeNum + " ";
            using (var p4SubmitTask = P4Util.Instance.CreateP4CommandLine(arguments))
            {
                statusCommandLineOutput = P4Util.Instance.ExecuteOperation(p4SubmitTask);
                success &= String.IsNullOrEmpty(statusCommandLineOutput.ErrorStr);
            }
            return success;
        }
        public virtual bool Commit(string commitMessage = "")
        {
            return true;
        }

        private void UpdateAfterOperation(IEnumerable<string> assets)
        {
            foreach (var asset in assets) AddToLocalStatusQueue(asset);
        }

        public bool Add(IEnumerable<string> assets)
        {
            bool success = CreateAssetOperation("add -f ", assets);
            if (success) UpdateAfterOperation(assets);
            return success;
        }

        public bool Revert(IEnumerable<string> assets)
        {
            // need to make sure the assets are opened for edit first...
            bool success = CreateAssetOperation("edit", assets.Where(a => statusDatabase[new ComposedString(a)].fileStatus == VCFileStatus.Normal || statusDatabase[new ComposedString(a)].fileStatus == VCFileStatus.Modified));
            success &= CreateAssetOperation("revert", assets);
            if (success) UpdateAfterOperation(assets);
            return success;
        }

        public bool Delete(IEnumerable<string> assets, OperationMode mode)
        {
            // OperationMode.Force is not supported in p4
            // TODO: can we detect when delete fails (i.e. trying to delete a file that is already opened for edit)?
            bool success = CreateAssetOperation("delete", assets);
            if (success) UpdateAfterOperation(assets);
            return success;
        }

        public bool GetLock(IEnumerable<string> assets, OperationMode mode)
        {
            // OperationMode.Force is not supported in p4
            // need to make sure the assets are opened for edit first...
            bool success = CreateAssetOperation("edit", assets.Where(a => statusDatabase[new ComposedString(a)].fileStatus == VCFileStatus.Normal || statusDatabase[new ComposedString(a)].fileStatus == VCFileStatus.Modified));
            success &= CreateAssetOperation("lock", assets);
            if (success) UpdateAfterOperation(assets);
            return success;
        }

        public bool ReleaseLock(IEnumerable<string> assets)
        {
            return CreateAssetOperation("unlock", assets);
        }

        public bool ChangeListAdd(IEnumerable<string> assets, string changelist)
        {
            return CreateAssetOperation("reopen -c " + changelist, assets);
        }

        public bool ChangeListRemove(IEnumerable<string> assets)
        {
            bool success = true;
            foreach (var asset in assets)
            {
                success &= CreateAssetOperation("reopen -c default", new String[] { asset });
            }
            return success;
        }

        public bool Checkout(string url, string path = "")
        {
            // unsupported on p4
            // CreateOperation("checkout \"" + url + "\" \"" + (path == "" ? workingDirectory : path) + "\"");
            return true;
        }

        public bool CreateBranch(string from, string to)
        {
            return true;
        }

        public bool MergeBranch(string url, string path = "")
        {
            return true;
        }

        public bool SwitchBranch(string url, string path = "")
        {
            return true;
        }

        public string GetCurrentBranch()
        {
            return null;
        }

        public virtual string GetBranchDefaultPath()
        {
            return null;
        }

        public virtual string GetTrunkPath()
        {
            return null;
        }

        public List<BranchStatus> RemoteList(string path)
        {
            return null;
        }

        public bool AllowLocalEdit(IEnumerable<string> assets)
        {
            return CreateAssetOperation("edit", assets);
        }

        public bool SetLocalOnly(IEnumerable<string> assets)
        {
            return true;
        }

        public bool Resolve(IEnumerable<string> assets, ConflictResolution conflictResolution)
        {
            if (conflictResolution == ConflictResolution.Ignore) return true;
            string conflictparameter = conflictResolution == ConflictResolution.Theirs ? "-at" : "-ay";
            bool success = CreateAssetOperation("resolve " + conflictparameter, assets);
            if (success) UpdateAfterOperation(assets);
            return success;
        }

        public bool Move(string from, string to)
        {
            bool success = CreateOperation("move \"" + from + "\" \"" + to + "\"") && RequestStatus(new[] { from, to }, StatusLevel.Previous);
            if (success) UpdateAfterOperation(new string[] { to });
            return success;
        }

        public bool SetIgnore(string path, IEnumerable<string> assets)
        {
            DebugLog.LogWarning("P4Commands.SetIgnore not implemented");
            return false;
        }

        public IEnumerable<string> GetIgnore(string path)
        {
            DebugLog.LogWarning("P4Commands.GetIgnore not implemented");
            return null;
        }

        public int GetRevision()
        {
            DebugLog.LogWarning("P4Commands.GetRevisionNumber not implemented");
            return 0;
        }

        public string GetBasePath(string assetPath)
        {
            if (string.IsNullOrEmpty(versionNumber))
            {
                versionNumber = P4Util.Instance.CreateP4CommandLine("-V").Execute().OutputStr;
                versionNumber = versionNumber.Substring(versionNumber.LastIndexOf("\n") + 1);
            }

            // base version is not stored locally - need to get base version from server into a temp path and return that path
            return "";
        }

        public bool GetConflict(string assetPath, out string basePath, out string yours, out string theirs)
        {
            DebugLog.LogWarning("P4Commands.GetConflict not implemented");
            basePath = null;
            yours = null;
            theirs = null;
            return false;
        }

        public bool CleanUp()
        {
            // no cleanup operation for p4 - doesn't need it?
            return true;//CreateOperation("cleanup");
        }

        public void ClearDatabase()
        {
            lock (statusDatabaseLockToken)
            {
                statusDatabase.Clear();
            }
        }

        public void RemoveFromDatabase(IEnumerable<string> assets)
        {
            lock (statusDatabaseLockToken)
            {
                foreach (var assetIt in assets)
                {
                    statusDatabase.Remove(new ComposedString(assetIt));
                }
            }
        }

        public event Action<string> ProgressInformation;
        private void OnProgressInformation(string info)
        {
            if (ProgressInformation != null) ProgressInformation(info);
        }

        public event Action StatusCompleted;
        private void OnStatusCompleted()
        {
            //D.Log("DB Size : " + statusDatabase.Keys.Count); // + "\n" + statusDatabase.Keys.Aggregate((a,b) => a + ", " + b)
            if (StatusCompleted != null) StatusCompleted();
        }
    }
}
