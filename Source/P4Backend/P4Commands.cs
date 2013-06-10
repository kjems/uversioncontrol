// Copyright (c) <2013> <E-Line Media, LLC>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using CommandLineExecution;
using System.Timers;

namespace VersionControl.Backend.P4
{

    public class P4Commands : MarshalByRefObject, IVersionControlCommands
    {
		private static string fstatAttributes = "clientFile,depotFile,movedFile,shelved,headRev,haveRev,action,actionOwner,change,otherOpen,otherOpen0,otherLock,ourLock";
		private string rootPath = "";
        private string versionNumber;
		private Dictionary<string, string> depotToDir = null;
		private bool localStatusDirty = true;
		private bool remoteStatusDirty = true;
        private readonly StatusDatabase statusDatabase = new StatusDatabase();
        private bool OperationActive { get { return currentExecutingOperation != null; } }
        private CommandLine currentExecutingOperation = null;
        private Thread refreshThread = null;
		private System.Timers.Timer remoteRefreshTimer = null;
        private readonly object requestQueueLockToken = new object();
        private readonly object statusDatabaseLockToken = new object();
        private readonly List<string> localRequestQueue = new List<string>();
        private readonly List<string> remoteRequestQueue = new List<string>();
        private volatile bool active = false;
        private volatile bool refreshLoopActive = false;
        private volatile bool requestRefreshLoopStop = false;
        private FileSystemWatcher assetsWatcher = null;
		
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
			if ( remoteRefreshTimer != null ) {
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
			
			// only allow refreshing remote status every 15 seconds
			remoteRefreshTimer = new System.Timers.Timer(15000);

			// Hook up the Elapsed event for the timer.
			remoteRefreshTimer.Elapsed += new ElapsedEventHandler(OnTimerExpired);
			
			remoteRefreshTimer.Start();
			remoteRefreshTimer.Enabled = true;
			remoteRefreshTimer.AutoReset = true;
        }

	    private void OnTimerExpired(object source, ElapsedEventArgs e)
	    {
			remoteStatusDirty = true;
			Status( StatusLevel.Remote, DetailLevel.Normal );
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

		// Define the event handlers. 
	    private void OnAssetsChanged(object source, FileSystemEventArgs e)
	    {
			AddToLocalStatusQueue( e.FullPath );
			localStatusDirty = true;
	    }
	
	    private void OnAssetsRenamed(object source, RenamedEventArgs e)
	    {
			AddToLocalStatusQueue( e.FullPath );
			localStatusDirty = true;
	    }
			
        public void SetWorkingDirectory(string workingDirectory)
        {
            P4Util.Instance.Vars.workingDirectory = workingDirectory;
			if ( assetsWatcher != null ) {
		        // Stop watching.
		        assetsWatcher.EnableRaisingEvents = false;

				// Remove event handlers.
		        assetsWatcher.Changed -= new FileSystemEventHandler(OnAssetsChanged);
		        assetsWatcher.Created -= new FileSystemEventHandler(OnAssetsChanged);
		        assetsWatcher.Deleted -= new FileSystemEventHandler(OnAssetsChanged);
		        assetsWatcher.Renamed -= new RenamedEventHandler(OnAssetsRenamed);
				
				assetsWatcher = null;
			}

			assetsWatcher = new FileSystemWatcher(workingDirectory);

			// Add event handlers.
	        assetsWatcher.Changed += new FileSystemEventHandler(OnAssetsChanged);
	        assetsWatcher.Created += new FileSystemEventHandler(OnAssetsChanged);
	        assetsWatcher.Deleted += new FileSystemEventHandler(OnAssetsChanged);
	        assetsWatcher.Renamed += new RenamedEventHandler(OnAssetsRenamed);
	
	        // Begin watching.
	        assetsWatcher.EnableRaisingEvents = true;        
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
		
		public bool GetStatus(StatusLevel statusLevel, string fstatArgs, IEnumerable<string> assets = null)
		{
			// if nothing has changed locally or our minimum refresh time hasn't expired, don't do checks again
			if ( (statusLevel == StatusLevel.Local && !localStatusDirty) || (statusLevel == StatusLevel.Remote && !remoteStatusDirty) ) {
				if ( assets == null ) return true;
				
				// make sure all assets are already in the database
				bool missedOne = false;
				foreach( var asset in assets ) {
					VersionControlStatus status = null;
					if ( !statusDatabase.TryGetValue( new ComposedString(asset), out status ) ) {
						missedOne = true;
					}
					else if ( (status.reflectionLevel == VCReflectionLevel.Local && statusLevel != StatusLevel.Local)
						   || (status.reflectionLevel == VCReflectionLevel.Repository && statusLevel != StatusLevel.Remote)
						   || (status.reflectionLevel == VCReflectionLevel.Pending) ) {
						missedOne = true;
					}
				}

				if ( !missedOne) return true;
			}

			string arguments = "status -aed ";
//            if (statusLevel == StatusLevel.Remote) arguments += " -u";
//            if (detailLevel == DetailLevel.Verbose) arguments += " -v";

            CommandLineOutput statusCommandLineOutput = null;
			if ( statusLevel == StatusLevel.Local ) {
	            using (var p4StatusTask = P4Util.Instance.CreateP4CommandLine(arguments))
	            {
	                statusCommandLineOutput = P4Util.Instance.ExecuteOperation(p4StatusTask);
	            }
			}
			
            arguments = fstatArgs;
            CommandLineOutput fstatCommandLineOutput;
            using (var p4FstatTask = P4Util.Instance.CreateP4CommandLine(arguments))
            {
                fstatCommandLineOutput = P4Util.Instance.ExecuteOperation(p4FstatTask);
            }

			if ((statusCommandLineOutput != null && ( string.IsNullOrEmpty(statusCommandLineOutput.OutputStr) || statusCommandLineOutput.Failed )) || !active) return false;
			if (fstatCommandLineOutput == null || fstatCommandLineOutput.Failed || string.IsNullOrEmpty(fstatCommandLineOutput.OutputStr) || !active) return false;
            try
            {
//				if ( statusCommandLineOutput != null ) D.Log(statusCommandLineOutput.OutputStr);
//				D.Log("");
//				D.Log(fstatCommandLineOutput.OutputStr);
                var statusDB = statusCommandLineOutput != null ? P4StatusParser.P4ParseStatus(statusCommandLineOutput.OutputStr, P4Util.Instance.Vars.userName) : null;
                var fstatDB = P4StatusParser.P4ParseFstat(fstatCommandLineOutput.OutputStr, P4Util.Instance.Vars.workingDirectory);
                lock (statusDatabaseLockToken)
                {
					if ( statusDB != null ) {
	                    foreach (var statusIt in statusDB)
	                    {
	                        var status = statusIt.Value;
	                        status.reflectionLevel = statusLevel == StatusLevel.Remote ? VCReflectionLevel.Repository : VCReflectionLevel.Local;
	                        statusDatabase[statusIt.Key] = status;
	                    }
					}

                    foreach (var statusIt in fstatDB)
                    {
                        VersionControlStatus status = null;
						statusDatabase.TryGetValue(statusIt.Key, out status);
						if ( status == null || status.reflectionLevel == VCReflectionLevel.Pending ) {
							// no previous status or previous status is pending, so set it here
							status = statusIt.Value;
						} else {
							// probably got this status from the "status -a -e -d" command, merge it with whatever we got back from fstat
							if ( status.fileStatus == VCFileStatus.Modified && statusIt.Value.remoteStatus == VCRemoteFileStatus.Modified ) {
								// we have modified locally and file is out of date with server - mark as a conflict (might not be, but at
								// least this will raise a flag with the user to make sure they get up to date before going any further)
								status.fileStatus = VCFileStatus.Conflicted;
								status.treeConflictStatus = VCTreeConflictStatus.TreeConflict;
							}
						}
                        status.reflectionLevel = statusLevel == StatusLevel.Remote ? VCReflectionLevel.Repository : VCReflectionLevel.Local;
                        statusDatabase[statusIt.Key] = status;
                    }
				}
                lock (requestQueueLockToken)
                {
					if ( statusDB != null ) {
	                    foreach (var assetIt in statusDB.Keys)
	                    {
	                        if (statusLevel == StatusLevel.Remote) remoteRequestQueue.Remove(assetIt.ToString());
	                        localRequestQueue.Remove(assetIt.ToString());
	                    }
					}
                    foreach (var assetIt in fstatDB.Keys)
                    {
                        if (statusLevel == StatusLevel.Remote) remoteRequestQueue.Remove(assetIt.ToString());
                        localRequestQueue.Remove(assetIt.ToString());
                    }
                }
				if ( assets != null ) {
	                lock (statusDatabaseLockToken)
	                {
						// make sure there's a status item for each asset requested
						foreach ( var assetIt in assets ) {
	                        VersionControlStatus status = null;
							ComposedString asset = new ComposedString(assetIt);
							if ( !statusDatabase.TryGetValue(asset, out status) || status.reflectionLevel == VCReflectionLevel.Pending ) {
								//D.Log( "No status for " + assetIt + ", adding a default one..." );
								status = new VersionControlStatus();
				                status.assetPath = asset;
								if ( !Directory.Exists( P4Util.Instance.Vars.workingDirectory + "/" + assetIt ) ) {
									// this is not a directory, it's a file, so it must be ignored if we didn't get a status for it
									status.fileStatus = VCFileStatus.Ignored;
									//D.Log( "Ignoring " + status.assetPath.ToString() );
								}
								status.reflectionLevel = statusLevel == StatusLevel.Remote ? VCReflectionLevel.Repository : VCReflectionLevel.Local;
								statusDatabase[asset] = status;
							}
						}
					}
				}
                OnStatusCompleted();
            }
            catch (Exception e)
            {
                D.ThrowException(e);
                return false;
            }

			// clear dirty statuses
			localStatusDirty = false;
			remoteStatusDirty = false;

			return true;
		}

        public bool Status(StatusLevel statusLevel, DetailLevel detailLevel)
        {
            if (!active) return false;

//			D.Log(statusLevel.ToString() + " " + detailLevel.ToString());

			return GetStatus(statusLevel, "fstat -T " + fstatAttributes + " //" + P4Util.Instance.Vars.clientSpec + "/...", null);
        }

        public bool Status(IEnumerable<string> assets, StatusLevel statusLevel)
        {
//			D.Log(statusLevel.ToString());
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

			return GetStatus(statusLevel, "fstat -T " + fstatAttributes + " //" + P4Util.Instance.Vars.clientSpec + "/...", assets);
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
				localStatusDirty = true;
				remoteStatusDirty = true;
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
