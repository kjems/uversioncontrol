// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;

namespace VersionControl
{
    [Serializable]
    public class VCException : Exception
    {
        public string ErrorMessage { get { return base.Message; } }
        public string ErrorDetails { get; private set; }
        public VCException(string errorMessage, string errorDetails) : base(errorMessage) { ErrorDetails = errorDetails + "\n\n" + D.GetCallstack(); }
        public VCException(string errorMessage, string errorDetails, Exception innerEx) : base(errorMessage, innerEx) { ErrorDetails = errorDetails + "\n\n" + D.GetCallstack(); }
    }

    public class VCCriticalException : VCException
    {
        public VCCriticalException(string errorMessage, string errorDetails) : base(errorMessage, errorDetails) { }
        public VCCriticalException(string errorMessage, string errorDetails, Exception innerEx) : base(errorMessage, errorDetails, innerEx) { }
    }

    public class VCConnectionTimeoutException : VCException
    {
        public VCConnectionTimeoutException(string errorMessage, string errorDetails) : base(errorMessage, errorDetails) { }
        public VCConnectionTimeoutException(string errorMessage, string errorDetails, Exception innerEx) : base(errorMessage, errorDetails, innerEx) { }
    }

    public class VCNewerVersionException : VCException
    {
        public VCNewerVersionException(string errorMessage, string errorDetails) : base(errorMessage, errorDetails) { }
        public VCNewerVersionException(string errorMessage, string errorDetails, Exception innerEx) : base(errorMessage, errorDetails, innerEx) { }
    }

    public class VCOutOfDate : VCException
    {
        public VCOutOfDate(string errorMessage, string errorDetails) : base(errorMessage, errorDetails) { }
        public VCOutOfDate(string errorMessage, string errorDetails, Exception innerEx) : base(errorMessage, errorDetails, innerEx) { }
    }

    public class VCLocalCopyLockedException : VCException
    {
        public VCLocalCopyLockedException(string errorMessage, string errorDetails) : base(errorMessage, errorDetails) { }
        public VCLocalCopyLockedException(string errorMessage, string errorDetails, Exception innerEx) : base(errorMessage, errorDetails, innerEx) { }
    }

    public class VCLockedByOther : VCException
    {
        public VCLockedByOther(string errorMessage, string errorDetails) : base(errorMessage, errorDetails) { }
        public VCLockedByOther(string errorMessage, string errorDetails, Exception innerEx) : base(errorMessage, errorDetails, innerEx) { }
    }
}