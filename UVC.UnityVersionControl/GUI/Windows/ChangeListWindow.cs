using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable CS4014

namespace UVC.UserInterface
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    internal class ChangeListWindow : EditorWindow
    {
        public IEnumerable<string> assetPaths;
        private string changeListName;
        
        private string[] changeLists;
        private int changeListIndex = 0;

        public static void Open(IEnumerable<string> assetPaths)
        {
            var changeListWindow = CreateInstance<ChangeListWindow>();
            changeListWindow.minSize = new Vector2(220, 60);
            changeListWindow.maxSize = new Vector2(220, 60);
            changeListWindow.titleContent = new GUIContent("Change List");
            changeListWindow.assetPaths = assetPaths;
            //var rect = new Rect(Event.current.mousePosition, Vector2.one);
            //changeListWindow.ShowAsDropDown(rect, new Vector2(220, 60));
            changeListWindow.ShowUtility();
        }
        
        public static void Init()
        {
            GetWindow<ChangeListWindow>("Change Lists");
        }

        void OnEnable()
        {
            changeLists = VCCommands.Instance
                .GetFilteredAssets(s => !ComposedString.IsNullOrEmpty(s.changelist))
                .Select(s => s.changelist.Compose())
                .ToArray();
            
        }

        private void OnGUI()
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Existing", GUILayout.Width(40));
                int newChangeListIndex = EditorGUILayout.Popup(changeListIndex, changeLists);
                if (newChangeListIndex != changeListIndex)
                {
                    changeListIndex = newChangeListIndex;
                    changeListName = changeLists[changeListIndex];
                }
            }
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("New", GUILayout.Width(40));
                changeListName = GUILayout.TextField(changeListName);
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add"))
                {
                    VCCommands.Instance.ChangeListAdd(assetPaths, changeListName);
                    Close();
                }
            }
        }
    }
}