// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using MultiColumnState = MultiColumnState<string, UnityEngine.GUIContent>;

namespace VersionControl.UserInterface
{
    internal class VCCommitWindow : EditorWindow
    {
        public static void Init()
        {
            GetWindow<VCCommitWindow>("Commit");
        }

        public IEnumerable<string> commitedFiles = new List<string>();

        private string commitMessage = null;
        private string CommitMessage
        {
            get { return commitMessage ?? (commitMessage = EditorPrefs.GetString("VCCommitWindow/CommitMessage", "")); }
            set { commitMessage = value; EditorPrefs.SetString("VCCommitWindow/CommitMessage", commitMessage); }
        }

        bool firstTime = true;
        bool commitInProgress = false;
        bool commitCompleted = false;
        string commitProgress = "";
        Vector2 scrollViewVectorLog = Vector2.zero;

        IEnumerable<string> assetPaths = new List<string>();
        IEnumerable<string> depedencyAssetPaths = new List<string>();
        VCMultiColumnAssetList vcMultiColumnAssetList;

        public void SetAssetPaths(IEnumerable<string> assets, IEnumerable<string> dependencies)
        {
            Profiler.BeginSample("CommitWindow::SetAssetPaths");
            assetPaths = assets.ToList();
            depedencyAssetPaths = dependencies.ToList();
            vcMultiColumnAssetList.SetBaseFilter(BaseFilter);
            vcMultiColumnAssetList.ForEachRow(r => r.selected = VCSettings.IncludeDepedenciesAsDefault || assetPaths.Contains(r.data));
            Profiler.EndSample();
        }

        private bool BaseFilter(string key, VersionControlStatus vcStatus)
        {
            using (PushStateUtility.Profiler("CommitWindow::BaseFilter"))
            {
                key = key.EndsWith(VCCAddMetaFiles.meta) ? key.Remove(key.Length - VCCAddMetaFiles.meta.Length) : key;
                var metaStatus = vcStatus.MetaStatus();
                bool interresting = (vcStatus.fileStatus != VCFileStatus.None &&
                                    (vcStatus.fileStatus != VCFileStatus.Normal || (metaStatus != null && metaStatus.fileStatus != VCFileStatus.Normal))) ||
                                    vcStatus.lockStatus == VCLockStatus.LockedHere;

                if (!interresting) return false;
                return (assetPaths.Contains(key, System.StringComparer.InvariantCultureIgnoreCase) || depedencyAssetPaths.Contains(key, System.StringComparer.InvariantCultureIgnoreCase));
            }
        }

        private void UpdateFilteringOfKeys()
        {
            vcMultiColumnAssetList.RefreshGUIFilter();
        }

        private void StatusCompleted()
        {
            vcMultiColumnAssetList.ForEachRow(r => r.selected = VCSettings.IncludeDepedenciesAsDefault || assetPaths.Contains(r.data));
            Repaint();
        }

        private void OnEnable()
        {
            minSize = new Vector2(250,100);
            vcMultiColumnAssetList = new VCMultiColumnAssetList();
            UpdateFilteringOfKeys();
            VCCommands.Instance.StatusCompleted += StatusCompleted;
        }
        
        private void OnDisable()
        {
            vcMultiColumnAssetList.Dispose();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            if (commitInProgress)
            {
                scrollViewVectorLog = EditorGUILayout.BeginScrollView(scrollViewVectorLog, false, false);
                DrawProgress();
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
            else
            {
                vcMultiColumnAssetList.DrawGUI();
                DrawButtons();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawProgress()
        {
            GUILayout.TextArea(commitProgress);
        }


        private void DrawButtons()
        {
            EditorGUILayout.BeginHorizontal();

            GUI.SetNextControlName("CommitMessage");
            using (GUILayoutHelper.BackgroundColor(CommitMessage.Length < 10 ? new Color(1, 0, 0) : new Color(0, 1, 0)))
            {
                CommitMessage = EditorGUILayout.TextField(CommitMessage, GUILayout.MinWidth(100), GUILayout.ExpandWidth(true));
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
                    if (vcMultiColumnAssetList.GetSelectedAssets().Count() != 0)
                    {
                        VCCommands.Instance.ProgressInformation += s =>
                        {
                            commitProgress = s + "\n" + commitProgress;
                            Repaint();
                        };
                        var commitTask = VCCommands.Instance.CommitTask(vcMultiColumnAssetList.GetSelectedAssets().ToList(), CommitMessage);
                        commitTask.ContinueWithOnNextUpdate(result =>
                        {
                            if (result)
                            {
                                commitedFiles = vcMultiColumnAssetList.GetSelectedAssets();
                                CommitMessage = "";
                                Repaint();
                                if(VCSettings.AutoCloseAfterSuccess) Close();
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
            if (vcMultiColumnAssetList.GetSelectedAssets().Any())
            {
                RemoveNotification();
            }
        }
    }
}

