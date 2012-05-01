// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnitySVN@gmail.com>

// This script is a window to display local changes and to perform commands on 
// the repository like updating and committing files.
// SVNIntegration is used to get state and execute commands on the repository.
//
// Although functional the general quality of this file is poor and need a refactor


using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using MultiColumnState = MultiColumnState<string, UnityEngine.GUIContent>;

namespace VersionControl.UserInterface
{
    internal class VCWindow : EditorWindow
    {
        [MenuItem("UVC/Overview Window", false, 1)]
        public static void Init()
        {
            GetWindow<VCWindow>(false, "VersionControl");
        }

        [SerializeField] private bool showUnversioned = true;
        [SerializeField] private bool showMeta = true;
        [SerializeField] private float statusHeight = 1000;

        const float toolbarHeight = 18.0f;
        const float inStatusHeight = 18.0f;
        string commandInProgress = "";
        VCMultiColumnAssetList vcMultiColumnAssetList;
        VCSettingsWindow settingsWindow;
        Rect rect;

        private bool GUIFilter(string key, VersionControlStatus vcStatus)
        {
            var metaStatus = vcStatus.MetaStatus();
            bool unversioned = vcStatus.fileStatus == VCFileStatus.Unversioned;
            bool meta = metaStatus.fileStatus != VCFileStatus.Normal && vcStatus.fileStatus == VCFileStatus.Normal;
            bool rest = !unversioned && !meta;
            return (showUnversioned && unversioned) || (showMeta && meta) || rest;
        }

        private bool BaseFilter(string key, VersionControlStatus vcStatus)
        {
            var metaStatus = vcStatus.MetaStatus();
            bool metaCriteria = metaStatus.fileStatus != VCFileStatus.Normal && metaStatus.fileStatus != VCFileStatus.None && metaStatus.fileStatus != VCFileStatus.Ignored;
            bool assetCriteria = vcStatus.fileStatus != VCFileStatus.None && vcStatus.fileStatus != VCFileStatus.Normal && vcStatus.fileStatus != VCFileStatus.Ignored;
            bool localLock = vcStatus.lockStatus == VCLockStatus.LockedHere;
            return metaCriteria || assetCriteria || localLock;
        }

        private void UpdateFilteringOfKeys()
        {
            vcMultiColumnAssetList.RefreshGUIFilter();
        }

        private List<string> GetSelectedAssets()
        {
            return vcMultiColumnAssetList.GetSelectedAssets().ToList();
        }
        
        virtual protected void OnEnable()
        {
            vcMultiColumnAssetList = new VCMultiColumnAssetList();

            vcMultiColumnAssetList.SetBaseFilter(BaseFilter);
            vcMultiColumnAssetList.SetGUIFilter(GUIFilter);

            VCCommands.Instance.StatusCompleted += RefreshGUI;
            VCSettings.SettingChanged += Repaint;
            VCCommands.Instance.ProgressInformation += s =>
            {
                commandInProgress = s + "\n" + commandInProgress;
                Repaint();
            };

            rect = new Rect(0, statusHeight, position.width, 10.0f);
        }

        virtual protected void OnDisable()
        {
            VCCommands.Instance.StatusCompleted -= RefreshGUI;
            VCSettings.SettingChanged -= Repaint;
            vcMultiColumnAssetList.Dispose();
        }
        
        private void RefreshGUI()
        {
            Repaint();
        }
        
        private void OnGUI()
        {
            HandleInput();

            EditorGUILayout.BeginVertical();

            DrawToolbar();

            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);
            rect = GUIControls.DragButton(rect, GUIContent.none, null);
            rect.x = 0.0f;
            statusHeight = rect.y = Mathf.Clamp(rect.y, toolbarHeight, position.height - inStatusHeight);

            GUILayout.BeginArea(new Rect(0, toolbarHeight, position.width, rect.y - toolbarHeight));
            vcMultiColumnAssetList.DrawGUI();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(0, rect.y, position.width, position.height - rect.y));
            DrawStatus();
            GUILayout.EndArea();

            EditorGUILayout.EndVertical();
        }

        private void HandleInput()
        {
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.F5)
                {
                    VCCommands.Instance.Status();
                    Event.current.Use();
                }
                if (Event.current.keyCode == KeyCode.Delete)
                {
                    VCUtility.VCDeleteWithConfirmation(GetSelectedAssets());
                    Event.current.Use();
                }
            }
        }

        private void DrawToolbar()
        {
            GUILayoutOption[] buttonLayout = {GUILayout.MaxWidth(50)};
            {
                // Buttons at top        
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

                using (new PushState<bool>(GUI.enabled, VCCommands.Instance.Ready, v => GUI.enabled = v))
                {
                    if (GUILayout.Button(Terminology.status, EditorStyles.toolbarButton, buttonLayout))
                    {
                        VCCommands.Instance.StatusTask();
                    }
                    if (GUILayout.Button(Terminology.update, EditorStyles.toolbarButton, buttonLayout))
                    {
                        VCCommands.Instance.UpdateTask();
                    }
                    if (GUILayout.Button(Terminology.revert, EditorStyles.toolbarButton, buttonLayout))
                    {
                        VCCommands.Instance.Revert(GetSelectedAssets().ToArray());
                    }
                    if (GUILayout.Button(Terminology.delete, EditorStyles.toolbarButton, buttonLayout))
                    {
                        VCCommands.Instance.Delete(GetSelectedAssets().ToArray());
                    }
                    if (GUILayout.Button(Terminology.unlock, EditorStyles.toolbarButton, buttonLayout))
                    {
                        VCCommands.Instance.ReleaseLock(GetSelectedAssets().ToArray());
                    }
                    if (GUILayout.Button(Terminology.add, EditorStyles.toolbarButton, buttonLayout))
                    {
                        VCCommands.Instance.AddTask(GetSelectedAssets().ToArray());
                    }
                    if (GUILayout.Button(Terminology.commit, EditorStyles.toolbarButton, buttonLayout))
                    {
                        VCCommands.Instance.CommitDialog(GetSelectedAssets().ToArray(), true);
                    }
                }

                GUILayout.FlexibleSpace();
                
                bool newShowUnversioned = GUILayout.Toggle(showUnversioned, "Unversioned", EditorStyles.toolbarButton, new[] {GUILayout.MaxWidth(80)});
                if (newShowUnversioned != showUnversioned)
                {
                    showUnversioned = newShowUnversioned;
                    UpdateFilteringOfKeys();
                }

                bool newShowMeta = GUILayout.Toggle(showMeta, "Meta", EditorStyles.toolbarButton, new[] {GUILayout.MaxWidth(50)});
                if (newShowMeta != showMeta)
                {
                    showMeta = newShowMeta;
                    UpdateFilteringOfKeys();
                }

                GUILayout.Space(7.0f);

                if (GUILayout.Button("Settings", EditorStyles.toolbarButton, new[] { GUILayout.MaxWidth(55) }))
                {
                    if (settingsWindow == null)
                    {
                        settingsWindow = CreateInstance<VCSettingsWindow>();
                        settingsWindow.title = "Version Control Settings";
                        settingsWindow.ShowUtility();
                    }
                    else
                    {
                        settingsWindow.Close();
                    }
                }

                GUILayout.Space(7.0f);

                bool vcsOn = VCSettings.VCEnabled;
                using (GUIColor(vcsOn ? Color.green : Color.red))
                {
                    if (GUILayout.Button(new GUIContent(vcsOn ? "On" : "Off", "Toggle Version Control"), EditorStyles.toolbarButton, new[] {GUILayout.MaxWidth(25)}))
                    {
                        VCSettings.VCEnabled = !VCSettings.VCEnabled;
                    }
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Separator();
            }
        }
        
        private Vector2 statusScroll = Vector2.zero;

        private void DrawStatus()
        {
            statusScroll = EditorGUILayout.BeginScrollView(statusScroll, false, false);
            GUILayout.TextArea(commandInProgress, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        public static PushState<Color> GUIColor(Color color)
        {
            return new PushState<Color>(GUI.color, GUI.color = color, c => GUI.color = c);
        }

        public static PushState<Color> BackgroundColor(Color color)
        {
            return new PushState<Color>(GUI.backgroundColor, GUI.backgroundColor = color, c => GUI.backgroundColor = c);
        }
    }
}

