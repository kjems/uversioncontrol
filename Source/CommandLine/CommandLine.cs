// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Collections.Generic;

namespace CommandLineExecution
{
    public sealed class CommandLineOutput
    {
        public CommandLineOutput(string command, string arguments, string outputStr, string errorStr, int exitcode)
        {
            Command = command;
            Arguments = arguments;
            OutputStr = outputStr;
            ErrorStr = errorStr;
            Exitcode = exitcode;
        }

        public string Command { get; private set; }
        public string Arguments { get; private set; }
        public string OutputStr { get; private set; }
        public string ErrorStr { get; private set; }
        public int Exitcode { get; private set; }
        public bool Failed { get { return (Exitcode != 0 || !string.IsNullOrEmpty(ErrorStr)); } }
    }



    public sealed class CommandLine : IDisposable
    {
        public CommandLine(
            string command, 
            string arguments, 
            string workingDirectory, 
            string input = null, 
            string cliEnding = "", 
            Dictionary<string, string> _envVars = null, 
            Encoding desiredEncoding = null
            )
        {
            this.command = command;
            this.arguments = arguments;
            this.workingDirectory = workingDirectory;
            this.input = input;
            this.cliEnding = cliEnding;
            if(desiredEncoding != null) this.desiredEncoding = desiredEncoding;
            if (_envVars != null) this.envVars = new Dictionary<string, string>(_envVars);
            AppDomain.CurrentDomain.DomainUnload += Unload;
            AppDomain.CurrentDomain.ProcessExit += Unload;
        }

        private void Unload(object sender, EventArgs args)
        {
            AbortProcess();
        }

        private void AbortProcess()
        {
            if (!aborted && process != null && !process.HasExited)
            {
                aborted = true;
                process.Kill();
                process.Dispose();
                process = null;
            }
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.DomainUnload -= Unload;
            AppDomain.CurrentDomain.ProcessExit -= Unload;
            AbortProcess();
        }

        public override string ToString()
        {
            return workingDirectory + " " + command + " " + arguments;
        }

        public event Action<string> OutputReceived;
        public event Action<string> ErrorReceived;


        string output;
        string error;
        string input;
        string cliEnding;
        Encoding desiredEncoding = Encoding.UTF8;
        int exitcode;
        bool aborted;
        readonly string command;
        readonly string arguments;
        readonly string workingDirectory;
        Dictionary<string, string> envVars = new Dictionary<string, string>();
        Process process;

        public CommandLineOutput Execute()
        {
            aborted = false;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo()
                {
                    FileName = command,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    StandardOutputEncoding = desiredEncoding,
                    StandardErrorEncoding = desiredEncoding,
                    ErrorDialog = false
                };
                // set env vars
                foreach (KeyValuePair<string, string> kvp in envVars) { psi.EnvironmentVariables.Add(kvp.Key, kvp.Value); }
                process = Process.Start(psi);

                if (!String.IsNullOrEmpty(input))
                {
                    StreamWriter myStreamWriter = process.StandardInput;
                    BinaryWriter writer = new BinaryWriter(myStreamWriter.BaseStream);
                    writer.Write(System.Text.Encoding.UTF8.GetBytes(input));
                    myStreamWriter.Close();
                }

                var sbOutput = new StringBuilder();
                process.OutputDataReceived += (obj, de) =>
                {
                    if (!string.IsNullOrEmpty(de.Data))
                    {
                        sbOutput.Append(de.Data + cliEnding);
                        if (OutputReceived != null) OutputReceived(de.Data);
                    }
                };

                var sbError = new StringBuilder();
                process.ErrorDataReceived += (obj, de) =>
                {
                    if (!string.IsNullOrEmpty(de.Data))
                    {
                        sbError.Append(de.Data + cliEnding);
                        if (ErrorReceived != null) ErrorReceived(de.Data);
                    }
                };

                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                process.WaitForExit();

                if (!aborted)
                {
                    error = sbError.ToString();
                    output = sbOutput.ToString();
                    exitcode = process.ExitCode;
                }
            }
            finally
            {
                if (process != null) process.Dispose();
                process = null;
            }
            return new CommandLineOutput(command, arguments, output, error, exitcode);
        }
    }
}