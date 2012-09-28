// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEngine;
using UnityEditor;

namespace VersionControl.UserInterface
{
    using Extensions;
    [InitializeOnLoad]
    internal static class VCStatusIcons
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

        private static void RequestStatus(string assetPath, VCSettings.EReflectionLevel reflectionLevel)
        {
            if (VCSettings.VCEnabled)
            {
                VersionControlStatus assetStatus = VCCommands.Instance.GetAssetStatus(assetPath);
                if (reflectionLevel == VCSettings.EReflectionLevel.Remote && assetStatus.reflectionLevel != VCReflectionLevel.Pending && assetStatus.reflectionLevel != VCReflectionLevel.Repository)
                {
                    VCCommands.Instance.RequestStatus(assetStatus.assetPath, StatusLevel.Remote);
                }
                else if (reflectionLevel == VCSettings.EReflectionLevel.Local && assetStatus.reflectionLevel == VCReflectionLevel.None)
                {
                    VCCommands.Instance.RequestStatus(assetStatus.assetPath, StatusLevel.Previous);
                }
            }
        }

        private static void ProjectWindowListElementOnGUI(string guid, Rect selectionRect)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !VCSettings.ProjectIcons) return;
            var obj = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            RequestStatus(AssetDatabase.GUIDToAssetPath(guid), VCSettings.ProjectReflectionMode);
            DrawIcon(selectionRect, obj, IconUtils.circleIcon);
        }

        private static void HierarchyWindowListElementOnGUI(int instanceID, Rect selectionRect)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !VCSettings.HierarchyIcons) return;
            var obj = EditorUtility.InstanceIDToObject(instanceID);

            bool changesStoredInPrefab = ObjectUtilities.ChangesStoredInPrefab(obj);
            bool guiLockForPrefabs = EditableManager.LockPrefab(obj.GetAssetPath());

            if (obj.GetAssetPath() != EditorApplication.currentScene && (!changesStoredInPrefab || (changesStoredInPrefab && guiLockForPrefabs) ) )
            {
                RequestStatus(obj.GetAssetPath(), VCSettings.HierarchyReflectionMode);
                DrawIcon(selectionRect, obj, GetHierarchyIcon(obj));
            }
        }

        private static IconUtils.Icon GetHierarchyIcon(Object obj)
        {
            IconUtils.Icon iconType = IconUtils.rubyIcon;
            if (ObjectUtilities.ChangesStoredInPrefab(obj))
            {
                iconType = IconUtils.squareIcon;
            }
            if (IsChildNode(obj))
            {
                iconType = IconUtils.childIcon;
            }
            return iconType;
        }

        private static void RefreshGUI()
        {
            //D.Log("GUI Refresh");
            EditorApplication.RepaintProjectWindow();
            EditorApplication.RepaintHierarchyWindow();
        }


        private static Rect GetRightAligned(Rect rect, float size)
        {
            float border = (rect.height - size);
            rect.x = rect.x + rect.width - (border / 2.0f);
            rect.x -= size;
            rect.width = size;
            rect.y = rect.y + border / 2.0f;
            rect.height = size;
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

        private static void DrawIcon(Rect rect, Object obj, IconUtils.Icon iconType)
        {
            if (VCSettings.VCEnabled)
            {
                var assetStatus = obj.GetAssetStatus();
                string statusText = AssetStatusUtils.GetStatusText(assetStatus);
                Texture2D texture = iconType.GetTexture(AssetStatusUtils.GetStatusColor(assetStatus, true));
                Rect placement = GetRightAligned(rect, iconType.Size);
                if (texture) GUI.DrawTexture(placement, texture);
                var clickRect = placement;
                clickRect.xMax += 5;
                clickRect.xMin -= 15;
                clickRect.yMax += 5;
                clickRect.yMin -= 5;
                if (GUI.Button(clickRect, new GUIContent("", statusText), GUIStyle.none))
                {
                    VCGUIControls.DiaplayVCContextMenu(obj, 10.0f, -40.0f, true);
                }
            }
        }
    }
}