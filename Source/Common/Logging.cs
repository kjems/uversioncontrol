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

        public static Action<string> writeLogCallback;
        public static Action<string> writeWarningCallback;
        public static Action<string> writeErrorCallback;
        public static Action<VCException> exceptionCallback;

        public static void ThrowException(Exception exception)
        {
            if (exceptionCallback != null)
            {
                if (exception is VCException) exceptionCallback((VCException)exception);
                else exceptionCallback(new VCException(exception.Message, exception.StackTrace, exception));
            }
            else LogError("Unhandled exception : " + exception.Message);
        }

        private static string FormatMessage(string message)
        {
            return DateTime.Now.ToString("HH:mm:ss.ffff") + "(" + System.Threading.Thread.CurrentThread.ManagedThreadId + "): " + message + "\n\n" + GetCallstack();
        }

        public static void Log(string message)
        {
            if (writeLogCallback != null) writeLogCallback(FormatMessage(message));
        }

        public static void LogError(string message)
        {
            if (writeErrorCallback != null) writeErrorCallback(FormatMessage(message));
        }

        public static void LogWarning(string message)
        {
            if (writeWarningCallback != null) writeWarningCallback(FormatMessage(message));
        }

        /// <summary>
        /// An important condition is not met and the program is unable to continue in a reasonable state
        /// </summary>
        public static void Assert(bool condition, string message)
        {
            if(!condition) LogError(message);
        }

        /// <summary>
        /// A recommended condition is not met but the program is able to continue
        /// </summary>
        public static void Check(bool condition, string message)
        {
            if (!condition) LogWarning(message);
        }
    }
}
