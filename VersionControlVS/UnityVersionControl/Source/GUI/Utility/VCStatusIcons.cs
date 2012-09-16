// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace VersionControl.UserInterface
{
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

        private static void RequestStatus(Object asset, VCSettings.EReflectionLevel reflectionLevel)
        {
            if (VCSettings.VCEnabled)
            {
                VersionControlStatus assetStatus = VCCommands.Instance.GetAssetStatus(asset.GetAssetPath());
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
            RequestStatus(obj, VCSettings.ProjectReflectionMode);
            DrawVersionControlStatusIcon(obj, selectionRect);
        }

        private static void HierarchyWindowListElementOnGUI(int instanceID, Rect selectionRect)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !VCSettings.HierarchyIcons) return;
            var obj = EditorUtility.InstanceIDToObject(instanceID);
            RequestStatus(obj, VCSettings.HierarchyReflectionMode);
            DrawVersionControlStatusIcon(obj, selectionRect);
        }

        private static void DrawVersionControlStatusIcon(Object obj, Rect rect)
        {
            if (VCSettings.VCEnabled)
            {
                VersionControlStatus assetStatus = VCCommands.Instance.GetAssetStatus(obj.GetAssetPath());
                bool isPrefab = PrefabHelper.IsPrefab(obj);
                bool isPrefabRoot = PrefabHelper.IsPrefabRoot(obj);
                bool halfsize = isPrefab && !isPrefabRoot;
                Rect iconRect = GetRightAligned(rect, IconUtils.iconSize * (halfsize ? 0.5f : 1.0f));
                DrawIcon(iconRect, obj);
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
            float border = (rect.height - size);
            rect.x = rect.x + rect.width - (border / 2.0f);
            rect.x -= size;
            rect.width = size;
            rect.y = rect.y + border / 2.0f;
            rect.height = size;
            return rect;
        }

        private static void DrawIcon(Rect rect, Object obj)
        {
            var assetStatus = obj.GetAssetStatus();
            string statusText = AssetStatusUtils.GetStatusText(assetStatus);
            float size = IconUtils.iconSize;
            IconUtils.Icon iconType = IconUtils.rubyIcon;
            if(PrefabHelper.IsPrefab(obj, true, false, false))
            {
                iconType = IconUtils.squareIcon;
                if (!PrefabHelper.IsPrefabRoot(obj)) size *= 0.5f;
            }
            Texture2D texture = iconType.GetTexture(AssetStatusUtils.GetStatusColor(assetStatus, true));
            Rect placement = GetRightAligned(rect, size);
            if (texture) GUI.DrawTexture(placement, texture);
            var clickRect = placement;
            clickRect.xMax += 5; clickRect.xMin -= 15;
            clickRect.yMax += 5; clickRect.yMin -= 5;
            if (GUI.Button(clickRect, new GUIContent("", statusText), GUIStyle.none))
            {
                VCGUIControls.DiaplayVCContextMenu(obj);
            }
            
        }
    }
}