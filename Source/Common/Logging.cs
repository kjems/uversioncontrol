// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Diagnostics;

namespace VersionControl
{
    public static class D
    {
        public static string GetCallstack()
        {
            string callstack = "";
            var stackTrace = new StackTrace(2, true);
            StackFrame[] stackFrames = stackTrace.GetFrames();
            if (stackFrames != null)
            {
                foreach (var stackFrame in stackFrames)
                {
                    callstack += stackFrame.GetMethod() + "\t" + stackFrame.GetFileName() + " : " + stackFrame.GetFileLineNumber() + "\n";
                }
            }
            return callstack;
        }


        public enum Severity
        {
            Log,
            Error
        }

        public static Action<string> writeLogCallback;
        public static Action<string> writeErrorCallback;
        public static Action<VCException> exceptionCallback;

        public static void ThrowException(Exception exception)
        {
            if (exceptionCallback != null)
            {
                if (exception is VCException) exceptionCallback((VCException)exception);
                else exceptionCallback(new VCException(exception.Message, exception.StackTrace, exception));
            }
            else Log("Unhandled exception : " + exception.Message, Severity.Error);
        }

        private static string FormatMessage(string message)
        {
            return DateTime.Now.ToString("HH:mm:ss.ffff") + "(" + System.Threading.Thread.CurrentThread.ManagedThreadId + "): " + message + "\n\n" + GetCallstack();
        }

        public static void Log(string message, Severity severity = Severity.Log)
        {
            if (severity == Severity.Log && writeLogCallback != null) writeLogCallback(FormatMessage(message));
            if (severity == Severity.Error && writeErrorCallback != null) writeErrorCallback(FormatMessage(message));
        }
    }
}
