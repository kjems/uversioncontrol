// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;

namespace UVC
{
    [Serializable]
    public sealed class BranchStatus
    {
        public BranchStatus Clone() => MemberwiseClone() as BranchStatus;

        public string name;
        public string author;
        public int revision;
        public DateTime date;
    }
}