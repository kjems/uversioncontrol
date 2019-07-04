// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using MultiColumnState = MultiColumnState<UVC.VersionControlStatus, UnityEngine.GUIContent>;
using MultiColumnViewOption = MultiColumnView.MultiColumnViewOption<UVC.VersionControlStatus>;

namespace UVC.UserInterface
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    internal class VCMultiColumnAssetList : IDisposable
    {
        private HashSet<VersionControlStatus> masterSelection = new HashSet<VersionControlStatus>();
        private bool showMasterSelection = false;
        private Action repaint;
        private IEnumerable<VersionControlStatus> interrestingStatus;
        private MultiColumnState multiColumnState;
        private MultiColumnViewOption options;

        private MultiColumnState.Column columnSelection;
        private MultiColumnState.Column columnAssetPath;
        private MultiColumnState.Column columnOwner;
        private MultiColumnState.Column columnFileStatus;
        private MultiColumnState.Column columnMetaStatus;
        private MultiColumnState.Column columnFileType;
        private MultiColumnState.Column columnConflict;
        private MultiColumnState.Column columnChangelist;

        private Func<VersionControlStatus, bool> guiFilter;
        private Func<VersionControlStatus, bool> baseFilter;

        private static VersionControlStatus GetAssetStatus(string assetPath)
        {
            return VCCommands.Instance.GetAssetStatus(assetPath);
        }

        private static VersionControlStatus GetMetaStatus(string assetPath)
        {
            return VCCommands.Instance.GetAssetStatus(assetPath).MetaStatus();
        }

        public VCMultiColumnAssetList(Action repaint = null, bool showMasterSelection = false)
        {
            this.repaint = repaint;
            this.showMasterSelection = showMasterSelection;
            Initialize();
            VCCommands.Instance.StatusCompleted += RefreshGUI;
            VCSettings.SettingChanged += RefreshGUI;
        }

        public void Dispose()
        {
            VCCommands.Instance.StatusCompleted -= RefreshGUI;
            VCSettings.SettingChanged -= RefreshGUI;
        }

        private static GUIContent GetFileStatusContent(VersionControlStatus assetStatus)
        {
            if (assetStatus.treeConflictStatus != VCTreeConflictStatus.Normal)
                return new GUIContent(assetStatus.treeConflictStatus.ToString(), IconUtils.squareIcon.GetTexture(AssetStatusUtils.GetStatusColor(assetStatus, true)));
            return new GUIContent(AssetStatusUtils.GetStatusText(assetStatus), IconUtils.circleIcon.GetTexture(AssetStatusUtils.GetStatusColor(assetStatus, true)));
        }


        private void Initialize()
        {
            baseFilter = s => false;
            guiFilter = s => true;

            columnSelection = new MultiColumnState.Column(new GUIContent("[]"), data => new GUIContent(masterSelection.Contains(data) ? " ☑" : " ☐"));
            columnAssetPath = new MultiColumnState.Column(new GUIContent("AssetPath"), data => new GUIContent(data.assetPath.Compose()));
            columnOwner = new MultiColumnState.Column(new GUIContent("Owner"), data => new GUIContent(data.owner, data.lockToken));
            columnFileStatus = new MultiColumnState.Column(new GUIContent("Status"), GetFileStatusContent);
            columnMetaStatus = new MultiColumnState.Column(new GUIContent("Meta"), data => GetFileStatusContent(data.MetaStatus()));
            columnFileType = new MultiColumnState.Column(new GUIContent("Type"), data => new GUIContent(GetFileType(data.assetPath.Compose())));
            columnConflict = new MultiColumnState.Column(new GUIContent("Conflict"), data => new GUIContent(data.treeConflictStatus.ToString()));
            columnChangelist = new MultiColumnState.Column(new GUIContent("ChangeList"), data => new GUIContent(data.changelist.Compose()));

            var guiSkin = EditorGUIUtility.GetBuiltinSkin( EditorGUIUtility.isProSkin ? EditorSkin.Scene : EditorSkin.Inspector);
            multiColumnState = new MultiColumnState();

            multiColumnState.Comparer = (r1, r2, c) =>
            {
                var r1Text = c.GetContent(r1.data).text;
                var r2Text = c.GetContent(r2.data).text;
                if (r1Text == null) r1Text = "";
                if (r2Text == null) r2Text = "";
                //D.Log("Comparing: " + r1Text + " with " + r2Text + " : " + r1Text.CompareTo(r2Text));
                return String.CompareOrdinal(r1Text, r2Text);
            };

            Func<MultiColumnState.Row, MultiColumnState.Column, GenericMenu> rowRightClickMenu = (row, column) =>
            {
                var selected = multiColumnState.GetSelected().Select(status => status.assetPath.Compose()).ToList();
                if (!selected.Any()) return new GenericMenu();
                GenericMenu menu = new GenericMenu();
                if (selected.Count() == 1) VCGUIControls.CreateVCContextMenu(ref menu, selected.First());
                else VCGUIControls.CreateVCContextMenu(ref menu, selected);
                var selectedObjs = selected.Select(a => AssetDatabase.LoadMainAssetAtPath(a)).ToArray();
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Show in Project"), false, () =>
                {
                    Selection.objects = selectedObjs;
                    EditorGUIUtility.PingObject(Selection.activeObject);
                });
                menu.AddItem(new GUIContent("Show on Harddisk"), false, () =>
                {
                    foreach (string item in selected)
                    {
                        EditorUtility.RevealInFinder(item);
                    }
                });
                return menu;
            };

            Func<MultiColumnState.Column, GenericMenu> headerRightClickMenu = column =>
            {
                var menu = new GenericMenu();
                //menu.AddItem(new GUIContent("Remove"), false, () => { ToggleColumn(column); });
                return menu;
            };

            // Return value of true steals the click from normal selection, false does not.
            Func<MultiColumnState.Row, MultiColumnState.Column, bool> cellClickAction = (row, column) =>
            {
                GUI.FocusControl("");
                if (column == columnSelection)
                {
                    var currentSelection = multiColumnState.GetSelected();
                    if (currentSelection.Contains(row.data))
                    {
                        bool currentRowSelection = masterSelection.Contains(row.data);
                        foreach (var selectionIt in currentSelection)
                        {
                            if (currentRowSelection)
                                masterSelection.Remove(selectionIt);
                            else
                                masterSelection.Add(selectionIt);
                        }
                    }
                    else
                    {
                        if (masterSelection.Contains(row.data))
                            masterSelection.Remove(row.data);
                        else
                            masterSelection.Add(row.data);
                    }
                    return true;
                }
                return false;
                //D.Log(row.data.assetPath.Compose() + " : "  + column.GetContent(row.data).text);
            };

            options = new MultiColumnViewOption
            {
                headerStyle = new GUIStyle(guiSkin.button),
                rowStyle = new GUIStyle(guiSkin.label),
                rowRightClickMenu = rowRightClickMenu,
                headerRightClickMenu = headerRightClickMenu,
                cellClickAction = cellClickAction,
                widths = new float[] { 200 },
                doubleClickAction = status =>
                {
                    if (MergeHandler.IsDiffableAsset(status.assetPath) && VCUtility.ManagedByRepository(status) && status.fileStatus == VCFileStatus.Conflicted)
                    {
                        var assetPath = status.assetPath.Compose();
                        VCCommands.Instance.GetConflict(assetPath, out var basePath, out var yours, out var theirs);
                        MergeHandler.ResolveConflict(assetPath, basePath, theirs, yours);
                    }
                    else if (MergeHandler.IsDiffableAsset(status.assetPath) && VCUtility.ManagedByRepository(status) && status.fileStatus == VCFileStatus.Modified)
                        MergeHandler.DiffWithBase(status.assetPath.Compose());
                    else
                    {
                        string path = status.assetPath.Compose();
                        var obj = AssetDatabase.LoadMainAssetAtPath(path);

                        if (path.StartsWith("Assets"))
                        {
                            if (AssetDatabase.IsValidFolder(path) || obj == null)
                            {
                                EditorUtility.RevealInFinder(path);
                            }
                            else
                            {
                                bool result = AssetDatabase.OpenAsset(obj);
                                if (!result)
                                {
                                    EditorUtility.RevealInFinder(path);
                                }
                            }
                        }
                        else
                        {
                            EditorUtility.RevealInFinder(path);
                        }
                    }
                }
            };

            options.headerStyle.fixedHeight = 20.0f;
            options.rowStyle.onNormal.background = IconUtils.CreateSquareTexture(4, 1, new Color(0.24f, 0.5f, 0.87f, 0.75f));
            options.rowStyle.margin = new RectOffset(2, 2, 2, 1);
            options.rowStyle.border = new RectOffset(0, 0, 0, 0);
            options.rowStyle.padding = new RectOffset(0, 0, 0, 0);

            if (showMasterSelection)
            {
                multiColumnState.AddColumn(columnSelection);
                options.widthTable.Add(columnSelection.GetHeader().text, 25);
            }

            multiColumnState.AddColumn(columnAssetPath);
            options.widthTable.Add(columnAssetPath.GetHeader().text, 500);

            multiColumnState.AddColumn(columnFileStatus);
            options.widthTable.Add(columnFileStatus.GetHeader().text, 90);

            multiColumnState.AddColumn(columnMetaStatus);
            options.widthTable.Add(columnMetaStatus.GetHeader().text, 100);

            multiColumnState.AddColumn(columnFileType);
            options.widthTable.Add(columnFileType.GetHeader().text, 80);

            multiColumnState.AddColumn(columnOwner);
            options.widthTable.Add(columnOwner.GetHeader().text, 60);

            multiColumnState.AddColumn(columnChangelist);
            options.widthTable.Add(columnChangelist.GetHeader().text, 120);

            //columnConflictState.AddColumn(columnConflict);
            options.widthTable.Add(columnConflict.GetHeader().text, 80);
        }

        public void SetBaseFilter(Func<VersionControlStatus, bool> newBaseFilter)
        {
            baseFilter = newBaseFilter;
            RefreshBaseFilter();
        }

        public void SetGUIFilter(Func<VersionControlStatus, bool> newGUIFilter)
        {
            guiFilter = newGUIFilter;
            RefreshGUIFilter();
        }

        private void RefreshBaseFilter()
        {
            ProfilerUtilities.BeginSample("MultiColumnAssetList::RefreshBaseFilter");
            interrestingStatus = VCCommands.Instance.GetFilteredAssets(baseFilter);
            //D.Log("RefreshBaseFilter, interrestingStatus.Count : " + interrestingStatus.Count());
            RefreshGUIFilter();
            ProfilerUtilities.EndSample();
        }

        private static string GetFileType(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return "[folder]";
            int indexOfLastDot = assetPath.LastIndexOf(".", StringComparison.Ordinal);
            int indexOfLastSlah = assetPath.LastIndexOf("/", StringComparison.Ordinal);
            return (indexOfLastDot > 0 && indexOfLastDot > indexOfLastSlah) ? assetPath.Substring(assetPath.LastIndexOf(".", StringComparison.Ordinal) + 1) : "[folder]";
        }

        public void RefreshGUIFilter()
        {
            ProfilerUtilities.BeginSample("MultiColumnAssetList::RefreshGUIFilter");
            multiColumnState.Refresh(interrestingStatus.Where(status => guiFilter(status)));
            ProfilerUtilities.EndSample();
        }

        public IEnumerable<VersionControlStatus> GetSelection()
        {
            return multiColumnState.GetSelected();
        }

        public IEnumerable<VersionControlStatus> GetMasterSelection()
        {
            return masterSelection;
        }

        public void SetMasterSelection(VersionControlStatus status, bool selected)
        {
            if (selected)
                masterSelection.Add(status);
            else
                masterSelection.Remove(status);
        }

        public void ForEachRow(Action<MultiColumnState.Row> action)
        {
            foreach (var rowIt in multiColumnState.GetRows())
            {
                action(rowIt);
            }
        }

        private void RefreshGUI()
        {
            RefreshBaseFilter();
        }

        private void ToggleMasterSelection()
        {
            var selected = multiColumnState.GetSelected();
            if(selected.Any())
            {
                bool toggle = masterSelection.Contains( selected.First() );
                foreach(var item in selected)
                {
                    if (toggle)
                        masterSelection.Remove(item);
                    else
                        masterSelection.Add(item);
                }
                if (repaint != null) repaint();
            }
        }

        public void DrawGUI()
        {
            if (GUIUtility.hotControl == 0 && GUIUtility.keyboardControl == 0 && Event.current.isKey && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
            {
                ToggleMasterSelection();
            }

            Rect rect = GUILayoutUtility.GetRect(5, float.MaxValue, 5, float.MaxValue, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUI.Box(rect, "");
            MultiColumnView.ListView(rect, multiColumnState, options);
        }


        private void ToggleColumn(MultiColumnState.Column column)
        {
            if (multiColumnState.ExistColumn(column))
                multiColumnState.RemoveColumn(column);
            else
                multiColumnState.AddColumn(column);
        }

        private bool ValidateColumn(MultiColumnState.Column column)
        {
            return !multiColumnState.ExistColumn(column) || multiColumnState.CountColumns() > 1;
        }
    }
}
