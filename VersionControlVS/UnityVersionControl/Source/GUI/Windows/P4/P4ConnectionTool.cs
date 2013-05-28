using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using CommandLineExecution;
using System;
using System.Linq;

public class P4ConnectionTool : EditorWindow {

	static P4ConnectionTool window = null;
	
	private string userName = "";
	private string password = "";
	private string clientSpec = "";
	private string port = "";
	private string rootPath = "";
    private string workingDirectory = ".";
	private string p4ConfigFile = ".p4config";
	private string p4IgnoreFile = ".gitignore"; 
	private List<string> clients = new List<string>();
	private List<string> log = new List<string>();
	private string logString = "";
	private bool clientSelected = false;
	private readonly object operationActiveLockToken = new object();
	
	private const int VISIBLE_LOG_LINES = 8;
	private const int FIELD_HEIGHT = 20;
	private const int OUTER_PADDING = 15;
	private const int TEXT_AREA_LINE_HEIGHT = 13;
	
	private bool P4Initialized {
		get { return !(String.IsNullOrEmpty(userName) || String.IsNullOrEmpty(port)); }
	}

	[MenuItem("Tools/Connect To Perforce")]
	public static void Init() {
		window = EditorWindow.GetWindow<P4ConnectionTool>();
		window.title = "P4 Connect";
		window.position = new Rect( 300.0f, 300.0f, 600.0f, 275.0f );
		window.Show();

		EditorApplication.playmodeStateChanged += () => {
			if(EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying) {
				window.SaveSettings();
			}
		};
		
		window.workingDirectory = Application.dataPath.Remove(Application.dataPath.LastIndexOf("/Assets", StringComparison.Ordinal));

        CommandLineOutput commandLineOutput;
		
		// get connection info
		bool gotP4Config = false;
        using (var p4StatusTask = window.CreateP4CommandLine("set"))
        {
			window.log.Add("CMD: " + p4StatusTask.ToString());
            commandLineOutput = window.ExecuteOperation(p4StatusTask);
			if ( !String.IsNullOrEmpty(commandLineOutput.OutputStr) ) {
				string[] output = commandLineOutput.OutputStr.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
				window.AddLogMessage("P4: " + commandLineOutput.OutputStr);
				// sample output:
				// P4CLIENT=workspace_name (set)
				// P4EDITOR=C:\Program Files (x86)\Notepad++\notepad++.exe (set)
				// P4PASSWD=password (set)
				// P4PORT=192.168.1.1:1666
				// P4USER=username
				foreach( String line in output ) {
					var cleaned = line.Trim();
					// check for/remove (set) and (config) tags
					if ( line.IndexOf("(set") != -1 ) {
						cleaned = line.Substring(0, line.IndexOf("(set")).Trim();
					}
					else if ( line.IndexOf("(config") != -1 ) {
						cleaned = line.Substring(0, line.IndexOf("(config")).Trim();
					}
	
					if ( cleaned.StartsWith("P4CLIENT=") )
					{
						window.clientSpec = cleaned.Substring( "P4CLIENT=".Length );
						//Debug.Log(window.clientSpec);
					}
					else if ( cleaned.StartsWith("P4PASSWD=") )
					{
						window.password = cleaned.Substring( "P4PASSWD=".Length );
						//Debug.Log(window.password);
					}
					else if ( cleaned.StartsWith("P4PORT=") )
					{
						window.port = cleaned.Substring( "P4PORT=".Length );
						//Debug.Log(window.port);
					}
					else if ( cleaned.StartsWith("P4USER=") )
					{
						window.userName = cleaned.Substring( "P4USER=".Length );
						//Debug.Log(window.userName);
					}
					else if ( cleaned.StartsWith("P4CONFIG=") )
					{
						window.p4ConfigFile = cleaned.Substring( "P4CONFIG=".Length );
						//Debug.Log(window.p4ConfigFile);
						gotP4Config = true;
					}
					else if ( cleaned.StartsWith("P4IGNORE=") )
					{
						window.p4IgnoreFile = cleaned.Substring( "P4IGNORE=".Length );
						//Debug.Log(window.p4IgnoreFile);
					}
				}
			}
		}
		
		if ( !gotP4Config ) {
			// set P4CONFIG
			window.P4Set("P4CONFIG", window.p4ConfigFile);
		}
		
		window.LoadSettings();
	}
	
	private void RefreshClients(string user = "") {
        CommandLineOutput commandLineOutput;
		string args = "clients";
		if ( !String.IsNullOrEmpty(user) ) {
			args = args + " -u " + user;
		}

		using (var p4ClientsTask = CreateP4CommandLine(args))
	    {
			clients.Clear();
			AddLogMessage("CMD: " + p4ClientsTask.ToString());
	        commandLineOutput = ExecuteOperation(p4ClientsTask);
			string output = commandLineOutput.OutputStr;
			AddLogMessage("P4: " + output);
			// sample output:
			// Client client1 2013/05/17 root /Users/username1/dev/ 'Created by username1. '
			// Client client2 2013/05/17 root /Users/username2/dev/ 'Created by username2. '
			// Client add-user-keys 2013/05/05 root /home/p4git/users 'Created by git-fusion-user. '
			string[] lines = output.Split(new String[] { "Client " }, StringSplitOptions.RemoveEmptyEntries);
			foreach( var line in lines ) {
				var front = line.Substring(0, line.IndexOf('/'));
				var space = front.LastIndexOf(' ');
				var client = line.Substring( 0, space ).Trim();
				clients.Add(client);
			}
		}
	}
	
	public void OnDestroy() {
		if(!EditorApplication.isPlayingOrWillChangePlaymode) {
			SaveSettings();
		}
	}
	
	private void AddLogMessage(string message) {
		string []lines = message.Split( new String[] { Environment.NewLine }, StringSplitOptions.None );
		foreach( string line in lines ) log.Add(line);
		logString = BuildLogString();
	}
	
    private CommandLine CreateP4CommandLine(string arguments, string input = null)
    {
        if (P4Initialized)
        {
            arguments = " -u " + userName
				      + (String.IsNullOrEmpty(password) ? "" : " -P " + password)
					  + (String.IsNullOrEmpty(clientSpec) ? "" : " -c " + clientSpec)
					  + " -p " + port + " " + arguments;
        }
		if (Application.platform == RuntimePlatform.OSXEditor) {
	        return new CommandLine("/usr/local/bin/p4", arguments, workingDirectory, input);
		}
        return new CommandLine("p4", arguments, workingDirectory, input);
    }

    private CommandLineOutput ExecuteCommandLine(CommandLine commandLine)
    {
        CommandLineOutput commandLineOutput;
        try
        {
            //Debug.Log(commandLine.ToString());
            commandLineOutput = commandLine.Execute();
	        return commandLineOutput;
        }
        catch (Exception e)
        {
            AddLogMessage("ERROR: Check that your commandline P4 client is installed correctly - " + e.Message);
        }
        return null;
    }

    private CommandLineOutput ExecuteOperation(CommandLine commandLine, bool useOperationLock = true)
    {
        CommandLineOutput commandLineOutput;
        if (useOperationLock)
        {
            lock (operationActiveLockToken)
            {
                commandLineOutput = ExecuteCommandLine(commandLine);
            }
        }
        else
        {
            commandLineOutput = ExecuteCommandLine(commandLine);
        }
		
		if ( !String.IsNullOrEmpty(commandLineOutput.OutputStr) ) {
	        if (commandLineOutput.Arguments.Contains("ExceptionTest.txt"))
	        {
	            AddLogMessage("ERROR: Test Exception cast due to ExceptionTest.txt being a part of arguments");
	        }
	        if (!string.IsNullOrEmpty(commandLineOutput.ErrorStr))
	        {
	            var errStr = commandLineOutput.ErrorStr;
	            if (errStr.Contains("E730060") || errStr.Contains("Unable to connect") || errStr.Contains("is unreachable") || errStr.Contains("Operation timed out") || errStr.Contains("Can't connect to"))
	                AddLogMessage("ERROR: " + errStr + " " + commandLine.ToString());
	            if (errStr.Contains("W160042") || errStr.Contains("Newer Version"))
	                AddLogMessage("ERROR: " + errStr + " " + commandLine.ToString());
	            if (errStr.Contains("W155007") || errStr.Contains("'" + workingDirectory + "'" + " is not a working copy"))
	                AddLogMessage("ERROR: " + errStr + " " + commandLine.ToString());
	            if (errStr.Contains("E160028") || errStr.Contains("is out of date"))
	                AddLogMessage("ERROR: " + errStr + " " + commandLine.ToString());
	            if (errStr.Contains("E155037") || errStr.Contains("E155004") || errStr.Contains("run 'p4 cleanup'"))
	                AddLogMessage("ERROR: " + errStr + " " + commandLine.ToString());
	            if (errStr.Contains("W160035") || errStr.Contains("is already locked by user"))
	                AddLogMessage("ERROR: " + errStr + " " + commandLine.ToString());
				AddLogMessage("ERROR: " + errStr + " " + commandLine.ToString());
	        }
		}
        return commandLineOutput;
    }
	
	string BuildLogString() {
		string fullLog = "";
		for ( int i = 0; i < log.Count; i++ ) {
			if ( i == 0 ) {
				fullLog = log[i];
			}
			else {
				fullLog = fullLog + '\n' + log[i];
			}
		}
		return fullLog;
	}

	void OnGUI () {
		int y = 5;
		Rect winDims = EditorGUILayout.BeginVertical(); {
			// server
			port = EditorGUI.TextField(new Rect(OUTER_PADDING, y, winDims.width - (2 * OUTER_PADDING), FIELD_HEIGHT), "Server:", port);
			y += FIELD_HEIGHT + 5;
			
			// username
			userName = EditorGUI.TextField(new Rect(OUTER_PADDING, y, winDims.width - (2 * OUTER_PADDING), FIELD_HEIGHT), "Username:", userName);
			y += FIELD_HEIGHT + 5;
			
			// password
			password = EditorGUI.PasswordField(new Rect(OUTER_PADDING, y, winDims.width - (2 * OUTER_PADDING), FIELD_HEIGHT), "Password:", password);
			y += FIELD_HEIGHT + 5;

			// ignore
			p4IgnoreFile = EditorGUI.TextField(new Rect(OUTER_PADDING, y, winDims.width - (2 * OUTER_PADDING), FIELD_HEIGHT), "Ignore:", p4IgnoreFile);
			y += FIELD_HEIGHT + 5;

			// client
			int clientIndex = Mathf.Max(0, clients.IndexOf(clientSpec));
			Color oldColor = GUI.color;
			if ( !clientSelected ) GUI.color = Color.red;
			clientIndex = EditorGUI.Popup(new Rect(OUTER_PADDING, y, winDims.width - (2 * OUTER_PADDING), FIELD_HEIGHT), "Workspace:", clientIndex, clients.ToArray());
			if ( clients.Count > 0 && clientIndex > -1 ) {
				clientSpec = clients[clientIndex];
			}
			y += FIELD_HEIGHT + 5;
			GUI.color = oldColor;

			EditorGUILayout.BeginHorizontal(); {
				// refresh
				int width = (int)(winDims.width / 2.0f) - OUTER_PADDING;
				if ( GUI.Button(new Rect(OUTER_PADDING, y, width, FIELD_HEIGHT), "Refresh") ) {
					clientSpec = "";
					clientSelected = false;
					LoadSettings();
				}

				// save!
				if ( GUI.Button(new Rect(width + OUTER_PADDING, y, width, FIELD_HEIGHT), "Save Settings") ) {
					SaveSettings();
				}
			} EditorGUILayout.EndHorizontal();
			y += FIELD_HEIGHT + 5;

			// log
			EditorGUI.TextArea(new Rect(OUTER_PADDING, y, winDims.width - (2 * OUTER_PADDING), TEXT_AREA_LINE_HEIGHT * VISIBLE_LOG_LINES), logString);
		} EditorGUILayout.EndVertical();
	}
	
	void LoadSettings() {
		if ( String.IsNullOrEmpty(clientSpec) ) {
			DetectClient();
		}
		else {
			var user = "";
			if ( !String.IsNullOrEmpty( userName ) ) user = userName;
			RefreshClients(user);
			AddLogMessage("INFO: Selected workspace: " + clientSpec + " from P4CONFIG");
			clientSelected = true;
		}
	}
	
	void DetectClient() {
		clientSelected = false;
		if ( P4Initialized ) {
			CommandLineOutput commandLineOutput;

			// get all clients owned by this user
			RefreshClients(userName);

			// check each client looking for a root path match with this unity project
			int matchLength = 0;
			int matchIndex = -1;
			string matchRoot = "";
			for ( int i = 0; i < clients.Count; i++ ) {
				string client = clients[i];
		        using (var p4ClientTask = CreateP4CommandLine("client -o " + client))
		        {
		            commandLineOutput = ExecuteOperation(p4ClientTask);
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
						if ( line.StartsWith( "Root:" ) ) {
							string root = line.Substring( "Root:".Length ).Trim().Replace("\\", "/");
							if ( Application.dataPath.Contains( root ) ) {
								if ( root.Length > matchLength ) {
									// found a match, log it
									matchIndex = i;
									matchLength = root.Length;
									matchRoot = root;
									break;
								}
								else if ( root.Length == matchLength ) {
									// 2 clients with same root - can't auto-detect
									AddLogMessage("WARNING: Unable to auto-detect a workspace - more than 1 workspace with the same root directory - please choose one from the drop-down menu.");
									return;
								}
							}
						}
					}
		        }
			}
			
			if ( matchIndex != -1 ) {
				// found a match, set detected info...
				clientSelected = true;
				clientSpec = clients[matchIndex];
				rootPath = matchRoot;
				AddLogMessage("INFO: Auto-detected workspace: " + clientSpec);
			}
			else {
				AddLogMessage("WARNING: Unable to auto-detect a workspace - make sure your username is set and you've created a workspace with a root at or above this Unity directory.");
			}
		}
	}
	
	void P4Set(string key, string value) {
        CommandLineOutput commandLineOutput;
        using (var p4SetTask = CreateP4CommandLine("set " + key + "=" + value))
        {
			log.Add("CMD: " + p4SetTask.ToString());
            commandLineOutput = ExecuteOperation(p4SetTask);
			if ( !String.IsNullOrEmpty(commandLineOutput.OutputStr) ) {
				AddLogMessage("P4: " + commandLineOutput.OutputStr);
			}
		}
	}
	
	void SaveSettings() {
		// use p4 set to save P4USER, P4PASSWD, and P4PORT
		if ( !String.IsNullOrEmpty(userName) ) {
			// set P4USER
			P4Set("P4USER", userName);
		}
		
		if ( !String.IsNullOrEmpty(password) ) {
			// set P4PASSWD
			P4Set("P4PASSWD", password);
		}
		
		if ( !String.IsNullOrEmpty(port) ) {
			// set P4PORT
			P4Set("P4PORT", port);
		}
		
		if ( !String.IsNullOrEmpty(p4IgnoreFile) ) {
			// set P4IGNORE
			P4Set("P4IGNORE", p4IgnoreFile);
		}
		
		// save P4CLIENT to file specified by P4CONFIG
		if ( clientSelected && !String.IsNullOrEmpty(rootPath) ) {
			string filepath = rootPath + "/" + p4ConfigFile;
			bool foundSpec = false;
			System.IO.StreamWriter fileWriter = null;
			
			if ( System.IO.File.Exists( filepath ) ) {
				// file already exists, look for P4CLIENT entry and overwrite it if found
				string []lines = System.IO.File.ReadAllLines( filepath );
				System.IO.File.Delete( filepath );
				fileWriter = System.IO.File.CreateText( filepath );
				foreach ( string line in lines ) {
					if ( !String.IsNullOrEmpty(clientSpec) && line.StartsWith( "P4CLIENT=" ) ) {
						fileWriter.WriteLine( "P4CLIENT=" + clientSpec );
						foundSpec = true;
					}
					else {
						fileWriter.WriteLine( line );
					}
				}
			}
			else {
				// no file exists, create it
				fileWriter = System.IO.File.CreateText( filepath );
			}
			
			// make sure file is valid
			if ( fileWriter == null ) AddLogMessage("ERROR: Unable to save settings to P4CONFIG file - will need to be re-entered on next launch");

			// add missing vars to the bottom
			if ( !String.IsNullOrEmpty(clientSpec) && !foundSpec ) {
				fileWriter.WriteLine( "P4CLIENT=" + clientSpec );
			}
			fileWriter.Close();
		}
	}
}
