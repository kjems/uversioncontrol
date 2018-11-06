using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UVC.Logging;

namespace UVC.UserInterface
{
    internal class NewBranchWindow : EditorWindow
    {
        public string fromPath;
        public string toPath;
        public Action refresh = () => { };
        
        private bool switchToNewBranch = true;

        public static void Init()
        {
            GetWindow<NewBranchWindow>("New Branch");
        }

        private void OnDisable()
        {
        }

        private void OnEnable()
        {
        }

        private void OnGUI()
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("From", GUILayout.Width(40));
                fromPath = GUILayout.TextField(fromPath);
            }
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("To", GUILayout.Width(40));
                toPath = GUILayout.TextField(toPath);
            }

            using (new GUILayout.HorizontalScope())
            {
                switchToNewBranch = GUILayout.Toggle(switchToNewBranch, "Switch To New Branch");
                if (GUILayout.Button("Create"))
                {
                    VCCommands.Instance.CreateBranch(fromPath, toPath);
                    if (switchToNewBranch)
                        VCCommands.Instance.SwitchBranch(toPath);

                    refresh();
                    Close();
                }
            }
        }
    }
}