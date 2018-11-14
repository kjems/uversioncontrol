using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using UVC.Logging;

namespace UVC.UserInterface
{
    using MultiColumnState = MultiColumnState<BranchStatus, GUIContent>;
    using MultiColumnViewOption = MultiColumnView.MultiColumnViewOption<BranchStatus>;
    
    internal class BranchWindow : EditorWindow
    {
        BranchMulticolumnList branchColumnList;

        private string currentBranch = "";
        private string trunkpath = null;
        private string branchpath = null;
        public string BranchPath
        {
            get { return branchpath ?? (branchpath = EditorPrefs.GetString("BranchWindow/BranchPath")); }
            set
            {
                branchpath = value;
                EditorPrefs.SetString("BranchWindow/BranchPath", branchpath);
            }
        }
        
        public static void Init()
        {
            GetWindow<BranchWindow>("Branches");
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(branchpath)) branchpath = VCCommands.Instance.GetBranchDefaultPath();
            if (string.IsNullOrEmpty(trunkpath)) trunkpath = VCCommands.Instance.GetTrunkPath();
            branchColumnList = new BranchMulticolumnList();
            VCCommands.Instance.OperationCompleted += InstanceOnOperationCompleted;
            Refresh();
        }

        private void OnDisable()
        {
            VCCommands.Instance.OperationCompleted -= InstanceOnOperationCompleted;
        }
        
        private void InstanceOnOperationCompleted(OperationType operation, VersionControlStatus[] beforeStatus, VersionControlStatus[] afterStatus, bool success)
        {
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
            GUILayout.Label("Branch Path", EditorStyles.miniLabel, GUILayout.Width(70));
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
            GUI.enabled = branchColumnList.GetSelection()?.Count() == 1;
            if (GUILayout.Button(Terminology.switchbranch, EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                var selection = branchColumnList.GetSelection().First();
                VCCommands.Instance.SwitchBranch(BranchPath + selection.name);
                Refresh();
            }
            if (GUILayout.Button(Terminology.merge, EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                if (!GetChangedAssets().Any() || 
                    EditorUtility.DisplayDialog("Local Copy Modified", 
                                                "Before doing a merge your local copy needs to be without any modification. Please revert or commit all changes before doing a merge", 
                                                "Merge Anyway", 
                                                "Cancel"))
                {
                    var selection = branchColumnList.GetSelection().First();
                    if (VCCommands.Instance.MergeBranch(BranchPath + selection.name))
                    {
                        VCCommands.Instance.Status(StatusLevel.Local, DetailLevel.Normal);
                        VCCommands.Instance.CommitDialog(GetChangedAssets().Select(status => status.assetPath.Compose()), true, $"Merged {selection} to {currentBranch}");
                    }
                    Refresh();
                }
            }
            GUI.enabled = true;
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                var newBranchWindow = CreateInstance<NewBranchWindow>();
                newBranchWindow.minSize = new Vector2(440, 50);
                newBranchWindow.titleContent = new GUIContent("Create Branch");
                newBranchWindow.fromPath = currentBranch;
                newBranchWindow.toPath = BranchPath + DateTime.Now.ToString("yyyy-MM-dd_");
                newBranchWindow.ShowUtility();
            }
        }

        static IEnumerable<VersionControlStatus> GetChangedAssets()
        {
            return VCCommands.Instance.GetFilteredAssets(status => 
                    status.fileStatus == VCFileStatus.Modified ||
                    status.fileStatus == VCFileStatus.Added ||
                    status.fileStatus == VCFileStatus.Conflicted ||
                    status.fileStatus == VCFileStatus.Deleted ||
                    status.fileStatus == VCFileStatus.Merged ||
                    status.fileStatus == VCFileStatus.Replaced ||
                    status.property == VCProperty.Modified ||
                    status.property == VCProperty.Conflicted);
        }

        void Refresh()
        {
            if (!branchpath.EndsWith("/")) branchpath += "/";

            VCCommands.Instance.RemoteListTask(BranchPath).ContinueWithOnNextUpdate(relativeBranches =>
            {
                branchColumnList.SetBranches(relativeBranches);
                Repaint();
            });

            VCCommands.Instance.GetCurrentBranchTask().ContinueWithOnNextUpdate(b =>
            {
                currentBranch = b; Repaint();
            });
        }

        void BranchListGUI()
        {
            branchColumnList.DrawGUI();
        }

    }

    internal class BranchMulticolumnList
    {
        private MultiColumnState         multiColumnState;
        private MultiColumnViewOption    options;
        private MultiColumnState.Column  columnPath;
        private MultiColumnState.Column  columnAuthor;
        private MultiColumnState.Column  columnRevision;
        private MultiColumnState.Column  columnDate;

        public BranchMulticolumnList()
        {
            Initialize();
        }

        private void Initialize()
        {
            columnPath = new MultiColumnState.Column(new GUIContent("Path"), data => new GUIContent(data.name));
            columnAuthor = new MultiColumnState.Column(new GUIContent("Author"), data => new GUIContent(data.author));
            columnRevision = new MultiColumnState.Column(new GUIContent("Revision"), data => new GUIContent(data.revision.ToString()));
            columnDate = new MultiColumnState.Column(new GUIContent("Date"), data => new GUIContent(data.date.ToString("yyyy-MM-dd HH:mm:ss")));
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
                    DebugLog.Log(path.name);
                }
            };

            options.headerStyle.fixedHeight = 20.0f;
            options.rowStyle.onNormal.background = IconUtils.CreateSquareTexture(4, 1, new Color(0.24f, 0.5f, 0.87f, 0.75f));
            options.rowStyle.margin = new RectOffset(2, 2, 2, 1);
            options.rowStyle.border = new RectOffset(0, 0, 0, 0);
            options.rowStyle.padding = new RectOffset(0, 0, 0, 0);
            
            multiColumnState.AddColumn(columnPath);
            options.widthTable.Add(columnPath.GetHeader().text, 300);
            
            multiColumnState.AddColumn(columnAuthor);
            options.widthTable.Add(columnAuthor.GetHeader().text, 80);
            
            multiColumnState.AddColumn(columnRevision);
            options.widthTable.Add(columnRevision.GetHeader().text, 80);
            
            multiColumnState.AddColumn(columnDate);
            options.widthTable.Add(columnDate.GetHeader().text, 300);
            
        }

        public void SetBranches(IEnumerable<BranchStatus> branches)
        {
            multiColumnState.Refresh(branches);
        }
        
        public IEnumerable<BranchStatus> GetSelection()
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