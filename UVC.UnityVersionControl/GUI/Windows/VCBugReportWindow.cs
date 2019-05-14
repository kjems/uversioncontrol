// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using UnityEngine;
using UnityEditor;

namespace UVC.UserInterface
{
    [Serializable]
    internal class VCBugReportWindow : EditorWindow
    {
        private const string defaultDescription = "Description:\n\n\n\nReproduction:\n\n\n";
        Vector2 statusScroll = Vector2.zero;
        string description = "";
        string bugTitle = "";
        string email = "";

        [MenuItem("Window/UVC/Report Bug", false, 3)]
        public static void Init()
        {
            GetWindow<VCBugReportWindow>(false, "Bug Report");
        }

        private void OnEnable()
        {
            email = EditorPrefs.GetString("VCBugReportWindow/email", "");
            description = defaultDescription;
        }

        private void OnDisable()
        {
            EditorPrefs.SetString("VCBugReportWindow/email", email);
        }

        private void OnGUI()
        {
            using (GUILayoutHelper.Vertical())
            {
                email = EditorGUILayout.TextField("E-Mail", email);
                bugTitle = EditorGUILayout.TextField("Title", bugTitle);
                statusScroll = EditorGUILayout.BeginScrollView(statusScroll, false, false);
                description = GUILayout.TextArea(description, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("Send"))
                {
                    bool sendBug = true;
                    if (string.IsNullOrEmpty(email)) email = "no@email";
                    if (sendBug && string.IsNullOrEmpty(bugTitle))
                    {
                        UserDialog.DisplayDialog("Need Title", "You need to give the bug a title", "OK");
                        sendBug = false;
                    }
                    if (sendBug && (string.IsNullOrEmpty(description) || description == defaultDescription))
                    {
                        UserDialog.DisplayDialog("Need Description", "You need to give the bug a description", "OK");
                        sendBug = false;
                    }
                    if (sendBug)
                    {
                        FogbugzUtilities.SubmitUserBug(bugTitle, description, email);
                        Close();
                    }
                }
            }
        }
    }
}

