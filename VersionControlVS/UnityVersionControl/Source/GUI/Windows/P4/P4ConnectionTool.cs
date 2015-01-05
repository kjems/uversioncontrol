/*P4_DISABLED
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using CommandLineExecution;
using System;
using System.Linq;
using VersionControl.Backend.P4;

public class P4ConnectionTool : EditorWindow
{

    static P4ConnectionTool window = null;

    private string rootPath = "";
    private List<string> clients = new List<string>();
    private List<string> log = new List<string>();
    private string logString = "";
    private bool clientSelected = false;

    private const int VISIBLE_LOG_LINES = 8;
    private const int FIELD_HEIGHT = 20;
    private const int OUTER_PADDING = 15;
    private const int TEXT_AREA_LINE_HEIGHT = 13;

    [MenuItem("Window/UVC/Connect To.../Perforce")]
    public static void Init()
    {
        window = EditorWindow.GetWindow<P4ConnectionTool>();
        window.title = "P4 Connect";
        window.position = new Rect(300.0f, 300.0f, 600.0f, 275.0f);
        window.Show();

        EditorApplication.playmodeStateChanged += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
            {
                window.SaveSettings();
            }
        };

        P4Util.Instance.Vars.workingDirectory = Application.dataPath.Remove(Application.dataPath.LastIndexOf("/Assets", StringComparison.Ordinal));

        string oldP4Config = P4Util.Instance.Vars.configFile;
        P4Util.Instance.InitVars();

        if (P4Util.Instance.Vars.configFile != oldP4Config)
        {
            // set P4CONFIG
            P4Util.Instance.P4Set("P4CONFIG", P4Util.Instance.Vars.configFile);
        }

        window.LoadSettings();
    }

    private void RefreshClients(string user = "")
    {
        CommandLineOutput commandLineOutput;
        string args = "clients";
        if (!String.IsNullOrEmpty(user))
        {
            args = args + " -u " + user;
        }

        using (var p4ClientsTask = P4Util.Instance.CreateP4CommandLine(args))
        {
            clients.Clear();
            AddLogMessage("CMD: " + p4ClientsTask.ToString());
            commandLineOutput = P4Util.Instance.ExecuteOperation(p4ClientsTask);
            string output = commandLineOutput.OutputStr;
            AddLogMessage("P4: " + output);
            // sample output:
            // Client client1 2013/05/17 root /Users/username1/dev/ 'Created by username1. '
            // Client client2 2013/05/17 root /Users/username2/dev/ 'Created by username2. '
            // Client add-user-keys 2013/05/05 root /home/p4git/users 'Created by git-fusion-user. '
            string[] lines = output.Split(new String[] { "Client " }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var front = line.Substring(0, line.IndexOf('/'));
                var space = front.LastIndexOf(' ');
                var client = line.Substring(0, space).Trim();
                clients.Add(client);
            }
        }
    }

    public void OnDestroy()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SaveSettings();
        }
    }

    private void AddLogMessage(string message)
    {
        string[] lines = message.Split(new String[] { Environment.NewLine }, StringSplitOptions.None);
        foreach (string line in lines) log.Add(line);
        logString = BuildLogString();
    }

    string BuildLogString()
    {
        string fullLog = "";
        for (int i = 0; i < log.Count; i++)
        {
            if (i == 0)
            {
                fullLog = log[i];
            }
            else
            {
                fullLog = fullLog + '\n' + log[i];
            }
        }
        return fullLog;
    }

    void OnGUI()
    {
        int y = 5;
        Rect winDims = EditorGUILayout.BeginVertical();
        {
            // server
            P4Util.Instance.Vars.port = EditorGUI.TextField(new Rect(OUTER_PADDING, y, winDims.width - (2 * OUTER_PADDING), FIELD_HEIGHT), "Server:", P4Util.Instance.Vars.port);
            y += FIELD_HEIGHT + 5;

            // username
            P4Util.Instance.Vars.userName = EditorGUI.TextField(new Rect(OUTER_PADDING, y, winDims.width - (2 * OUTER_PADDING), FIELD_HEIGHT), "Username:", P4Util.Instance.Vars.userName);
            y += FIELD_HEIGHT + 5;

            // password
            P4Util.Instance.Vars.password = EditorGUI.PasswordField(new Rect(OUTER_PADDING, y, winDims.width - (2 * OUTER_PADDING), FIELD_HEIGHT), "Password:", P4Util.Instance.Vars.password);
            y += FIELD_HEIGHT + 5;

            // ignore
            P4Util.Instance.Vars.ignoreFile = EditorGUI.TextField(new Rect(OUTER_PADDING, y, winDims.width - (2 * OUTER_PADDING), FIELD_HEIGHT), "Ignore:", P4Util.Instance.Vars.ignoreFile);
            y += FIELD_HEIGHT + 5;

            // client
            int clientIndex = Mathf.Max(0, clients.IndexOf(P4Util.Instance.Vars.clientSpec));
            Color oldColor = GUI.color;
            if (!clientSelected) GUI.color = Color.red;
            clientIndex = EditorGUI.Popup(new Rect(OUTER_PADDING, y, winDims.width - (2 * OUTER_PADDING), FIELD_HEIGHT), "Workspace:", clientIndex, clients.ToArray());
            if (clients.Count > 0 && clientIndex > -1)
            {
                P4Util.Instance.Vars.clientSpec = clients[clientIndex];
            }
            y += FIELD_HEIGHT + 5;
            GUI.color = oldColor;

            EditorGUILayout.BeginHorizontal();
            {
                // refresh
                int width = (int)(winDims.width / 2.0f) - OUTER_PADDING;
                if (GUI.Button(new Rect(OUTER_PADDING, y, width, FIELD_HEIGHT), "Refresh"))
                {
                    P4Util.Instance.Vars.clientSpec = "";
                    clientSelected = false;
                    LoadSettings();
                }

                // save!
                if (GUI.Button(new Rect(width + OUTER_PADDING, y, width, FIELD_HEIGHT), "Save Settings"))
                {
                    SaveSettings();
                }
            } EditorGUILayout.EndHorizontal();
            y += FIELD_HEIGHT + 5;

            // log
            EditorGUI.TextArea(new Rect(OUTER_PADDING, y, winDims.width - (2 * OUTER_PADDING), TEXT_AREA_LINE_HEIGHT * VISIBLE_LOG_LINES), logString);
        } EditorGUILayout.EndVertical();
    }

    void LoadSettings()
    {
        if (String.IsNullOrEmpty(P4Util.Instance.Vars.clientSpec))
        {
            DetectClient();
        }
        else
        {
            var user = "";
            if (!String.IsNullOrEmpty(P4Util.Instance.Vars.userName)) user = P4Util.Instance.Vars.userName;
            RefreshClients(user);
            AddLogMessage("INFO: Selected workspace: " + P4Util.Instance.Vars.clientSpec + " from P4CONFIG");
            clientSelected = true;
        }
    }

    void DetectClient()
    {
        clientSelected = false;
        if (P4Util.Instance.P4Initialized)
        {
            CommandLineOutput commandLineOutput;

            // get all clients owned by this user
            RefreshClients(P4Util.Instance.Vars.userName);

            // check each client looking for a root path match with this unity project
            int matchLength = 0;
            int matchIndex = -1;
            string matchRoot = "";
            for (int i = 0; i < clients.Count; i++)
            {
                string client = clients[i];
                using (var p4ClientTask = P4Util.Instance.CreateP4CommandLine("client -o " + client))
                {
                    commandLineOutput = P4Util.Instance.ExecuteOperation(p4ClientTask);
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
                        if (line.StartsWith("Root:"))
                        {
                            string root = line.Substring("Root:".Length).Trim().Replace("\\", "/");
                            if (Application.dataPath.Contains(root))
                            {
                                if (root.Length > matchLength)
                                {
                                    // found a match, log it
                                    matchIndex = i;
                                    matchLength = root.Length;
                                    matchRoot = root;
                                    break;
                                }
                                else if (root.Length == matchLength)
                                {
                                    // 2 clients with same root - can't auto-detect
                                    AddLogMessage("WARNING: Unable to auto-detect a workspace - more than 1 workspace with the same root directory - please choose one from the drop-down menu.");
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            if (matchIndex != -1)
            {
                // found a match, set detected info...
                clientSelected = true;
                P4Util.Instance.Vars.clientSpec = clients[matchIndex];
                rootPath = matchRoot;
                AddLogMessage("INFO: Auto-detected workspace: " + P4Util.Instance.Vars.clientSpec);
            }
            else
            {
                AddLogMessage("WARNING: Unable to auto-detect a workspace - make sure your username is set and you've created a workspace with a root at or above this Unity directory.");
            }
        }
    }

    void SaveSettings()
    {
        // use p4 set to save P4USER, P4PASSWD, and P4PORT
        if (!String.IsNullOrEmpty(P4Util.Instance.Vars.userName))
        {
            // set P4USER
            P4Util.Instance.P4Set("P4USER", P4Util.Instance.Vars.userName);
        }

        if (!String.IsNullOrEmpty(P4Util.Instance.Vars.password))
        {
            // set P4PASSWD
            P4Util.Instance.P4Set("P4PASSWD", P4Util.Instance.Vars.password);
        }

        if (!String.IsNullOrEmpty(P4Util.Instance.Vars.port))
        {
            // set P4PORT
            P4Util.Instance.P4Set("P4PORT", P4Util.Instance.Vars.port);
        }

        if (!String.IsNullOrEmpty(P4Util.Instance.Vars.ignoreFile))
        {
            // set P4IGNORE
            P4Util.Instance.P4Set("P4IGNORE", P4Util.Instance.Vars.ignoreFile);
        }

        // save P4CLIENT to file specified by P4CONFIG
        if (clientSelected && !String.IsNullOrEmpty(rootPath))
        {
            string filepath = rootPath + "/" + P4Util.Instance.Vars.configFile;
            bool foundSpec = false;
            System.IO.StreamWriter fileWriter = null;

            if (System.IO.File.Exists(filepath))
            {
                // file already exists, look for P4CLIENT entry and overwrite it if found
                string[] lines = System.IO.File.ReadAllLines(filepath);
                System.IO.File.Delete(filepath);
                fileWriter = System.IO.File.CreateText(filepath);
                foreach (string line in lines)
                {
                    if (!String.IsNullOrEmpty(P4Util.Instance.Vars.clientSpec) && line.StartsWith("P4CLIENT="))
                    {
                        fileWriter.WriteLine("P4CLIENT=" + P4Util.Instance.Vars.clientSpec);
                        foundSpec = true;
                    }
                    else
                    {
                        fileWriter.WriteLine(line);
                    }
                }
            }
            else
            {
                // no file exists, create it
                fileWriter = System.IO.File.CreateText(filepath);
            }

            // make sure file is valid
            if (fileWriter == null) AddLogMessage("ERROR: Unable to save settings to P4CONFIG file - will need to be re-entered on next launch");

            // add missing vars to the bottom
            if (!String.IsNullOrEmpty(P4Util.Instance.Vars.clientSpec) && !foundSpec)
            {
                fileWriter.WriteLine("P4CLIENT=" + P4Util.Instance.Vars.clientSpec);
            }
            fileWriter.Close();
        }
    }
}*/
