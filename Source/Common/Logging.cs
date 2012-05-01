// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnitySVN@gmail.com>
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
        public static void Log(string message)
        {
            if (writeLogCallback != null) writeLogCallback(message + "\n\n" + GetCallstack());
        }
    }
}
