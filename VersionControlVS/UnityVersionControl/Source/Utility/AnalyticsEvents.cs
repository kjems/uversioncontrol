// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEditor;

namespace VersionControl
{
    [InitializeOnLoad]
    internal static class AnalyticsEvents
    {
        static AnalyticsEvents()
        {
            VCCommands.Instance.OperationCompleted += (operation, success) =>
            {
                GoogleAnalytics.LogUserEvent("Operation", string.Format("{0}_{1}", operation.ToString(), (success ? "success" : "failed")));
            };
        }
    }
}
