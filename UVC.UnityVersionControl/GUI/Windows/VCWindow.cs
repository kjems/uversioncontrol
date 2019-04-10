// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

// This script is a window to display local changes and to perform commands on
// the repository like updating and committing files.
// SVNIntegration is used to get state and execute commands on the repository.
//
// Although functional the general quality of this file is poor and need a refactor
#pragma warning disable CS4014

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using MultiColumnState = MultiColumnState<string, UnityEngine.GUIContent>;

namespace UVC.UserInterface
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    internal class VCWindow : EditorWindow
    {
        // Const
        const float toolbarHeight = 18.0f;
        const float inStatusHeight = 18.0f;
        const int maxProgressSize = 10000;
        private readonly Color activeColor = new Color(0.8f, 0.8f, 1.0f);

        // State
        private bool showUnversioned = true;
        private bool showMeta = true;
        private bool showModifiedNoLock = true;
        private bool showProjectSetting = false;
        private float statusHeight = 1000;
        private bool updateInProgress = false;
        private bool refreshInProgress = false;
        private string commandInProgress = "";
        private string currentBranch = "<unknown>";
        private string searchString;
        private SearchField searchField;
        private VCMultiColumnAssetList vcMultiColumnAssetList;
        private VCSettingsWindow settingsWindow;
        private Rect rect;
        private int updateCounter = 0;

        // Cache
        private Vector2 statusScroll = Vector2.zero;


        [MenuItem("Window/UVC/Overview Window", false, 1)]
        public static void Init()
        {
            GetWindow<VCWindow>(false, "UVC.Overview Window");
        }

        private bool GUIFilter(VersionControlStatus vcStatus)
        {
            var metaStatus = vcStatus.MetaStatus();
            bool projectSetting = vcStatus.assetPath.StartsWith("ProjectSettings/");
            bool unversioned = vcStatus.fileStatus == VCFileStatus.Unversioned;
            bool meta = metaStatus.fileStatus != VCFileStatus.Normal && vcStatus.fileStatus == VCFileStatus.Normal;
            bool modifiedNoLock = !projectSetting && vcStatus.ModifiedOrLocalEditAllowed();

            bool rest = !unversioned && !meta && !modifiedNoLock && !projectSetting;
            return ((showUnversioned && unversioned) || (showMeta && meta) || (showModifiedNoLock && modifiedNoLock) || (showProjectSetting && projectSetting) || rest) &&
                   (string.IsNullOrEmpty(searchString) || vcStatus.assetPath.Compose().Contains(searchString) || vcStatus.changelist.Compose().Contains(searchString));
        }

        // This is a performance critical function
        private bool BaseFilter(VersionControlStatus vcStatus)
        {
            if (!vcStatus.Reflected) return false;

            bool assetCriteria = vcStatus.fileStatus != VCFileStatus.None && (vcStatus.ModifiedOrLocalEditAllowed() || vcStatus.fileStatus != VCFileStatus.Normal || !ComposedString.IsNullOrEmpty(vcStatus.changelist)) && vcStatus.fileStatus != VCFileStatus.Ignored;
            if (assetCriteria) return true;

            bool property = vcStatus.property == VCProperty.Modified || vcStatus.property == VCProperty.Conflicted;
            if (property) return true;

            bool localLock = vcStatus.lockStatus == VCLockStatus.LockedHere;
            if (localLock) return true;

            var metaStatus = vcStatus.MetaStatus();
            bool metaCriteria = metaStatus.fileStatus != VCFileStatus.Normal && (metaStatus.fileStatus != VCFileStatus.None || !ComposedString.IsNullOrEmpty(metaStatus.changelist)) && metaStatus.fileStatus != VCFileStatus.Ignored;

            if (metaCriteria) return true;

            return false;
        }

        private void UpdateFilteringOfKeys()
        {
            vcMultiColumnAssetList.RefreshGUIFilter();
        }

        private List<string> GetSelectedAssets()
        {
            return vcMultiColumnAssetList.GetSelection().Select(status => status.assetPath).Select(cstr => cstr.Compose()).ToList();
        }

        virtual protected void OnEnable()
        {
            showUnversioned = EditorPrefs.GetBool("VCWindow/showUnversioned", true);
            showMeta = EditorPrefs.GetBool("VCWindow/showMeta", true);
            showModifiedNoLock = EditorPrefs.GetBool("VCWindow/showModifiedNoLock", true);
            statusHeight = EditorPrefs.GetFloat("VCWindow/statusHeight", 400.0f);

            searchField = new SearchField();

            vcMultiColumnAssetList = new VCMultiColumnAssetList();

            vcMultiColumnAssetList.SetBaseFilter(BaseFilter);
            vcMultiColumnAssetList.SetGUIFilter(GUIFilter);

            VCCommands.Instance.StatusCompleted += RefreshGUI;
            VCCommands.Instance.OperationCompleted += OperationComplete;
            VCCommands.Instance.ProgressInformation += ProgressInformation;
            VCSettings.SettingChanged += Repaint;

            rect = new Rect(0, statusHeight, position.width, 40.0f);

            RefreshCurrentBranch();
        }

        virtual protected void OnDisable()
        {
            EditorPrefs.SetBool("VCWindow/showUnversioned", showUnversioned);
            EditorPrefs.SetBool("VCWindow/showMeta", showMeta);
            EditorPrefs.SetBool("VCWindow/showModifiedNoLock", showModifiedNoLock);
            EditorPrefs.SetFloat("VCWindow/statusHeight", statusHeight);

            VCCommands.Instance.StatusCompleted -= RefreshGUI;
            VCCommands.Instance.OperationCompleted -= OperationComplete;
            VCCommands.Instance.ProgressInformation -= ProgressInformation;
            VCSettings.SettingChanged -= Repaint;

            vcMultiColumnAssetList.Dispose();
            if (updateInProgress) EditorUtility.ClearProgressBar();
        }

        private void ProgressInformation(string progress)
        {
            if (updateInProgress)
            {
                updateCounter++;
                EditorUtility.DisplayProgressBar(VCSettings.VersionControlBackend + " Updating", progress, 1.0f - (1.0f / updateCounter));
            }
            commandInProgress = commandInProgress + progress;
            if (commandInProgress.Length > maxProgressSize)
            {
                commandInProgress = commandInProgress.Substring(commandInProgress.Length - maxProgressSize);
            }
            statusScroll.y = Mathf.Infinity;
            Repaint();
        }

        private void OperationComplete(OperationType operation, IEnumerable<VersionControlStatus> statusBefore, IEnumerable<VersionControlStatus> statusAfter, bool success)
        {
            if (operation == OperationType.Update)
            {
                EditorUtility.ClearProgressBar();
                updateInProgress = false;
                RefreshGUI();
                updateCounter = 0;
            }
        }

        private void RefreshCurrentBranch()
        {
            VCCommands.Instance.GetCurrentBranchTask().ContinueWithOnNextUpdate(cb =>
            {
                currentBranch = cb;
                Repaint();
            });
        }

        private void RefreshGUI()
        {
            RefreshCurrentBranch();
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
                    RefreshStatus();
                    Event.current.Use();
                }
                if (Event.current.keyCode == KeyCode.Delete)
                {
                    VCUtility.VCDeleteWithConfirmation(GetSelectedAssets());
                    Event.current.Use();
                }
            }
        }

        private void RefreshStatus()
        {
            refreshInProgress = true;
            VCCommands.Instance.FlushFiles();
            bool remoteProjectReflection = VCSettings.ProjectReflectionMode == VCSettings.EReflectionLevel.Remote;
            VCCommands.Instance.DeactivateRefreshLoop();
            VCCommands.Instance.ClearDatabase();
            var statusLevel = remoteProjectReflection ? StatusLevel.Remote : StatusLevel.Local;
            var detailLevel = remoteProjectReflection ? DetailLevel.Verbose : DetailLevel.Normal;
            VCCommands.Instance.StatusTask(statusLevel, detailLevel).ContinueWithOnNextUpdate(t =>
            {
                VCCommands.Instance.ActivateRefreshLoop();
                refreshInProgress = false;
                RefreshGUI();
            });

        }

        private void DrawToolbar()
        {
            GUILayoutOption[] buttonLayout = { GUILayout.MaxWidth(50) };
            {
                // Buttons at top
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

                using (new PushState<bool>(GUI.enabled, VCCommands.Instance.Ready && !refreshInProgress, v => GUI.enabled = v))
                {
                    if (GUILayout.Button(Terminology.status, EditorStyles.toolbarButton, buttonLayout))
                    {
                        RefreshStatus();
                    }
                    if (GUILayout.Button(Terminology.update, EditorStyles.toolbarButton, buttonLayout))
                    {
                        updateInProgress = true;
                        EditorUtility.DisplayProgressBar(VCSettings.VersionControlBackend + " Updating", "", 0.0f);
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
                        VCCommands.Instance.CommitDialog(GetSelectedAssets().ToList(), true);
                    }
                }
                GUILayout.Space(7);
                GUILayout.Label(currentBranch, EditorStyles.toolbarTextField,GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));

                if (GUILayout.Button(Terminology.branch, EditorStyles.toolbarButton, buttonLayout))
                {
                    BranchWindow.Create();
                }
                GUILayout.FlexibleSpace();


                string newSearchString = searchField.OnToolbarGUI(searchString);
                if (newSearchString != searchString)
                {
                    searchString = newSearchString;
                    UpdateFilteringOfKeys();
                }

                bool newShowModifiedProjectSettings = GUILayout.Toggle(showProjectSetting, "Project Settings", EditorStyles.toolbarButton, new[] { GUILayout.MaxWidth(95) });
                if (newShowModifiedProjectSettings != showProjectSetting)
                {
                    showProjectSetting = newShowModifiedProjectSettings;
                    UpdateFilteringOfKeys();
                }

                bool newShowModifiedNoLock = GUILayout.Toggle(showModifiedNoLock, Terminology.localModified , EditorStyles.toolbarButton, new[] { GUILayout.MaxWidth(90) });
                if (newShowModifiedNoLock != showModifiedNoLock)
                {
                    showModifiedNoLock = newShowModifiedNoLock;
                    UpdateFilteringOfKeys();
                }

                bool newShowUnversioned = GUILayout.Toggle(showUnversioned, "Unversioned", EditorStyles.toolbarButton, new[] { GUILayout.MaxWidth(80) });
                if (newShowUnversioned != showUnversioned)
                {
                    showUnversioned = newShowUnversioned;
                    UpdateFilteringOfKeys();
                }

                bool newShowMeta = GUILayout.Toggle(showMeta, "Meta", EditorStyles.toolbarButton, new[] { GUILayout.MaxWidth(40) });
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
                        settingsWindow.titleContent = new GUIContent("Version Control Settings");
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
                    if (GUILayout.Button(new GUIContent(vcsOn ? "On" : "Off", "Toggle Version Control"), EditorStyles.toolbarButton, new[] { GUILayout.MaxWidth(25) }))
                    {
                        commandInProgress = "";
                        VCSettings.VCEnabled = !VCSettings.VCEnabled;
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Separator();
            }
        }

        private void DrawStatus()
        {
            GUILayout.Space(6);
            statusScroll = EditorGUILayout.BeginScrollView(statusScroll, false, false);
            var originalColor = GUI.backgroundColor;
            if (updateInProgress) GUI.backgroundColor = activeColor;
            GUILayout.TextArea(commandInProgress, GUILayout.ExpandHeight(true));
            GUI.backgroundColor = originalColor;
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

