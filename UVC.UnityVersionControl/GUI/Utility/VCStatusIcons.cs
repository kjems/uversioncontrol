// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;

namespace UVC.UserInterface
{
    using Extensions;
    [InitializeOnLoad]
    public static class VCStatusIcons
    {
        static VCStatusIcons()
        {

            // Add delegates
            EditorApplication.projectWindowItemOnGUI += ProjectWindowListElementOnGUI;
            EditorApplication.hierarchyWindowItemOnGUI += HierarchyWindowListElementOnGUI;
            VCCommands.Instance.StatusCompleted += RefreshGUI;
            VCSettings.SettingChanged += RefreshGUI;

            // Request repaint of project and hierarchy windows
            EditorApplication.RepaintProjectWindow();
            EditorApplication.RepaintHierarchyWindow();

        }

        private static void ProjectWindowListElementOnGUI(string guid, Rect selectionRect)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !VCSettings.ProjectIcons || !VCCommands.Active) return;
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            VCUtility.RequestStatus(assetPath, VCSettings.ProjectReflectionMode);
            DrawIcon(selectionRect, IconUtils.circleIcon, assetPath);
        }

        private static void HierarchyWindowListElementOnGUI(int instanceID, Rect selectionRect)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !VCSettings.HierarchyIcons || !VCCommands.Active) return;
            var obj = EditorUtility.InstanceIDToObject(instanceID);

            if (obj == null)
            {
                string sceneAssetPath = SceneManagerUtilities.GetSceneAssetPathFromHandle(instanceID);
                if (!string.IsNullOrEmpty(sceneAssetPath))
                {
                    VCUtility.RequestStatus(sceneAssetPath, VCSettings.HierarchyReflectionMode);
                    DrawIcon(selectionRect, IconUtils.rubyIcon, sceneAssetPath, null, -20f);
                }
            }
            else
            {
                var currentPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (currentPrefabStage != null)
                {
                    var go = obj as GameObject;
                    if (go)
                    {
                        if (PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot == go)
                        {
                            DrawIcon(selectionRect, IconUtils.squareIcon, PrefabStageUtility.GetCurrentPrefabStage().prefabAssetPath, PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot, 0f);
                        }
                    }
                }
            }
        }

        private static void RefreshGUI()
        {
            //D.Log("GUI Refresh");
            EditorApplication.RepaintProjectWindow();
            EditorApplication.RepaintHierarchyWindow();
        }

        private static Rect GetRightAligned(Rect rect, float size)
        {
            if (rect.height > 20)
            {
                // Unity 4.x large project icons
                rect.y = rect.y - 3;
                rect.x = rect.width + rect.x - 12;
                rect.width = size;
                rect.height = size;
            }
            else
            {
                // Normal icons
                float border = (rect.height - size);
                rect.x = rect.x + rect.width - (border / 2.0f);
                rect.x -= size;
                rect.width = size;
                rect.y = rect.y + border / 2.0f;
                rect.height = size;
            }
            return rect;
        }



        private static bool IsChildNode(Object obj)
        {
            GameObject go = obj as GameObject;
            if (go != null)
            {
                var persistentAssetPath = obj.GetAssetPath();
                var persistentParentAssetPath = go.transform.parent != null ? go.transform.parent.gameObject.GetAssetPath() : "";
                return persistentAssetPath == persistentParentAssetPath;
            }
            return false;
        }

        public static void DrawIcon(Rect rect, IconUtils.Icon iconType, string assetPath, Object instance = null, float xOffset = 0f)
        {
            if (VCSettings.VCEnabled)
            {
                var assetStatus = VCCommands.Instance.GetAssetStatus(assetPath);
                string statusText = AssetStatusUtils.GetStatusText(assetStatus);
                Texture2D texture = iconType.GetTexture(AssetStatusUtils.GetStatusColor(assetStatus, true));
                Rect placement = GetRightAligned(rect, iconType.Size);
                placement.x += xOffset;
                var clickRect = placement;
                //clickRect.xMax += iconType.Size * 0.25f;
                //clickRect.xMin -= rect.width * 0.15f;
                if (texture) GUI.DrawTexture(placement, texture);
                if (GUI.Button(clickRect, new GUIContent("", statusText), GUIStyle.none))
                {
                    VCGUIControls.DisplayVCContextMenu(assetPath, instance, 10.0f, -40.0f, true);
                }
            }
        }
    }
}
