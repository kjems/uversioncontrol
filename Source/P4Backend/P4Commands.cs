// Copyright (c) <2013> <E-Line Media, LLC>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using CommandLineExecution;

namespace VersionControl.Backend.P4
{

    public class P4Commands : MarshalByRefObject, IVersionControlCommands
    {
		private static string fstatAttributes = "clientFile,depotFile,movedFile,shelved,headRev,haveRev,action,actionOwner,change,otherOpen,otherOpen0,otherLock,ourLock";
		private string rootPath = "";
        private string versionNumber;
		private Dictionary<string, string> depotToDir = null;
        private readonly StatusDatabase statusDatabase = new StatusDatabase();
        private bool OperationActive { get { return currentExecutingOperation != null; } }
        private CommandLine currentExecutingOperation = null;
        private Thread refreshThread = null;
        private readonly object requestQueueLockToken = new object();
        private readonly object statusDatabaseLockToken = new object();
        private readonly List<string> localRequestQueue = new List<string>();
        private readonly List<string> remoteRequestQueue = new List<string>();
        private volatile bool active = false;
        private volatile bool refreshLoopActive = false;
        private volatile bool requestRefreshLoopStop = false;
		
        public P4Commands(string cliEnding = "")
        {
			P4Util.Instance.Vars.cliEnding = cliEnding;
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
                    Thread.Sleep(200);

					// make sure p4 data is initialized
					if ( P4Util.Instance.P4Initialized || InitializeP4Connection() )
					{
						// refresh status
	                    if (active && refreshLoopActive) RefreshStatusDatabase();
					}
                }
            }
            catch (ThreadAbortException) { }
            catch (AppDomainUnloadedException) { }
            catch (Exception e)
            {
                D.ThrowException(e);
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
            refreshLoopActive = false;
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

        public void ActivateRefreshLoop()
        {
            refreshLoopActive = true;
        }

        public void DeactivateRefreshLoop()
        {
            refreshLoopActive = false;
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
				var lines = output.Split( new Char[] { '\r', '\n' } );
				foreach( String line in lines ) {
					//D.Log( line );
					if ( line.StartsWith( "Root:" ) ) {
						rootPath = line.Substring( "Root:".Length ).Trim().Replace("\\", "/");
					}
					else if ( line.Trim().StartsWith( "//" ) ) {
						string repoPath = line.Substring( 0, line.IndexOf("...") ).Trim();
						//D.Log( "Repo Path: " + repoPath );
						int clientPathStart = repoPath.Length + "...".Length + 1;
						string clientPath = line.Substring( clientPathStart, line.IndexOf("...", clientPathStart) - clientPathStart ).Trim();
						//D.Log( "Client Path: " + clientPath );
						string localPath = clientPath.Replace( "//" + P4Util.Instance.Vars.clientSpec, rootPath );
						//D.Log( "Local Path: " + localPath );
						depotToDir.Add( repoPath, localPath );
					}
				}
            }

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
        }

        public void SetUserCredentials(string userName, string password)
        {
            if (!string.IsNullOrEmpty(userName)) P4Util.Instance.Vars.userName = userName;
            if (!string.IsNullOrEmpty(password)) P4Util.Instance.Vars.password = password;
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
                return new List<VersionControlStatus>(statusDatabase.Values.Where(filter));
            }
        }

        public bool Status(StatusLevel statusLevel, DetailLevel detailLevel)
        {
            if (!active) return false;
			
            string arguments = "status -a -e -d ";
//            if (statusLevel == StatusLevel.Remote) arguments += " -u";
//            if (detailLevel == DetailLevel.Verbose) arguments += " -v";

            CommandLineOutput statusCommandLineOutput;
            using (var p4StatusTask = P4Util.Instance.CreateP4CommandLine(arguments))
            {
                statusCommandLineOutput = P4Util.Instance.ExecuteOperation(p4StatusTask);
            }
			
            arguments = "fstat -T " + fstatAttributes + " //" + P4Util.Instance.Vars.clientSpec + "/...";
            CommandLineOutput fstatCommandLineOutput;
            using (var p4FstatTask = P4Util.Instance.CreateP4CommandLine(arguments))
            {
                fstatCommandLineOutput = P4Util.Instance.ExecuteOperation(p4FstatTask);
            }

			if (statusCommandLineOutput == null || statusCommandLineOutput.Failed || string.IsNullOrEmpty(statusCommandLineOutput.OutputStr) || !active) return false;
			if (fstatCommandLineOutput == null || fstatCommandLineOutput.Failed || string.IsNullOrEmpty(fstatCommandLineOutput.OutputStr) || !active) return false;
            try
            {
                var statusDB = P4StatusParser.P4ParseStatus(statusCommandLineOutput.OutputStr, P4Util.Instance.Vars.userName);
                var fstatDB = P4StatusParser.P4ParseFstat(fstatCommandLineOutput.OutputStr, P4Util.Instance.Vars.workingDirectory);
                lock (statusDatabaseLockToken)
                {
                    foreach (var statusIt in statusDB)
                    {
                        var status = statusIt.Value;
                        status.reflectionLevel = statusLevel == StatusLevel.Remote ? VCReflectionLevel.Repository : VCReflectionLevel.Local;
                        statusDatabase[statusIt.Key] = status;
                    }

                    foreach (var statusIt in fstatDB)
                    {
                        VersionControlStatus status = null;
						if ( !statusDatabase.TryGetValue(statusIt.Key, out status) || status.reflectionLevel == VCReflectionLevel.Pending ) {
							status = statusIt.Value;
	                        status.reflectionLevel = statusLevel == StatusLevel.Remote ? VCReflectionLevel.Repository : VCReflectionLevel.Local;
	                        statusDatabase[statusIt.Key] = status;
						}
                    }
				}
                lock (requestQueueLockToken)
                {
                    foreach (var assetIt in statusDB.Keys)
                    {
                        if (statusLevel == StatusLevel.Remote) remoteRequestQueue.Remove(assetIt.ToString());
                        localRequestQueue.Remove(assetIt.ToString());
                    }
                }
                OnStatusCompleted();
            }
            catch (Exception e)
            {
                D.ThrowException(e);
                return false;
            }
            return true;
        }

        public bool Status(IEnumerable<string> assets, StatusLevel statusLevel)
        {
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

            if (statusLevel == StatusLevel.Remote) assets = RemoveFilesIfParentFolderInList(assets);
            const int assetsPerStatus = 20;
            if (assets.Count() > assetsPerStatus)
            {
                return Status(assets.Take(assetsPerStatus), statusLevel) && Status(assets.Skip(assetsPerStatus), statusLevel);
            }

            string arguments = "status -a -e -d";//--xml -q -v ";
//            if (statusLevel == StatusLevel.Remote) arguments += "-u ";
//            else arguments += " --depth=empty ";
//            arguments += ConcatAssetPaths(RemoveWorkingDirectoryFromPath(assets));

            SetPending(assets);

            CommandLineOutput statusCommandLineOutput;
            using (var p4StatusTask = P4Util.Instance.CreateP4CommandLine(arguments))
            {
                statusCommandLineOutput = P4Util.Instance.ExecuteOperation(p4StatusTask);
            }

            arguments = "fstat -T " + fstatAttributes + " //" + P4Util.Instance.Vars.clientSpec + "/...";
            CommandLineOutput fstatCommandLineOutput;
            using (var p4FstatTask = P4Util.Instance.CreateP4CommandLine(arguments))
            {
                fstatCommandLineOutput = P4Util.Instance.ExecuteOperation(p4FstatTask);
            }

			if (statusCommandLineOutput == null || statusCommandLineOutput.Failed || string.IsNullOrEmpty(statusCommandLineOutput.OutputStr) || !active) return false;
			if (fstatCommandLineOutput == null || fstatCommandLineOutput.Failed || string.IsNullOrEmpty(fstatCommandLineOutput.OutputStr) || !active) return false;
            try
            {
                var statusDB = P4StatusParser.P4ParseStatus(statusCommandLineOutput.OutputStr, P4Util.Instance.Vars.userName);
                var fstatDB = P4StatusParser.P4ParseFstat(fstatCommandLineOutput.OutputStr, P4Util.Instance.Vars.workingDirectory);
                lock (statusDatabaseLockToken)
                {
                    foreach (var statusIt in statusDB)
                    {
                        var status = statusIt.Value;
                        status.reflectionLevel = statusLevel == StatusLevel.Remote ? VCReflectionLevel.Repository : VCReflectionLevel.Local;
                        statusDatabase[statusIt.Key] = status;
                    }

                    foreach (var statusIt in fstatDB)
                    {
                        VersionControlStatus status = null;
						if ( !statusDatabase.TryGetValue(statusIt.Key, out status) || status.reflectionLevel == VCReflectionLevel.Pending ) {
							status = statusIt.Value;
	                        status.reflectionLevel = statusLevel == StatusLevel.Remote ? VCReflectionLevel.Repository : VCReflectionLevel.Local;
	                        statusDatabase[statusIt.Key] = status;
						}
                    }
                }
                lock (requestQueueLockToken)
                {
                    foreach (var assetIt in statusDB.Keys)
                    {
                        if (statusLevel == StatusLevel.Remote) remoteRequestQueue.Remove(assetIt.ToString());
                        localRequestQueue.Remove(assetIt.ToString());
                    }
                }
                OnStatusCompleted();
            }
            catch (Exception e)
            {
                D.ThrowException(e);
                return false;
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
			foreach( String asset in assets ) {
				success &= CreateOperation(arguments + " \"" + asset + "\"") && RequestStatus(new String[] { asset }, StatusLevel.Previous);
			}
			return success;
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
                        var status = statusDatabase[assetIt];
                        status.reflectionLevel = VCReflectionLevel.Pending;
                        statusDatabase[assetIt] = status;
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
                        else D.LogWarning("Unhandled previous state");
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
				changeNum = statusCommandLineOutput.OutputStr.Substring( "Change ".Length );
				changeNum = changeNum.Substring( 0, changeNum.IndexOf( " " ) ).Trim();
            }

			if ( String.IsNullOrEmpty(changeNum) ) return false;

			// "reopen" files in that changelist
			bool success = true;
			foreach( var asset in assets ) {
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

        public bool Add(IEnumerable<string> assets)
        {
            return CreateAssetOperation("add", assets);
        }

        public bool Revert(IEnumerable<string> assets)
        {
            return CreateAssetOperation("revert", assets);
        }

        public bool Delete(IEnumerable<string> assets, OperationMode mode)
        {
			// OperationMode.Force is not supported in p4
			// TODO: can we detect when delete fails (i.e. trying to delete a file that is already opened for edit)?
            return CreateAssetOperation("delete", assets);
        }

        public bool GetLock(IEnumerable<string> assets, OperationMode mode)
        {
			// OperationMode.Force is not supported in p4
			// need to make sure the assets are opened for edit first...
			bool success = CreateAssetOperation("edit", assets.Where( a => statusDatabase[a].fileStatus == VCFileStatus.Normal ));
            return success & CreateAssetOperation("lock", assets);
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
			foreach( var asset in assets ) {
				success &= CreateAssetOperation("reopen -c default", new String[] { asset } );
			}
			return success;
        }

        public bool Checkout(string url, string path = "")
        {
			// unsupported on p4
            // CreateOperation("checkout \"" + url + "\" \"" + (path == "" ? workingDirectory : path) + "\"");
			return true;
        }

        public bool Resolve(IEnumerable<string> assets, ConflictResolution conflictResolution)
        {
            if (conflictResolution == ConflictResolution.Ignore) return true;
            string conflictparameter = conflictResolution == ConflictResolution.Theirs ? "-at" : "-ay";
            return CreateAssetOperation("resolve " + conflictparameter, assets);
        }

        public bool Move(string from, string to)
        {
            return CreateOperation("move \"" + from + "\" \"" + to + "\"") && RequestStatus(new[] { from, to }, StatusLevel.Previous);
        }

        public string GetBasePath(string assetPath)
        {
            if (string.IsNullOrEmpty(versionNumber))
            {
                versionNumber = P4Util.Instance.CreateP4CommandLine("-V").Execute().OutputStr;
				versionNumber = versionNumber.Substring(versionNumber.LastIndexOf("\n")+1);
            }
			
			// base version is not stored locally - need to get base version from server into a temp path and return that path
            return "";
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
                    statusDatabase.Remove(assetIt);
                }
            }
        }

        IEnumerable<string> RemoveFilesIfParentFolderInList(IEnumerable<string> assets)
        {
            var folders = assets.Where(a => Directory.Exists(a));
            assets = assets.Where(a => !folders.Any(f => a.StartsWith(f) && a != f));
            return assets.ToArray();
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
