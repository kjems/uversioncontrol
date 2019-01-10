// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using UnityEngine;
using UnityEditor;

namespace UVC.UserInterface
{
    [Serializable]
    internal class VCAboutWindow : EditorWindow
    {
        const string maintainAtURL = "https://github.com/kjems/uversioncontrol";
        private const string infoText =
            "Unity Version Control is maintained at Github.\n" +
            "All code is subject to the MIT open source license.\n" +
            "Bug reports can be submitted via the Report Bug menu item.\n" +
            "Feedback can be given to 'kristian.kjems+UnityVC@gmail.com'";
        
        
        [MenuItem("Window/UVC/About", false, 4)]
        public static void Init()
        {
            GetWindow<VCAboutWindow>(false, "UVC About");
        }

        private void OnEnable()
        {
            minSize = new Vector2(400, 110);
        }

        private void OnGUI()
        {
            using (GUILayoutHelper.VerticalIdented(14))
            {
                GUILayout.Label("Unity Version Control (UVC), Version : " + VCUtility.GetCurrentVersion(), EditorStyles.boldLabel);
                EditorGUILayout.TextArea(infoText);

                var linkStyle = new GUIStyle(EditorStyles.label) {normal = {textColor = new Color(0.4f,0.4f,1.0f)}};
                if (GUILayout.Button(maintainAtURL, linkStyle))
                {
                    System.Diagnostics.Process.Start(maintainAtURL);
                }
            }
        }
    }
}

