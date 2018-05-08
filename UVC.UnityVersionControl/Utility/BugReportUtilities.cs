// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using UnityEditor;
using UnityEngine;
using VersionControl;
using VersionControl.Logging;

internal static class FogbugzUtilities
{
    private const int maxRetryCount = 5;

    public static void SubmitAutoBug(string title, string description)
    {
        SubmitBug("https://uversioncontrol.fogbugz.com/scoutSubmit.asp", "Kristian Kjems", "AutoReports", "Default", title, description, "", false);
    }

    public static void SubmitUserBug(string title, string description, string email)
    {
        SubmitBug("https://uversioncontrol.fogbugz.com/scoutSubmit.asp", "Kristian Kjems", "Inbox", "Undecided", title, description, email, true);
    }

    public static void SubmitBug(string url, string username, string project, string area, string description, string extra, string email, bool forceNewBug = false, int retryCount = 0)
    {
        string bugUrl = string.Format("{0}?Description={1}&Extra={2}&Email={3}&ScoutUserName={4}&ScoutProject={5}&ScoutArea={6}&ForceNewBug={7}",
            url,
            WWW.EscapeURL(description),
            WWW.EscapeURL(extra),
            WWW.EscapeURL(email),
            WWW.EscapeURL(username),
            WWW.EscapeURL(project),
            WWW.EscapeURL(area),
            (forceNewBug ? "1" : "0")
        );
        
        var www = new WWW(bugUrl);
        ContinuationManager.Add(() => www.isDone, () =>
        {
            bool success = string.IsNullOrEmpty(www.error) && www.text.Contains("<Success>");
            if (success)
            {
                D.Log("Bug successfully reported to the 'Unity Version Control' FogBugz database.");
            }
            else
            {
                if (retryCount <= maxRetryCount)
                {
                    SubmitBug(url, username, project, area, description, extra, email, forceNewBug, ++retryCount);
                }
                else
                {
                    D.LogError("Bug report failed:\n" + www.error);
                }
            }
        });
    }
}

internal static class GitHubUtilities
{
    public static void OpenNewIssueInBrowser(string user, string repo)
    {
        var url = string.Format("https://github.com/{0}/{1}/issues/new", user, repo);
        try
        {
            System.Diagnostics.Process.Start(url);
        }
        catch (Exception)
        {
            Debug.LogError("No default web browser installed so unable to open new github issue. Use following URL:\n" + url);
        }
    }
}

