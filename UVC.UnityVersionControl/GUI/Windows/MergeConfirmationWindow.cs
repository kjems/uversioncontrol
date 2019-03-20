using System;
using UnityEngine;
using UnityEditor;

#pragma warning disable CS4014

namespace UVC.UserInterface
{
    internal class MergeConfirmationWindow : EditorWindow
    {
        public string fromPath;
        public string toPath;
        public bool localModified = false;
        public Action mergeAction;
        
        public static void Init()
        {
            GetWindow<MergeConfirmationWindow>("Merge Confirmation");
        }

        private void OnGUI()
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("From", GUILayout.Width(40));
                GUILayout.Label(fromPath, EditorStyles.textField);
            }
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Into", GUILayout.Width(40));
                GUILayout.Label(toPath, EditorStyles.textField);
            }

            if (localModified)
            {
                EditorGUILayout.HelpBox("Your local-copy has modifications!", MessageType.Warning);
            }
            
            GUILayout.FlexibleSpace();

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Merge"))
                {
                    mergeAction?.Invoke();
                    Close();              
                }

                if (GUILayout.Button("Abort"))
                {
                    Close();
                }
            }
        }
    }
}