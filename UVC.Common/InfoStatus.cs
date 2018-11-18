// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;

namespace UVC
{
    [Serializable]
    public sealed class InfoStatus
    {
        public InfoStatus Clone() => MemberwiseClone() as InfoStatus;

        public string url;
        public string relativeUrl;
        public string uuid;
        public string repositoryRoot;
        public string author;
        public int revision;
        public int lastChangedRevision;
        public DateTime lastChangedDate;
    }
}