// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using UnityEngine;
using UnityEditor;

namespace UVC.UserInterface
{
    [Serializable]
    internal class VCCredentialsWindow : EditorWindow
    {        
        string password = "";
        string username = "";
        bool hasVerified = false;
        bool success = false;
        bool allowCacheCredentials = false;

        //[MenuItem("Window/UVC/Credentials", false, 3)]
        public static void Init()
        {
            GetWindow<VCCredentialsWindow>(true, "Credentials");
        }

        private void OnEnable()
        {
            maxSize = new Vector2(250, 100);
            minSize = new Vector2(250, 100);
            titleContent = new GUIContent(VCSettings.VersionControlBackend + " Credentials");
            username = EditorPrefs.GetString("VCCredentialsWindow/username", "");
        }

        private void OnDisable()
        {
            EditorPrefs.SetString("VCCredentialsWindow/username", username);
        }

        private void OnGUI()
        {
            using (GUILayoutHelper.Vertical())
            {
                username = EditorGUILayout.TextField("Username", username);
                Color passwordColor = Color.grey;
                if (hasVerified && !success) passwordColor = Color.red;
                if (hasVerified && success) passwordColor = Color.green;
                using (GUILayoutHelper.BackgroundColor(passwordColor))
                {
                    password = EditorGUILayout.TextField("Password", password);
                }

                allowCacheCredentials = GUILayout.Toggle(allowCacheCredentials, new GUIContent("Allow Credentials to be cached"));

                using (GUILayoutHelper.Horizontal())
                {
                    if (GUILayout.Button("Cancel"))
                    {                        
                        Close();
                    }                    
                    if (!success)
                    {
                        if (GUILayout.Button("Verify"))
                        {
                            hasVerified = true;
                            success = VCCommands.Instance.SetUserCredentials(username, password, allowCacheCredentials);
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Close"))
                        {
                            Close();
                        }
                    }
                }
            }
        }
    }
}

