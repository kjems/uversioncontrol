// Copyright (c) <2018>
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
            Dictionary<string, string> envVars = null
            )
        {
            this.command = command;
            this.arguments = arguments;
            this.workingDirectory = workingDirectory;
            this.input = input;
            if (envVars != null) this.envVars = new Dictionary<string, string>(envVars);
            AppDomain.CurrentDomain.DomainUnload += Unload;
            AppDomain.CurrentDomain.ProcessExit += Unload;
        }

        private void Unload(object sender, EventArgs args)
        {
            AbortProcess();
        }

        private void AbortProcess()
        {
            if (!aborted && process != null)
            {
                aborted = true;
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                }
                catch (Exception) { }
                finally
                {
                    process.Dispose();
                    process = null;
                }
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
        const int BUFFER_SIZE = 2048;
        Encoding encoding = Encoding.UTF8;
        public event Action<string> OutputReceived;
        public event Action<string> ErrorReceived;
        string output;
        string error;
        string input;
        int exitcode;
        volatile bool aborted;
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
                    StandardOutputEncoding = encoding,
                    StandardErrorEncoding = encoding,
                    ErrorDialog = false
                };
                // set env vars
                foreach (KeyValuePair<string, string> kvp in envVars) { psi.EnvironmentVariables.Add(kvp.Key, kvp.Value); }
                process = Process.Start(psi);
                encoding = process.StandardOutput.CurrentEncoding;

                if (!String.IsNullOrEmpty(input))
                {
                    StreamWriter myStreamWriter = process.StandardInput;
                    BinaryWriter writer = new BinaryWriter(myStreamWriter.BaseStream);
                    writer.Write(System.Text.Encoding.UTF8.GetBytes(input));
                    myStreamWriter.Close();
                }

                /*if (psi.Arguments.Contains("ExceptionTest.txt"))
                {
                    throw new System.ApplicationException("Test Exception cast due to ExceptionTest.txt being a part of arguments");
                }*/

                var sbOutput = new StringBuilder();
                byte[] buffer = new byte[BUFFER_SIZE];
                Decoder decoder = encoding.GetDecoder();
                while (true)
                {
                    var asyncResult = process.StandardOutput.BaseStream.BeginRead(buffer, 0, BUFFER_SIZE, null, null);
                    asyncResult.AsyncWaitHandle.WaitOne();
                    var bytesRead = process.StandardOutput.BaseStream.EndRead(asyncResult);
                    if (bytesRead > 0)
                    {
                        int charactersRead = decoder.GetCharCount(buffer, 0, bytesRead);
                        char[] chars = new char[charactersRead];
                        charactersRead = decoder.GetChars(buffer, 0, bytesRead, chars, 0);
                        string result = ConvertEncoding(chars, encoding, Encoding.UTF8);
                        if (OutputReceived != null && !string.IsNullOrEmpty(result))
                            OutputReceived(result);
                        sbOutput.Append(result);
                    }
                    else
                    {
                        process.WaitForExit();
                        break;
                    }
                }

                if (!aborted)
                {
                    output = sbOutput.ToString();
                    error = process.StandardError.ReadToEnd();
                    if (ErrorReceived != null)
                        ErrorReceived(error);
                    exitcode = process.ExitCode;
                }
            }
            finally
            {
                if (process != null)
                    process.Dispose();
                process = null;
            }
            return new CommandLineOutput(command, arguments, output, error, exitcode);
        }

        public static string ConvertEncoding(char[] source, Encoding sourceEncoding, Encoding targetEncoding)
        {
            if (sourceEncoding == targetEncoding)
                return new string(source);
            byte[] sourceEncodingBytes = sourceEncoding.GetBytes(source);
            byte[] targetEncodingBytes = Encoding.Convert(sourceEncoding, targetEncoding, sourceEncodingBytes);
            return targetEncoding.GetString(targetEncodingBytes);
        }

    }
}
