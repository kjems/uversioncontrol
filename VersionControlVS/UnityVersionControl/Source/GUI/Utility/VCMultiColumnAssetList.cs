// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

using MultiColumnState = MultiColumnState<string, UnityEngine.GUIContent>;
using MultiColumnViewOption = MultiColumnView.MultiColumnViewOption<string>;

namespace VersionControl.UserInterface
{
    internal class VCMultiColumnAssetList : IDisposable
    {
        private IEnumerable<string> interrestingKeys = new List<string>();
        private MultiColumnState multiColumnState;
        private MultiColumnViewOption options;

        private MultiColumnState.Column columnAssetPath;
        private MultiColumnState.Column columnOwner;
        private MultiColumnState.Column columnFileStatus;
        private MultiColumnState.Column columnMetaStatus;
        private MultiColumnState.Column columnFileType;
        private MultiColumnState.Column columnConflict;
        private MultiColumnState.Column columnChangelist;

        private Func<string, VersionControlStatus, bool> guiFilter;
        private Func<string, VersionControlStatus, bool> baseFilter;

        private static VersionControlStatus GetAssetStatus(string assetPath)
        {
            return VCCommands.Instance.GetAssetStatus(assetPath);
        }

        private static VersionControlStatus GetMetaStatus(string assetPath)
        {
            return VCCommands.Instance.GetAssetStatus(assetPath).MetaStatus();
        }

        public VCMultiColumnAssetList()
        {
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
                return new GUIContent(assetStatus.treeConflictStatus.ToString(), IconUtils.squareIcon.GetTexture(AssetStatusUtils.GetStatusColor(assetStatus, false)));
            return new GUIContent(assetStatus.fileStatus.ToString(), IconUtils.circleIcon.GetTexture(AssetStatusUtils.GetStatusColor(assetStatus, false)));
        }

        private void Initialize()
        {
            baseFilter = (k, s) => false;
            guiFilter = (k, s) => true;

            columnAssetPath = new MultiColumnState.Column(new GUIContent("AssetPath"), data => new GUIContent(data));
            columnOwner = new MultiColumnState.Column(new GUIContent("Owner"), data => new GUIContent(GetAssetStatus(data).owner, GetAssetStatus(data).lockToken));
            columnFileStatus = new MultiColumnState.Column(new GUIContent("Status"), data => GetFileStatusContent(GetAssetStatus(data)));
            columnMetaStatus = new MultiColumnState.Column(new GUIContent("Meta"), data => GetFileStatusContent(GetMetaStatus(data)));
            columnFileType = new MultiColumnState.Column(new GUIContent("Type"), data => new GUIContent(GetFileType(data)));
            columnConflict = new MultiColumnState.Column(new GUIContent("Conflict"), data => new GUIContent(GetAssetStatus(data).treeConflictStatus.ToString()));
            columnChangelist = new MultiColumnState.Column(new GUIContent("ChangeList"), data => new GUIContent(GetAssetStatus(data).changelist));

            var editorSkin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector);
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

            Func<GenericMenu> rowRightClickMenu = () =>
            {
                var selected = multiColumnState.GetSelected();
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
                    Selection.objects = selectedObjs;
                    EditorApplication.ExecuteMenuItem((Application.platform == RuntimePlatform.OSXEditor ? "Assets/Reveal in Finder" : "Assets/Show in Explorer"));
                });
                return menu;
            };

            Func<MultiColumnState.Column, GenericMenu> headerRightClickMenu = column =>
            {
                var menu = new GenericMenu();
                //menu.AddItem(new GUIContent("Remove"), false, () => { ToggleColumn(column); });
                return menu;
            };

            options = new MultiColumnViewOption
            {
                headerStyle = editorSkin.button,
                rowStyle = editorSkin.label,
                rowRightClickMenu = rowRightClickMenu,
                headerRightClickMenu = headerRightClickMenu,
                widths = new float[] { 200 },
                doubleClickAction = assetPath =>
                {
                    if (VCUtility.IsTextAsset(assetPath) && VCUtility.ManagedByRepository(GetAssetStatus(assetPath)))
                        VCUtility.DiffWithBase(assetPath);
                    else
                        AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(assetPath));
                }
            };

            options.headerStyle.fixedHeight = 20.0f;
            options.rowStyle.onNormal.background = IconUtils.CreateSquareTexture(4, 1, new Color(0.24f, 0.5f, 0.87f, 0.75f));

            multiColumnState.AddColumn(columnAssetPath);
            options.widthTable.Add(columnAssetPath.GetHeader().text, 500);

            multiColumnState.AddColumn(columnFileStatus);
            options.widthTable.Add(columnFileStatus.GetHeader().text, 90);

            multiColumnState.AddColumn(columnMetaStatus);
            options.widthTable.Add(columnMetaStatus.GetHeader().text, 90);

            multiColumnState.AddColumn(columnFileType);
            options.widthTable.Add(columnFileType.GetHeader().text, 80);

            multiColumnState.AddColumn(columnOwner);
            options.widthTable.Add(columnOwner.GetHeader().text, 60);

            multiColumnState.AddColumn(columnChangelist);
            options.widthTable.Add(columnChangelist.GetHeader().text, 120);

            //columnConflictState.AddColumn(columnConflict);
            options.widthTable.Add(columnConflict.GetHeader().text, 80);
        }

        public void SetBaseFilter(Func<string, VersionControlStatus, bool> baseFilter)
        {
            this.baseFilter = baseFilter;
            RefreshBaseFilter();
        }

        public void SetGUIFilter(Func<string, VersionControlStatus, bool> guiFilter)
        {
            this.guiFilter = guiFilter;
            RefreshGUIFilter();
        }

        private void RefreshBaseFilter()
        {
            Profiler.BeginSample("MultiColumnAssetList::RefreshBaseFilter");

            interrestingKeys = VCCommands.Instance.GetFilteredAssets(baseFilter);
            RefreshGUIFilter();

            Profiler.EndSample();
        }

        private static string GetFileType(string assetPath)
        {
            int indexOfLastDot = assetPath.LastIndexOf(".", StringComparison.Ordinal);
            return (indexOfLastDot > 0) ? assetPath.Substring(assetPath.LastIndexOf(".", StringComparison.Ordinal) + 1) : "[folder]";
        }

        public void RefreshGUIFilter()
        {
            Profiler.BeginSample("MultiColumnAssetList::RefreshGUIFilter");
            multiColumnState.Refresh(interrestingKeys.Where(k => guiFilter(k, GetAssetStatus(k))).ToList());
            Profiler.EndSample();
        }

        public IEnumerable<string> GetSelectedAssets()
        {
            return multiColumnState.GetSelected();
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

        public void DrawGUI()
        {
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
