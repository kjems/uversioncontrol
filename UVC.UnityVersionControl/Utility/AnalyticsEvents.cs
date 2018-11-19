// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using System;
using UnityEditor;

namespace UVC
{
    [InitializeOnLoad]
    internal static class AnalyticsEvents
    {
        static AnalyticsEvents()
        {
            VCCommands.Instance.OperationCompleted += (operation, beforeStatus, afterStatus, success) =>
            {
                GoogleAnalytics.LogUserEvent("Operation", $"{operation.ToString()}_{(success ? "success" : "failed")}");
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
