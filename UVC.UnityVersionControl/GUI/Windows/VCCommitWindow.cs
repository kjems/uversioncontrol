// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using MultiColumnState = MultiColumnState<string, UnityEngine.GUIContent>;

namespace UVC.UserInterface
{
    using Logging;
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    internal class VCCommitWindow : EditorWindow
    {
        // Const
        const float minimumControlHeight = 50;
        const int maxProgressSize = 10000;

        // State
        public IEnumerable<string> commitedFiles = new List<string>();

        private IEnumerable<ComposedString> assetPaths = new List<ComposedString>();
        private IEnumerable<ComposedString> depedencyAssetPaths = new List<ComposedString>();
        private bool firstTime = true;
        private bool commitInProgress = false;
        private bool commitCompleted = false;
        private string commitProgress = "";
        private float commitMessageHeight;
        private string commitMessage = null;
        private string CommitMessage
        {
            get { return commitMessage ?? (commitMessage = EditorPrefs.GetString("VCCommitWindow/CommitMessage", "")); }
            set { commitMessage = value; EditorPrefs.SetString("VCCommitWindow/CommitMessage", commitMessage); }
        }

        // Cache
        private Vector2 scrollViewVectorLog = Vector2.zero;
        private Vector2 statusScroll = Vector2.zero;
        private Rect rect;

        VCMultiColumnAssetList vcMultiColumnAssetList;

        public static void Init()
        {
            GetWindow<VCCommitWindow>("Commit");
        }

        public void SetAssetPaths(IEnumerable<string> assets, IEnumerable<string> dependencies)
        {
            DebugLog.Log("VCCommitWindow:SetAssetPaths");
            ProfilerUtilities.BeginSample("CommitWindow::SetAssetPaths");
            assetPaths = assets.Select(s => new ComposedString(s)).ToList();
            depedencyAssetPaths = dependencies.Select(s => new ComposedString(s)).ToList();
            vcMultiColumnAssetList.SetBaseFilter(BaseFilter);
            RefreshSelection();
            ProfilerUtilities.EndSample();
        }

        private bool BaseFilter(VersionControlStatus vcStatus)
        {
            using (PushStateUtility.Profiler("CommitWindow::BaseFilter"))
            {
                var metaStatus = vcStatus.MetaStatus();
                bool interesting = (vcStatus.fileStatus != VCFileStatus.None &&
                                    (vcStatus.fileStatus != VCFileStatus.Normal || (metaStatus != null && metaStatus.fileStatus != VCFileStatus.Normal))) ||
                                    vcStatus.lockStatus == VCLockStatus.LockedHere ||
                                    vcStatus.property == VCProperty.Modified;

                if (!interesting) return false;
                ComposedString key = vcStatus.assetPath.TrimEnd(VCCAddMetaFiles.meta);
                return (assetPaths.Contains(key) || depedencyAssetPaths.Contains(key));
            }
        }

        private void RefreshSelection()
        {
            if (VCSettings.SelectiveCommit)
            {
                vcMultiColumnAssetList.ForEachRow(r => vcMultiColumnAssetList.SetMasterSelection(r.data, VCSettings.IncludeDepedenciesAsDefault || assetPaths.Contains(r.data.assetPath)));
            }
            else
            {
                vcMultiColumnAssetList.ForEachRow(r => r.selected = VCSettings.IncludeDepedenciesAsDefault || assetPaths.Contains(r.data.assetPath));
            }
            Repaint();
        }

        private void OnEnable()
        {
            AssetDatabaseRefreshManager.PauseAssetDatabaseRefresh();
            minSize  = new Vector2(1000, 400);
            position = new Rect {
                xMin    = Screen.width * 0.5f - this.minSize.x,
                yMin    = Screen.height * 0.5f - this.minSize.y,
                width   = this.minSize.x,
                height  = this.minSize.y
            };
            commitMessageHeight = EditorPrefs.GetFloat("VCCommitWindow/commitMessageHeight", 140.0f);
            rect = new Rect(0, commitMessageHeight, position.width, 10.0f);
            vcMultiColumnAssetList = new VCMultiColumnAssetList(Repaint, VCSettings.SelectiveCommit);
            VCCommands.Instance.StatusCompleted += RefreshSelection;
        }

        private void OnDisable()
        {
            EditorPrefs.SetFloat("VCCommitWindow/commitMessageHeight", commitMessageHeight);
            vcMultiColumnAssetList.Dispose();
            AssetDatabaseRefreshManager.ResumeAssetDatabaseRefresh();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            if (commitInProgress) CommitProgressGUI();
            else CommitMessageGUI();
            EditorGUILayout.EndVertical();
        }

        private void CommitProgressGUI()
        {
            scrollViewVectorLog = EditorGUILayout.BeginScrollView(scrollViewVectorLog, false, false);
            GUILayout.TextArea(commitProgress);
            EditorGUILayout.EndScrollView();
            if (commitCompleted)
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close"))
                {
                    Close();
                }
            }
        }

        private void CommitMessageGUI()
        {
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);
            rect = GUIControls.DragButton(rect, GUIContent.none, null);
            rect.y = position.height - rect.y;
            rect.x = 0.0f;
            rect.width = position.width;
            commitMessageHeight = rect.y = Mathf.Clamp(rect.y, minimumControlHeight, position.height - minimumControlHeight);

            GUILayout.BeginArea(new Rect(0, 0, position.width, rect.y));
            vcMultiColumnAssetList.DrawGUI();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(0, rect.y, position.width, position.height - rect.y));
            DrawButtons();
            GUILayout.EndArea();
        }

        private void DrawButtons()
        {
            EditorGUILayout.BeginHorizontal();

            GUI.SetNextControlName("CommitMessage");
            using (GUILayoutHelper.BackgroundColor(CommitMessage.Length < 10 ? new Color(1, 0, 0) : new Color(0, 1, 0)))
            {
                statusScroll = EditorGUILayout.BeginScrollView(statusScroll, false, false);
                CommitMessage = EditorGUILayout.TextArea(CommitMessage, GUILayout.MinWidth(100), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
            if (firstTime)
            {
                GUI.FocusControl("CommitMessage");
                firstTime = false;
            }

            using (new PushState<bool>(GUI.enabled, VCCommands.Instance.Ready, v => GUI.enabled = v))
            {
                if (GUILayout.Button(Terminology.commit, GUILayout.Width(100)))
                {
                    var selection = VCSettings.SelectiveCommit ? vcMultiColumnAssetList.GetMasterSelection() : vcMultiColumnAssetList.GetSelection();
                    if (selection.Count() != 0)
                    {
                        var selectedAssets = selection.Select(status => status.assetPath).Select(cstr => cstr.Compose()).ToList();
                        VCCommands.Instance.ProgressInformation += s =>
                        {
                            commitProgress = commitProgress + s;
                            if (commitProgress.Length > maxProgressSize)
                            {
                                commitProgress = commitProgress.Substring(commitProgress.Length - maxProgressSize);
                            }
                            statusScroll.y = Mathf.Infinity;
                            Repaint();
                        };
                        var commitTask = VCCommands.Instance.CommitTask(selectedAssets, CommitMessage);
                        commitTask.ContinueWithOnNextUpdate(result =>
                        {
                            if (result)
                            {
                                commitedFiles = selectedAssets;
                                CommitMessage = "";
                                Repaint();
                                if (VCSettings.AutoCloseAfterSuccess) Close();
                            }
                            commitCompleted = true;
                        });
                        commitInProgress = true;
                    }
                    else
                    {
                        ShowNotification(new GUIContent("No files selected"));
                    }
                }
                if (GUILayout.Button("Cancel", GUILayout.Width(100)))
                {
                    Close();
                }
            }
            EditorGUILayout.EndHorizontal();
            if (vcMultiColumnAssetList.GetSelection().Any())
            {
                RemoveNotification();
            }
        }
    }
}

