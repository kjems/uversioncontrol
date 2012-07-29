// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEditor;
using UnityEngine;

namespace VersionControl.UserInterface
{
    [InitializeOnLoad]
    internal static class VCSceneViewGUI
    {
        private static GUIStyle buttonStyle;
        private static GUIStyle backgroundGuiStyle;

        static VCSceneViewGUI()
        {
            SceneView.onSceneGUIDelegate += SceneViewUpdate;
            VCSettings.SettingChanged += SceneView.RepaintAll;
            VCCommands.Instance.StatusCompleted += SceneView.RepaintAll;
        }

        static void SceneViewUpdate(SceneView sceneView)
        {
            if (!VCSettings.SceneviewGUI || !VCCommands.Active || string.IsNullOrEmpty(EditorApplication.currentScene)) return;

            var vcSceneStatus = VCCommands.Instance.GetAssetStatus(EditorApplication.currentScene);
            buttonStyle = new GUIStyle(EditorStyles.miniButton) {margin = new RectOffset(0, 0, 0, 0), fixedWidth = 70};

            backgroundGuiStyle = VCGUIControls.GetVCBox(vcSceneStatus);
            backgroundGuiStyle.padding = new RectOffset(4, 8, 1, 1);
            backgroundGuiStyle.margin = new RectOffset(1, 1, 1, 1);
            backgroundGuiStyle.border = new RectOffset(1, 1, 1, 1);
            backgroundGuiStyle.alignment = TextAnchor.MiddleCenter;

            var rect = new Rect(5, 5, 200, 65);
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(0, 0, rect.width, rect.height));
            GUILayout.TextField(VCGUIControls.GetLockStatusMessage(vcSceneStatus), backgroundGuiStyle);

            int numberOfButtons = 0;
            const int maxButtons = 4;

            using (GUILayoutHelper.Vertical())
            {
                using (new PushState<bool>(GUI.enabled, VCCommands.Instance.Ready, v => GUI.enabled = v))
                {
                    if (!VCUtility.HaveAssetControl(vcSceneStatus))
                    {
                        if (vcSceneStatus.lockStatus == VCLockStatus.LockedOther)
                        {
                            numberOfButtons++;
                            if (GUILayout.Button(new GUIContent(Terminology.bypass, "Shift-click to steal lock"), buttonStyle))
                            {
                                if (Event.current.shift)
                                {
                                    VCUtility.VCForceOpen(EditorApplication.currentScene, vcSceneStatus);
                                }
                                else VCCommands.Instance.BypassRevision(new[] {EditorApplication.currentScene});
                            }
                        }
                        else if (vcSceneStatus.fileStatus == VCFileStatus.Added)
                        {
                            numberOfButtons++;
                            if (GUILayout.Button(Terminology.commit, buttonStyle))
                            {
                                EditorApplication.SaveScene();
                                OnNextUpdate.Do(() => VCCommands.Instance.CommitDialog(new[] {EditorApplication.currentScene}));
                            }
                        }
                        else
                        {
                            numberOfButtons++;
                            if (GUILayout.Button(Terminology.getlock, buttonStyle))
                            {
                                VCCommands.Instance.GetLockTask(new[] {EditorApplication.currentScene});
                            }
                            numberOfButtons++;
                            if (GUILayout.Button(Terminology.bypass, buttonStyle))
                            {
                                VCCommands.Instance.BypassRevision(new[] { EditorApplication.currentScene });
                            }
                        }
                    }
                    else
                    {
                        if (vcSceneStatus.fileStatus == VCFileStatus.Unversioned)
                        {
                            numberOfButtons++;
                            if (GUILayout.Button(Terminology.add, buttonStyle))
                            {
                                EditorApplication.SaveScene();
                                OnNextUpdate.Do(() => VCCommands.Instance.CommitDialog(new[] {EditorApplication.currentScene}));
                            }
                        }
                        else
                        {
                            if (vcSceneStatus.bypassRevisionControl && vcSceneStatus.lockStatus != VCLockStatus.LockedOther)
                            {
                                numberOfButtons++;
                                if (GUILayout.Button(Terminology.getlock, buttonStyle))
                                {
                                    OnNextUpdate.Do(() => VCCommands.Instance.GetLockTask(new[] { EditorApplication.currentScene }));
                                }
                            }
                            else if (vcSceneStatus.lockStatus != VCLockStatus.LockedOther)
                            {
                                numberOfButtons++;
                                if (GUILayout.Button(Terminology.commit, buttonStyle))
                                {
                                    EditorApplication.SaveScene();
                                    OnNextUpdate.Do(() => VCCommands.Instance.CommitDialog(new[] {EditorApplication.currentScene}));
                                }
                            }
                            numberOfButtons++;
                            if (GUILayout.Button(new GUIContent(Terminology.revert, "Shift-click to " + Terminology.revert + " without confirmation"), buttonStyle))
                            {
                                var sceneAssetPath = new[] {EditorApplication.currentScene};
                                if (Event.current.shift || VCUtility.VCDialog(Terminology.revert, sceneAssetPath))
                                {
                                    EditorApplication.SaveScene();
                                    VCCommands.Instance.Revert(sceneAssetPath);
                                }
                            }
                        }
                    }


                    // bug: Workaround for a bug in Unity to avoid Tools getting stuck when number of GUI elements change while right mouse is down.
                    using (GUILayoutHelper.Enabled(false))
                    {
                        for (int i = numberOfButtons; i <= maxButtons; ++i)
                        {
                            GUI.Button(new Rect(0, 0, 0, 0), "", EditorStyles.label);
                        }
                    }
                }
            }


            GUILayout.EndArea();
            Handles.EndGUI();
        }

    }
}
