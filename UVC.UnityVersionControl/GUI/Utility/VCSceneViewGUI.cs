// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEditor;
using UnityEngine;
#pragma warning disable CS4014

namespace UVC.UserInterface
{
    [InitializeOnLoad]
    public static class VCSceneViewGUI
    {
        public static System.Func<string> currentContext = SceneManagerUtilities.GetCurrentScenePath;
        
        private static GUIStyle buttonStyle;
        private static GUIStyle backgroundGuiStyle;
        private static bool shouldDraw = true;
        private static string selectionPath = "";

        static VCSceneViewGUI()
        {
            SceneView.onSceneGUIDelegate += SceneViewUpdate;
            VCSettings.SettingChanged += SceneView.RepaintAll;
            VCCommands.Instance.StatusCompleted += SceneView.RepaintAll;
            EditorApplication.update += EditorUpdate;
        }

        static string GetSelectionsPersistentAssetPath()
        {
            return currentContext();
        }

        static void Refresh()
        {
            VCUtility.RequestStatus(GetSelectionsPersistentAssetPath(), VCSettings.HierarchyReflectionMode);
            SceneView.RepaintAll();
        }

        static void EditorUpdate()
        {
            selectionPath = GetSelectionsPersistentAssetPath();
            shouldDraw = VCSettings.SceneviewGUI && VCCommands.Active && VCUtility.ValidAssetPath(selectionPath );
        }

        private static VCGUIControls.ValidActions validActions;
        private static VersionControlStatus vcSceneStatus = new VersionControlStatus();
        static void SceneViewUpdate(SceneView sceneView)
        {
            EditorUpdate();
            if (!shouldDraw) return;
            
            if (Event.current.type == EventType.Layout)
            {
                string assetPath = selectionPath;
                VCUtility.RequestStatus(assetPath, VCSettings.HierarchyReflectionMode);
                vcSceneStatus = VCCommands.Instance.GetAssetStatus(assetPath);
                validActions = VCGUIControls.GetValidActions(assetPath);
            }
            
            buttonStyle = new GUIStyle(EditorStyles.miniButton) {margin = new RectOffset(0, 0, 0, 0), fixedWidth = 80};

            backgroundGuiStyle = VCGUIControls.GetVCBox(vcSceneStatus);
            backgroundGuiStyle.padding = new RectOffset(4, 8, 1, 1);
            backgroundGuiStyle.margin = new RectOffset(1, 1, 1, 1);
            backgroundGuiStyle.border = new RectOffset(1, 1, 1, 1);
            backgroundGuiStyle.alignment = TextAnchor.MiddleCenter;

            var rect = new Rect(5, 5, 800, 100);
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(0, 0, rect.width, rect.height));
            GUILayout.BeginHorizontal();
            GUILayout.TextField(AssetStatusUtils.GetLockStatusMessage(vcSceneStatus), backgroundGuiStyle);
            GUILayout.Label(selectionPath.Substring(selectionPath.LastIndexOf('/') + 1));
            GUILayout.EndHorizontal();
            

            int numberOfButtons = 0;
            const int maxButtons = 4;

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
                            OnNextUpdate.Do(() => VCCommands.Instance.CommitDialog(new[] { selectionPath }));
                        }
                    }
                    if (validActions.showOpen)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button(Terminology.getlock, buttonStyle))
                        {
                            VCCommands.Instance.GetLockTask(new[] { selectionPath });
                        }
                    }
                    if (validActions.showCommit)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button(Terminology.commit, buttonStyle))
                        {
                            OnNextUpdate.Do(() => VCCommands.Instance.CommitDialog(new[] { selectionPath }));
                        }
                    }
                    if (validActions.showRevert)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button(new GUIContent(Terminology.revert, "Shift-click to " + Terminology.revert + " without confirmation"), buttonStyle))
                        {
                            var sceneAssetPath = new[] { selectionPath };
                            if (Event.current.shift || VCUtility.VCDialog(Terminology.revert, sceneAssetPath))
                            {
                                VCCommands.Instance.Revert(sceneAssetPath);
                            }
                        }
                    }
                    if (validActions.showOpenLocal)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button(Terminology.allowLocalEdit, buttonStyle))
                        {
                            VCCommands.Instance.AllowLocalEdit(new[] { selectionPath });
                        }
                    }
                    if (validActions.showUnlock)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button(Terminology.unlock, buttonStyle))
                        {
                            OnNextUpdate.Do(() => VCCommands.Instance.ReleaseLock(new[] { selectionPath }));
                        }
                    }
                    if (validActions.showForceOpen)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button("Force Open", buttonStyle))
                        {
                            OnNextUpdate.Do(() => VCUtility.GetLock(selectionPath, OperationMode.Force));
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
