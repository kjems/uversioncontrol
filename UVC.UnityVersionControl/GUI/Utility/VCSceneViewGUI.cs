// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using Unity.Profiling;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine;

#pragma warning disable CS4014

namespace UVC.UserInterface
{
    [InitializeOnLoad]
    public static class VCSceneViewGUI
    {
        public static System.Func<string> currentContext = () =>
        {
            if (PrefabHelper.IsPartofPrefabStage(Selection.activeGameObject))
            {
                return PrefabStageUtility.GetPrefabStage(Selection.activeGameObject).prefabAssetPath;
            }
            return AssetDatabase.GetAssetOrScenePath(Selection.activeObject);
        };

        const float buttonHeight = 15f;
        const float buttonWidth = 80f;
        const int fontsize = 9;

        private static GUIStyle buttonStyle;
        private static GUIStyle backgroundGuiStyle;
        private static bool shouldDraw = true;
        private static string selectionPath = "";
        private static bool initialized = false;

        private static ValidActions validActions;
        private static VersionControlStatus vcSceneStatus = new VersionControlStatus();
        private static ProfilerMarker sceneviewUpdateMarker = new ProfilerMarker("UVC.SceneViewUpdate");


        private static readonly GUIContent addContent            = new GUIContent(Terminology.add);
        private static readonly GUIContent getLockContent        = new GUIContent(Terminology.getlock);
        private static readonly GUIContent commitContent         = new GUIContent(Terminology.commit);
        private static readonly GUIContent revertContent         = new GUIContent(Terminology.revert, "Shift-click to " + Terminology.revert + " without confirmation");
        private static readonly GUIContent allowLocalEditContent = new GUIContent(Terminology.allowLocalEdit);
        private static readonly GUIContent unlockContent         = new GUIContent(Terminology.unlock);
        private static readonly GUIContent forceOpenContent      = new GUIContent("Force Open");

        static VCSceneViewGUI()
        {
            SceneView.duringSceneGui += SceneViewUpdate;
            VCSettings.SettingChanged += SceneView.RepaintAll;
            VCCommands.Instance.StatusCompleted += SceneView.RepaintAll;
            EditorApplication.update += EditorUpdate;
        }

        static void InitializeIfNeeded()
        {
            if (!initialized)
            {
                initialized = true;
                buttonStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    margin = new RectOffset(0, 0, 0, 0),
                    fixedWidth = buttonWidth,
                    fontSize = fontsize
                };
                backgroundGuiStyle = VCGUIControls.GetVCBox(vcSceneStatus);
                backgroundGuiStyle.padding = new RectOffset(4, 4, 1, 1);
                backgroundGuiStyle.margin = new RectOffset(1, 1, 1, 1);
                backgroundGuiStyle.border = new RectOffset(1, 1, 1, 1);
                backgroundGuiStyle.alignment = TextAnchor.MiddleCenter;
                backgroundGuiStyle.fontSize = fontsize;

            }
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
            shouldDraw = VCSettings.SceneviewGUI && VCCommands.Active && VCUtility.ValidAssetPath(selectionPath);
            if (shouldDraw)
            {
                string assetPath = selectionPath;
                VCUtility.RequestStatus(assetPath, VCSettings.HierarchyReflectionMode);
                vcSceneStatus = VCCommands.Instance.GetAssetStatus(assetPath);
                validActions = VCGUIControls.GetValidActions(assetPath);
                if(backgroundGuiStyle != null)
                    backgroundGuiStyle.normal.background = IconUtils.boxIcon.GetTexture(AssetStatusUtils.GetStatusColor(vcSceneStatus, true));
            }
        }


        static void SceneViewUpdate(SceneView sceneView)
        {
            using (sceneviewUpdateMarker.Auto())
            {
                InitializeIfNeeded();
                if (!shouldDraw) return;

                // This optimization is causing problems for following sceneview GUI, so removed for now.
                //if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)
                //    return;

                var stateRect     = new Rect(  2f,  2f, buttonWidth, buttonHeight);
                var selectionRect = new Rect(  2f + buttonWidth,  1f, 700f, buttonHeight);
                var buttonRect    = new Rect(  2f,  2f, buttonWidth, buttonHeight);

                Handles.BeginGUI();

                GUI.TextField(stateRect, AssetStatusUtils.GetStatusText(vcSceneStatus), backgroundGuiStyle);
                GUI.Label(selectionRect, selectionPath.Substring(selectionPath.LastIndexOf('/') + 1), EditorStyles.miniLabel);

                int numberOfButtons = 0;
                const int maxButtons = 4;

                using (new PushState<bool>(GUI.enabled, VCCommands.Instance.Ready, v => GUI.enabled = v))
                {
                    if ((validActions & ValidActions.Add) != 0)
                    {
                        buttonRect.y += buttonHeight;
                        numberOfButtons++;
                        if (GUI.Button(buttonRect, addContent , buttonStyle))
                        {
                            SceneManagerUtilities.SaveActiveScene();
                            OnNextUpdate.Do(() => VCCommands.Instance.CommitDialog(new[] {selectionPath}));
                        }
                    }

                    if ((validActions & ValidActions.Open) != 0)
                    {
                        buttonRect.y += buttonHeight;
                        numberOfButtons++;
                        if (GUI.Button(buttonRect,getLockContent , buttonStyle))
                        {
                            VCCommands.Instance.GetLockTask(new[] {selectionPath});
                        }
                    }

                    if ((validActions & ValidActions.Commit) != 0)
                    {
                        buttonRect.y += buttonHeight;
                        numberOfButtons++;
                        if (GUI.Button(buttonRect,commitContent , buttonStyle))
                        {
                            OnNextUpdate.Do(() => VCCommands.Instance.CommitDialog(new[] {selectionPath}));
                        }
                    }

                    if ((validActions & ValidActions.Revert) != 0)
                    {
                        buttonRect.y += buttonHeight;
                        numberOfButtons++;
                        if (GUI.Button(buttonRect,revertContent , buttonStyle))
                        {
                            var sceneAssetPath = new[] {selectionPath};
                            if (Event.current.shift || VCUtility.VCDialog(Terminology.revert, sceneAssetPath))
                            {
                                VCCommands.Instance.Revert(sceneAssetPath);
                            }
                        }
                    }

                    if ((validActions & ValidActions.OpenLocal) != 0)
                    {
                        buttonRect.y += buttonHeight;
                        numberOfButtons++;
                        if (GUI.Button(buttonRect,allowLocalEditContent, buttonStyle))
                        {
                            VCCommands.Instance.AllowLocalEdit(new[] {selectionPath});
                        }
                    }

                    if ((validActions & ValidActions.Unlock) != 0)
                    {
                        buttonRect.y += buttonHeight;
                        numberOfButtons++;
                        if (GUI.Button(buttonRect,unlockContent , buttonStyle))
                        {
                            OnNextUpdate.Do(() => VCCommands.Instance.ReleaseLock(new[] {selectionPath}));
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
                Handles.EndGUI();
            }
        }

    }
}
