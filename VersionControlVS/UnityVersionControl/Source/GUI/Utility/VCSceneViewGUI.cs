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

        static void Refresh()
        {
            VCUtility.RequestStatus(EditorApplication.currentScene, VCSettings.HierarchyReflectionMode);
            SceneView.RepaintAll();
        }

        static void SceneViewUpdate(SceneView sceneView)
        {
            if (!VCSettings.SceneviewGUI || !VCCommands.Active || !VCUtility.ValidAssetPath(EditorApplication.currentScene)) return;

            VCUtility.RequestStatus(EditorApplication.currentScene, VCSettings.HierarchyReflectionMode);

            var vcSceneStatus = VCCommands.Instance.GetAssetStatus(EditorApplication.currentScene);
            buttonStyle = new GUIStyle(EditorStyles.miniButton) {margin = new RectOffset(0, 0, 0, 0), fixedWidth = 80};

            backgroundGuiStyle = VCGUIControls.GetVCBox(vcSceneStatus);
            backgroundGuiStyle.padding = new RectOffset(4, 8, 1, 1);
            backgroundGuiStyle.margin = new RectOffset(1, 1, 1, 1);
            backgroundGuiStyle.border = new RectOffset(1, 1, 1, 1);
            backgroundGuiStyle.alignment = TextAnchor.MiddleCenter;

            var rect = new Rect(5, 5, 200, 65);
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(0, 0, rect.width, rect.height));
            GUILayout.TextField(AssetStatusUtils.GetLockStatusMessage(vcSceneStatus), backgroundGuiStyle);

            int numberOfButtons = 0;
            const int maxButtons = 4;

            bool modified = vcSceneStatus.fileStatus == VCFileStatus.Modified;
            bool deleted = vcSceneStatus.fileStatus == VCFileStatus.Deleted;
            bool added = vcSceneStatus.fileStatus == VCFileStatus.Added;
            bool unversioned = vcSceneStatus.fileStatus == VCFileStatus.Unversioned;
            bool ignored = vcSceneStatus.fileStatus == VCFileStatus.Ignored;
            bool replaced = vcSceneStatus.fileStatus == VCFileStatus.Replaced;
            bool lockedByOther = vcSceneStatus.lockStatus == VCLockStatus.LockedOther;
            bool haveControl = VCUtility.HaveAssetControl(vcSceneStatus);
            bool haveLock = VCUtility.HaveVCLock(vcSceneStatus);
            bool allowLocalEdit = vcSceneStatus.LocalEditAllowed();
            bool pending = vcSceneStatus.reflectionLevel == VCReflectionLevel.Pending;

            bool showAdd =  !pending && !ignored && unversioned;
            bool showOpen =  !pending && !showAdd && !added && !haveLock && !deleted && (!lockedByOther || allowLocalEdit);
            bool showCommit = !pending && !ignored && !allowLocalEdit && (haveLock || added || deleted);
            bool showRevert = !pending && !ignored && !unversioned && (haveControl || modified || added || deleted || replaced);
            bool showOpenLocal = !pending && !ignored && !deleted && !allowLocalEdit && !unversioned && !added && !haveLock;
            bool showUnlock = !pending && !ignored && !allowLocalEdit && haveLock;
            bool showForceOpen = !pending && !ignored && !deleted  && !allowLocalEdit && !unversioned && !added && lockedByOther && Event.current.shift;

            using (GUILayoutHelper.Vertical())
            {
                using (new PushState<bool>(GUI.enabled, VCCommands.Instance.Ready, v => GUI.enabled = v))
                {
                    if (showAdd)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button(Terminology.add, buttonStyle))
                        {
                            EditorApplication.SaveScene();
                            OnNextUpdate.Do(() => VCCommands.Instance.CommitDialog(new[] { EditorApplication.currentScene }));
                        }
                    }
                    if (showOpen)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button(Terminology.getlock, buttonStyle))
                        {
                            VCCommands.Instance.GetLockTask(new[] { EditorApplication.currentScene });
                        }
                    }
                    if (showCommit)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button(Terminology.commit, buttonStyle))
                        {
                            OnNextUpdate.Do(() => VCCommands.Instance.CommitDialog(new[] { EditorApplication.currentScene }));
                        }
                    }
                    if (showRevert)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button(new GUIContent(Terminology.revert, "Shift-click to " + Terminology.revert + " without confirmation"), buttonStyle))
                        {
                            var sceneAssetPath = new[] { EditorApplication.currentScene };
                            if (Event.current.shift || VCUtility.VCDialog(Terminology.revert, sceneAssetPath))
                            {
                                EditorApplication.SaveScene();
                                VCCommands.Instance.Revert(sceneAssetPath);
                                OnNextUpdate.Do(AssetDatabase.Refresh);
                            }
                        }
                    }
                    if (showOpenLocal)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button(Terminology.allowLocalEdit, buttonStyle))
                        {
                            VCCommands.Instance.AllowLocalEdit(new[] { EditorApplication.currentScene });
                        }
                    }
                    if (showUnlock)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button(Terminology.unlock, buttonStyle))
                        {
                            OnNextUpdate.Do(() => VCCommands.Instance.ReleaseLock(new[] { EditorApplication.currentScene }));
                        }
                    }
                    if (showForceOpen)
                    {
                        numberOfButtons++;
                        if (GUILayout.Button("Force Open", buttonStyle))
                        {
                            OnNextUpdate.Do(() => VCUtility.GetLock(EditorApplication.currentScene, OperationMode.Force));
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
