// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Collections.Generic;

namespace VersionControl
{
    public class StatusDatabase : Dictionary<string, VersionControlStatus>
    {
        public StatusDatabase() { }
        public StatusDatabase(IDictionary<string, VersionControlStatus> database)
            : base(database, StringComparer.OrdinalIgnoreCase) { }

        public new VersionControlStatus this[string key]
        {
            get
            {
                VersionControlStatus svnStatus;
                return TryGetValue(key, out svnStatus) ? svnStatus : new VersionControlStatus { assetPath = key };
            }
            set
            {
                base[key] = value;
            }
        }
        public void Add(VersionControlStatus status)
        {
            base[status.assetPath] = status;
        }
    }
}