using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UVC.Logging;

namespace UVC.UserInterface
{
    using MultiColumnState = MultiColumnState<BranchStatus, GUIContent>;
    using MultiColumnViewOption = MultiColumnView.MultiColumnViewOption<BranchStatus>;

    internal class BranchWindow : EditorWindow
    {
        public static BranchWindow instance;
        static BranchMulticolumnList branchColumnList;
        private static SearchField searchField;

        private static string currentBranch = "";
        private static string trunkpath = null;
        private static string branchpath = null;
        private static string searchString;
        private static List<BranchStatus> branches = new List<BranchStatus>();
        public static string BranchPath
        {
            get { return branchpath ?? (branchpath = EditorPrefs.GetString("BranchWindow/BranchPath")); }
            set
            {
                branchpath = value;
                EditorPrefs.SetString("BranchWindow/BranchPath", branchpath);
            }
        }

        public static void Create()
        {
            if (instance == null)
            {
                instance = CreateInstance<BranchWindow>();
                instance.minSize = new Vector2(220, 140);
                instance.titleContent = new GUIContent(Terminology.branch);
                instance.ShowUtility();
            }
        }

        private void OnEnable()
        {
            instance = this;
            searchField = new SearchField();
            if (string.IsNullOrEmpty(branchpath)) branchpath = VCCommands.Instance.GetBranchDefaultPath();
            if (string.IsNullOrEmpty(trunkpath)) trunkpath = VCCommands.Instance.GetTrunkPath();
            branchColumnList = new BranchMulticolumnList();
            VCCommands.Instance.OperationCompleted += InstanceOnOperationCompleted;
            Refresh();
        }

        private void OnDisable()
        {
            VCCommands.Instance.OperationCompleted -= InstanceOnOperationCompleted;
            instance = null;
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
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                Refresh();
            }
            GUILayout.Space(10);
            GUI.enabled = branchColumnList.GetSelection()?.Count() == 1;
            if (GUILayout.Button(Terminology.switchbranch, EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                var selection = branchColumnList.GetSelection().First();
                Switch(selection.name);
            }
            if (GUILayout.Button(Terminology.merge, EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                var fromBranch = branchColumnList.GetSelection().First().name;
                MergeWithConfirm(fromBranch);
            }
            GUI.enabled = true;
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                var newBranchWindow = CreateInstance<NewBranchWindow>();
                newBranchWindow.minSize = new Vector2(440, 70);
                newBranchWindow.maxSize = new Vector2(440, 70);
                newBranchWindow.titleContent = new GUIContent("Create Branch");
                newBranchWindow.fromPath = currentBranch;
                newBranchWindow.toPath = BranchPath + DateTime.Now.ToString("yyyy-MM-dd_");
                newBranchWindow.ShowUtility();
            }

            string newSearchString = searchField.OnToolbarGUI(searchString);
            if (newSearchString != searchString)
            {
                searchString = newSearchString;
                RefreshBranchList();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Branch Path:", EditorStyles.miniLabel, GUILayout.Width(70));
            string newBranchPath = GUILayout.TextField(BranchPath, EditorStyles.toolbarTextField, GUILayout.ExpandWidth(true), GUILayout.MinWidth(200));
            if (newBranchPath != BranchPath)
            {
                BranchPath = newBranchPath;
            }
        }

        private static void RefreshBranchList()
        {
            branchColumnList.SetBranches(string.IsNullOrEmpty(searchString) ? branches : branches.Where(s => s.name.Contains(searchString) || s.author.Contains(searchString)));
        }

        private static void ConfirmMerge(bool modifiedLocalCopy, string from, string to, Action mergeAction)
        {
            var mergeConfirmation = CreateInstance<MergeConfirmationWindow>();
            mergeConfirmation.minSize = new Vector2(440, 100);
            mergeConfirmation.maxSize = new Vector2(440, 100);
            mergeConfirmation.fromPath = from;
            mergeConfirmation.toPath = to;
            mergeConfirmation.localModified = modifiedLocalCopy;
            mergeConfirmation.mergeAction = mergeAction;
            mergeConfirmation.ShowUtility();
        }

        private static IEnumerable<VersionControlStatus> GetChangedAssets()
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

        private static async void Switch(string url)
        {
            await VCCommands.Instance.SwitchBranchTask(url);
            AssetDatabaseRefreshManager.RequestAssetDatabaseRefresh();
            Refresh();
        }

        private static void MergeWithConfirm(string url)
        {
            ConfirmMerge(GetChangedAssets().Any(), url, currentBranch,
                mergeAction: () =>
                {
                    Merge(url);
                    Refresh();
                });
        }

        private static async void Merge(string url)
        {
            int progressCounter = 0;
            void UpdateMergeProgress(string s)
            {
                EditorUtility.DisplayProgressBar("Merge", s, ((progressCounter++ % 25) / 25f) );
            }

            VCCommands.Instance.ProgressInformation += UpdateMergeProgress;
            bool result = await VCCommands.Instance.MergeBranchTask(url);
            VCCommands.Instance.ProgressInformation -= UpdateMergeProgress;
            EditorUtility.ClearProgressBar();
            if (result)
            {
                await VCCommands.Instance.StatusTask(StatusLevel.Local, DetailLevel.Normal);
                VCCommands.Instance.CommitDialog(GetChangedAssets().Select(status => status.assetPath.Compose()).ToList(), includeDependencies: false, showUserConfirmation: true, commitMessage: $"Merged {url} to {currentBranch}");
                AssetDatabaseRefreshManager.RequestAssetDatabaseRefresh();
            }
        }

        private static async void Refresh()
        {
            try
            {
                if (!branchpath.EndsWith("/")) branchpath += "/";
                if (VCCommands.Active)
                {
                    branches = await VCCommands.Instance.RemoteListTask(BranchPath);
                    var trunkInfo = VCCommands.Instance.GetInfo(trunkpath);
                    var trunk = new BranchStatus()
                    {
                        name = trunkpath,
                        author = trunkInfo.author,
                        date = trunkInfo.lastChangedDate,
                        revision = trunkInfo.revision
                    };
                    branches.Insert(0, trunk);
                    RefreshBranchList();
                    currentBranch = await VCCommands.Instance.GetCurrentBranchTask();
                    await VCCommands.Instance.StatusTask(StatusLevel.Previous, DetailLevel.Normal);
                    if(instance != null)
                        instance.Repaint();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void BranchListGUI()
        {
            branchColumnList.DrawGUI();
        }

        internal class BranchMulticolumnList
        {
            private MultiColumnState         multiColumnState;
            private MultiColumnViewOption    options;
            private MultiColumnState.Column  columnActive;
            private MultiColumnState.Column  columnPath;
            private MultiColumnState.Column  columnAuthor;
            private MultiColumnState.Column  columnRevision;
            private MultiColumnState.Column  columnDate;

            private GUIContent activeBranch = new GUIContent("  ▶", "Active Branch");
            private GUIContent emptyGUIContent = new GUIContent();

            public BranchMulticolumnList()
            {
                Initialize();
            }

            private void Initialize()
            {

                columnActive = new MultiColumnState.Column(new GUIContent("", "Active Branch"), data => currentBranch == data.name ?  activeBranch : emptyGUIContent);
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

                Func<MultiColumnState.Row, MultiColumnState.Column, GenericMenu> rowRightClickMenu = (row, column) =>
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Switch"), false, () => Switch(row.data.name));
                    menu.AddItem(new GUIContent("Merge"), false, () => MergeWithConfirm(row.data.name));
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
                        //DebugLog.Log(path.name);
                    }
                };

                options.headerStyle.fixedHeight = 20.0f;
                options.rowStyle.onNormal.background = IconUtils.CreateSquareTexture(4, 1, new Color(0.24f, 0.5f, 0.87f, 0.75f));
                options.rowStyle.margin = new RectOffset(2, 2, 2, 1);
                options.rowStyle.border = new RectOffset(0, 0, 0, 0);
                options.rowStyle.padding = new RectOffset(0, 0, 0, 0);

                multiColumnState.AddColumn(columnActive);
                options.widthTable.Add(columnActive.GetHeader().text, 25);

                multiColumnState.AddColumn(columnPath);
                options.widthTable.Add(columnPath.GetHeader().text, 350);

                multiColumnState.AddColumn(columnAuthor);
                options.widthTable.Add(columnAuthor.GetHeader().text, 90);

                multiColumnState.AddColumn(columnRevision);
                options.widthTable.Add(columnRevision.GetHeader().text, 80);

                multiColumnState.AddColumn(columnDate);
                options.widthTable.Add(columnDate.GetHeader().text, 150);

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


}
