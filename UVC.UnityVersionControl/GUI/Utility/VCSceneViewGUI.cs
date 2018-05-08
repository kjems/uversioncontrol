// Copyright (c) <2018>
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
        private static bool shouldDraw = true;

        static VCSceneViewGUI()
        {
            SceneView.onSceneGUIDelegate += SceneViewUpdate;
            VCSettings.SettingChanged += SceneView.RepaintAll;
            VCCommands.Instance.StatusCompleted += SceneView.RepaintAll;
            EditorApplication.update += EditorUpdate;
        }

        static void Refresh()
        {
            VCUtility.RequestStatus(SceneManagerUtilities.GetCurrentScenePath(), VCSettings.HierarchyReflectionMode);
            SceneView.RepaintAll();
        }

        static void EditorUpdate()
        {
            shouldDraw = VCSettings.SceneviewGUI && VCCommands.Active && VCUtility.ValidAssetPath(SceneManagerUtilities.GetCurrentScenePath());
        }

        static void SceneViewUpdate(SceneView sceneView)
        {
            if (!shouldDraw) return;

            string assetPath = SceneManagerUtilities.GetCurrentScenePath();
            VCUtility.RequestStatus(assetPath, VCSettings.HierarchyReflectionMode);

            var vcSceneStatus = VCCommands.Instance.GetAssetStatus(assetPath);
            buttonStyle = new GUIStyle(EditorStyles.miniButton) {margin = new RectOffset(0, 0, 0, 0), fixedWidth = 80};

            backgroundGuiStyle = VCGUIControls.GetVCBox(vcSceneStatus);
            backgroundGuiStyle.padding = new RectOffset(4, 8, 1, 1);
            backgroundGuiStyle.margin = new RectOffset(1, 1, 1, 1);
            backgroundGuiStyle.border = new RectOffset(1, 1, 1, 1);
            backgroundGuiStyle.alignment = TextAnchor.MiddleCenter;

            var rect = new Rect(5, 5, 200, 100);
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(0, 0, rect.width, rect.height));
            GUILayout.TextField(AssetStatusUtils.GetLockStatusMessage(vcSceneStatus), backgroundGuiStyle);

            int numberOfButtons = 0;
            const int maxButtons = 4;

            var validActions = VCGUIControls.GetValidActions(assetPath);                       

            using (GUILayoutHelper.Vertical())
            {
                using (new PushState<bool>(GUI.enabled, VCCommands.Instance.Ready, v => GUI.enabled = v))
                {
                    if (validActions.showAdd)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button(Terminology.add, buttonStyle))
                        {
                            SceneManagerUtilities.SaveActiveScene();
                            OnNextUpdate.Do(() => VCCommands.Instance.CommitDialog(new[] { SceneManagerUtilities.GetCurrentScenePath() }));
                        }
                    }
                    if (validActions.showOpen)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button(Terminology.getlock, buttonStyle))
                        {
                            VCCommands.Instance.GetLockTask(new[] { SceneManagerUtilities.GetCurrentScenePath() });
                        }
                    }
                    if (validActions.showCommit)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button(Terminology.commit, buttonStyle))
                        {
                            OnNextUpdate.Do(() => VCCommands.Instance.CommitDialog(new[] { SceneManagerUtilities.GetCurrentScenePath() }));
                        }
                    }
                    if (validActions.showRevert)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button(new GUIContent(Terminology.revert, "Shift-click to " + Terminology.revert + " without confirmation"), buttonStyle))
                        {
                            var sceneAssetPath = new[] { SceneManagerUtilities.GetCurrentScenePath() };
                            if (Event.current.shift || VCUtility.VCDialog(Terminology.revert, sceneAssetPath))
                            {
                                SceneManagerUtilities.SaveActiveScene();
                                VCCommands.Instance.Revert(sceneAssetPath);
                                OnNextUpdate.Do(AssetDatabase.Refresh);
                            }
                        }
                    }
                    if (validActions.showOpenLocal)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button(Terminology.allowLocalEdit, buttonStyle))
                        {
                            VCCommands.Instance.AllowLocalEdit(new[] { SceneManagerUtilities.GetCurrentScenePath() });
                        }
                    }
                    if (validActions.showUnlock)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button(Terminology.unlock, buttonStyle))
                        {
                            OnNextUpdate.Do(() => VCCommands.Instance.ReleaseLock(new[] { SceneManagerUtilities.GetCurrentScenePath() }));
                        }
                    }
                    if (validActions.showForceOpen)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button("Force Open", buttonStyle))
                        {
                            OnNextUpdate.Do(() => VCUtility.GetLock(SceneManagerUtilities.GetCurrentScenePath(), OperationMode.Force));
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
