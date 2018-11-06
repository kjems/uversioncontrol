using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UVC.Logging;

namespace UVC.UserInterface
{
    using MultiColumnState = MultiColumnState<string, GUIContent>;
    using MultiColumnViewOption = MultiColumnView.MultiColumnViewOption<string>;
    
    internal class BranchWindow : EditorWindow
    {
        BranchMulticolumnList branchColumnList;

        private GUIContent branchPathContent = new GUIContent("Branch path", "default is '^/branches/'");

        private string currentBranch = "";
        
        private string branchpath = null;
        public string BranchPath
        {
            get { return branchpath ?? (branchpath = EditorPrefs.GetString("VCBranchWindow/BranchPath", "^/branches/")); }
            set
            {
                branchpath = value;
                EditorPrefs.SetString("VCBranchWindow/BranchPath", branchpath);
            }
        }
        
        public static void Init()
        {
            GetWindow<BranchWindow>("Branch");
        }

        private void OnDisable()
        {
        }

        private void OnEnable()
        {
            branchColumnList = new BranchMulticolumnList(Repaint);
            Refresh();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            BranchToolbarGUI();
            EditorGUILayout.EndHorizontal();
            
            BranchListGUI();
        }       
        
        void BranchToolbarGUI()
        {
            GUILayout.Label("Current", EditorStyles.miniLabel, GUILayout.Width(50));
            GUI.enabled = false;
            GUILayout.TextField(currentBranch, EditorStyles.toolbarTextField,GUILayout.MinWidth(120), GUILayout.ExpandWidth(true));
            GUI.enabled = true;
            GUILayout.Label(branchPathContent, EditorStyles.miniLabel, GUILayout.Width(70));
            string newBranchPath = GUILayout.TextField(BranchPath, EditorStyles.toolbarTextField, GUILayout.MinWidth(150));
            if (newBranchPath != BranchPath)
            {
                BranchPath = newBranchPath;
            }
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                Refresh();
            }
            GUILayout.Space(10);
            GUI.enabled = branchColumnList.GetSelection().Count() == 1;
            if (GUILayout.Button(Terminology.switchbranch, EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                var selection = branchColumnList.GetSelection().First();
                Debug.Log($"switch {selection}");
                VCCommands.Instance.SwitchBranch(selection);
            }
            if (GUILayout.Button(Terminology.merge, EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                var selection = branchColumnList.GetSelection().First();
                Debug.Log($"merge {selection}");
                VCCommands.Instance.MergeBranch(selection);
            }
            GUI.enabled = true;
            if (GUILayout.Button(Terminology.createbranch, EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                
            }
        }

        void Refresh()
        {
            if (!branchpath.EndsWith("/")) branchpath += "/";
            var branches = VCCommands.Instance.RemoteList(BranchPath).Select(p => branchpath + p).ToList();
            branches.Add("^/trunk/");
            branchColumnList.SetBranches(branches);
            currentBranch = VCCommands.Instance.GetCurrentBranch();
        }

        void BranchListGUI()
        {
            branchColumnList.DrawGUI();
        }

    }

    internal class BranchMulticolumnList
    {
        private HashSet<string> masterSelection = new HashSet<string>();
        private MultiColumnState         multiColumnState;
        private MultiColumnViewOption    options;
        private MultiColumnState.Column  columnPath;
        private Action repaint;

        public BranchMulticolumnList(Action repaint = null)
        {
            this.repaint = repaint;;
            Initialize();
        }

        private void Initialize()
        {
            columnPath = new MultiColumnState.Column(new GUIContent("Path"), data => new GUIContent(data));
            multiColumnState = new MultiColumnState();
            
            Func<MultiColumnState.Row, MultiColumnState.Column, bool> cellClickAction = (row, column) =>
            {
                GUI.FocusControl("");
                DebugLog.Log(row.data + " : "  + column.GetContent(row.data).text);
                return false;
            };
            
            var guiSkin = EditorGUIUtility.GetBuiltinSkin( EditorGUIUtility.isProSkin ? EditorSkin.Scene : EditorSkin.Inspector);

            Func<GenericMenu> rowRightClickMenu = () =>
            {
                GenericMenu menu = new GenericMenu();
                return menu;
            };
            
            Func<MultiColumnState.Column, GenericMenu> headerRightClickMenu = column =>
            {
                GenericMenu menu = new GenericMenu();
                return menu;
            };
            
            
            options = new MultiColumnViewOption
            {
                headerStyle = new GUIStyle(guiSkin.button),
                rowStyle = new GUIStyle(guiSkin.label),
                rowRightClickMenu = rowRightClickMenu,
                headerRightClickMenu = headerRightClickMenu,
                cellClickAction = cellClickAction,
                widths = new float[] { 200 },
                doubleClickAction = path =>
                {
                    DebugLog.Log(path);
                }
            };

            options.headerStyle.fixedHeight = 20.0f;
            options.rowStyle.onNormal.background = IconUtils.CreateSquareTexture(4, 1, new Color(0.24f, 0.5f, 0.87f, 0.75f));
            options.rowStyle.margin = new RectOffset(2, 2, 2, 1);
            options.rowStyle.border = new RectOffset(0, 0, 0, 0);
            options.rowStyle.padding = new RectOffset(0, 0, 0, 0);
            
            multiColumnState.AddColumn(columnPath);
            options.widthTable.Add(columnPath.GetHeader().text, 500);
            
        }

        public void SetBranches(IEnumerable<string> branches)
        {
            multiColumnState.Refresh(branches);
        }
        
        public IEnumerable<string> GetSelection()
        {
            return multiColumnState.GetSelected();
        }

        public void DrawGUI()
        {
            Rect rect = GUILayoutUtility.GetRect(5, float.MaxValue, 5, float.MaxValue, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUI.Box(rect, "");
            MultiColumnView.ListView(rect, multiColumnState, options);
        }
    }
}