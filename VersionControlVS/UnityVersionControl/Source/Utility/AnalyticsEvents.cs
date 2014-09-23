// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using System;
using UnityEditor;

namespace VersionControl
{
    [InitializeOnLoad]
    internal static class AnalyticsEvents
    {
        static AnalyticsEvents()
        {
            VCCommands.Instance.OperationCompleted += (operation, assets, success) =>
            {
                GoogleAnalytics.LogUserEvent("Operation", string.Format("{0}_{1}", operation.ToString(), (success ? "success" : "failed")));
            };

            int dayOfYear = DateTime.Now.DayOfYear;
            int dayOfYearForLastSubmit = EditorPrefs.GetInt("UVCGA/DayOfLastSubmit", -1);
            if (dayOfYear != dayOfYearForLastSubmit)
            {
                EditorPrefs.SetInt("UVCGA/DayOfLastSubmit", dayOfYear);
                GoogleAnalytics.LogUserEvent("Project", PlayerSettings.productName.GetHashCode().ToString());
                GoogleAnalytics.LogUserEvent("Version", VCUtility.GetCurrentVersion());
                GoogleAnalytics.LogUserEvent("OS", Environment.OSVersion.Platform.ToString());
            }
        }
    }
}
