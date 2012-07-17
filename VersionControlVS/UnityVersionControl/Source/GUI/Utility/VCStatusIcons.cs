// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace VersionControl.UserInterface
{
    internal class StatusIcon
    {
        public StatusIcon(Color color, Color borderColor, int iconSize, int borderSize)
        {
            Initilize(color, borderColor, iconSize, borderSize);
        }

        private void Initilize(Color color, Color borderColor, int iconSize, int borderSize)
        {
            FullIcon = TextureUtils.CreateSquareTextureWithBorder(iconSize, borderSize, color, color);
            HollowIcon = TextureUtils.CreateSquareTextureWithBorder(iconSize, borderSize, new Color(1, 1, 1, 0), color);
            BorderIcon = TextureUtils.CreateSquareTextureWithBorder(iconSize, borderSize, color, borderColor);
        }

        public Texture2D FullIcon { get; private set; }
        public Texture2D HollowIcon { get; private set; }
        public Texture2D BorderIcon { get; private set; }
    }

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

        private const int iconSize = 8;
        private const int borderSize = 1;

        private static readonly Color orange = new Color(1.0f, 0.75f, 0.0f);
        private static readonly Color pastelRed = new Color(0.85f, 0.4f, 0.4f);
        private static readonly Color pastelBlue = new Color(0.3f, 0.55f, 0.85f);
        private static readonly Color lightgrey = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color pink = new Color(1f, 0.1f, 1f);
        private static readonly Color black = Color.black;
        private static readonly Color border = new Color(0.1f, 0.1f, 0.1f);

        private static readonly Color addedColor = Color.blue;
        private static readonly Color conflictedColor = Color.red;
        private static readonly Color missingColor = new Color(1.0f, 0.2f, 1.0f);
        private static readonly Color normalColor = Color.white;
        private static readonly Color lockedColor = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color lockedOtherColor = new Color(0.9f, 0.3f, 0.3f);
        private static readonly Color modifiedColor = orange;
        private static readonly Color unversionedColor = lightgrey;
        private static readonly Color remoteModifiedColor = new Color(0.8f, 0.8f, 1f);
        private static readonly Color pendingColor = new Color(1, 1, 0.6f, 0.8f);
        private static readonly Color ignoreColor = new Color(1, 1, 1, 0.1f);
        private static readonly Color deletedColor = new Color(1, 1, 1, 0.1f);

        private static readonly StatusIcon addedIcon = new StatusIcon(addedColor, border, iconSize, borderSize);
        private static readonly StatusIcon conflictedIcon = new StatusIcon(conflictedColor, conflictedColor, iconSize, borderSize);
        private static readonly StatusIcon missingIcon = new StatusIcon(missingColor, border, iconSize, borderSize);
        private static readonly StatusIcon normalIcon = new StatusIcon(normalColor, border, iconSize, borderSize);
        private static readonly StatusIcon lockedIcon = new StatusIcon(lockedColor, border, iconSize, borderSize);
        private static readonly StatusIcon lockedOtherIcon = new StatusIcon(lockedOtherColor, border, iconSize, borderSize);
        private static readonly StatusIcon modifiedIcon = new StatusIcon(modifiedColor, border, iconSize, borderSize);
        private static readonly StatusIcon unversionedIcon = new StatusIcon(unversionedColor, border, iconSize, borderSize);
        private static readonly StatusIcon pendingIcon = new StatusIcon(pendingColor, border, iconSize, borderSize);
        private static readonly StatusIcon remoteModifiedIcon = new StatusIcon(remoteModifiedColor, border, iconSize, borderSize);
        private static readonly StatusIcon ignoreIcon = new StatusIcon(ignoreColor, border, iconSize, borderSize);
        private static readonly StatusIcon deletedIcon = new StatusIcon(deletedColor, border, iconSize, borderSize);
        private static readonly StatusIcon defaultIcon = new StatusIcon(black, black, iconSize, borderSize);


        public static StatusIcon GetStatusIcon(VersionControlStatus assetStatus, bool includeLockStatus)
        {
            if (assetStatus.treeConflictStatus == VCTreeConflictStatus.TreeConflict) return conflictedIcon;
            if (assetStatus.fileStatus == VCFileStatus.Conflicted) return conflictedIcon;
            if (assetStatus.fileStatus == VCFileStatus.Missing) return missingIcon;
            if (assetStatus.bypassRevisionControl) return modifiedIcon;
            if (assetStatus.fileStatus == VCFileStatus.Added) return addedIcon;
            
            if (includeLockStatus)
            {
                if (assetStatus.lockStatus == VCLockStatus.LockedHere) return lockedIcon;
                if (assetStatus.lockStatus == VCLockStatus.LockedOther) return lockedOtherIcon;
            }

            if (assetStatus.fileStatus == VCFileStatus.Modified) return modifiedIcon;
            if (assetStatus.reflectionLevel == VCReflectionLevel.Pending) return pendingIcon;
            if (assetStatus.fileStatus == VCFileStatus.Deleted) return deletedIcon;
            if (assetStatus.fileStatus == VCFileStatus.Unversioned) return unversionedIcon;
            if (assetStatus.remoteStatus == VCRemoteFileStatus.Modified) return remoteModifiedIcon;
            if (assetStatus.fileStatus == VCFileStatus.Normal) return normalIcon;

            return defaultIcon;
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
                    VCCommands.Instance.RequestStatus(assetStatus.assetPath, StatusLevel.Local);
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
                Rect iconRect = GetRightAligned(rect, iconSize * (halfsize ? 0.5f : 1.0f));
                DrawIcon(iconRect, VersionControlStatusToGUIContent(assetStatus, isPrefab), obj);
            }
        }

        private static void RefreshGUI()
        {
            //D.Log("GUI Refresh");
            EditorApplication.RepaintProjectWindow();
            EditorApplication.RepaintHierarchyWindow();
        }

        public static Color VersionControlStatusToColor(VersionControlStatus assetStatus)
        {
            if (assetStatus.reflectionLevel == VCReflectionLevel.Pending) return pendingColor;
            if (assetStatus.lockStatus == VCLockStatus.LockedHere) return lockedColor;
            if (assetStatus.bypassRevisionControl) return modifiedColor;
            if (assetStatus.fileStatus == VCFileStatus.Modified || assetStatus.bypassRevisionControl) return modifiedColor;
            if (assetStatus.fileStatus == VCFileStatus.Normal) return normalColor;
            if (assetStatus.fileStatus == VCFileStatus.Unversioned) return unversionedColor;
            if (assetStatus.fileStatus == VCFileStatus.Added) return addedColor;
            if (assetStatus.fileStatus == VCFileStatus.Conflicted) return conflictedColor;
            if (assetStatus.fileStatus == VCFileStatus.Replaced) return modifiedColor;
            if (assetStatus.fileStatus == VCFileStatus.Ignored) return ignoreColor;
            return Color.magenta;
        }

        private static Texture2D GetTexture(this StatusIcon icon, bool hollow = false)
        {
            return hollow ? icon.HollowIcon : icon.FullIcon;
        }

        public static GUIContent VersionControlStatusToGUIContent(VersionControlStatus assetStatus, bool isPrefab)
        {
            if (assetStatus.reflectionLevel == VCReflectionLevel.Pending) return new GUIContent(pendingIcon.GetTexture(!isPrefab), "Pending");
            if (assetStatus.lockStatus == VCLockStatus.LockedHere) return new GUIContent(lockedIcon.GetTexture(!isPrefab), Terminology.getlock);
            if (assetStatus.bypassRevisionControl) return new GUIContent(modifiedIcon.GetTexture(!isPrefab), "Bypass Lock");
            if (assetStatus.lockStatus == VCLockStatus.LockedOther) return new GUIContent(lockedOtherIcon.GetTexture(!isPrefab), Terminology.lockedBy + "'" + assetStatus.owner + "'\nShift click to force open");
            if (assetStatus.fileStatus == VCFileStatus.Modified || assetStatus.bypassRevisionControl) return new GUIContent(modifiedIcon.GetTexture(!isPrefab), Terminology.bypass);
            if (assetStatus.fileStatus == VCFileStatus.Unversioned) return new GUIContent(unversionedIcon.GetTexture(!isPrefab), Terminology.unversioned);
            if (assetStatus.fileStatus == VCFileStatus.Added) return new GUIContent(addedIcon.GetTexture(!isPrefab), "Added");
            if (assetStatus.fileStatus == VCFileStatus.Conflicted) return new GUIContent(conflictedIcon.GetTexture(!isPrefab), "Conflicted");
            if (assetStatus.fileStatus == VCFileStatus.Replaced) return new GUIContent(modifiedIcon.GetTexture(!isPrefab), "Replaced");
            if (assetStatus.fileStatus == VCFileStatus.Ignored) return new GUIContent(ignoreIcon.GetTexture(!isPrefab), "Ignored");
            if (assetStatus.remoteStatus == VCRemoteFileStatus.Modified) return new GUIContent(remoteModifiedIcon.GetTexture(!isPrefab), "Modified on server");
            if (assetStatus.fileStatus == VCFileStatus.Normal) return new GUIContent(normalIcon.GetTexture(!isPrefab), "Normal");

            return new GUIContent("-");
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

        private static void DrawIcon(Rect rect, GUIContent content, Object obj)
        {
            if (content.image) GUI.DrawTexture(rect, content.image);
            var clickRect = rect;
            clickRect.xMax += 5; clickRect.xMin -= 30;
            clickRect.yMax += 5; clickRect.yMin -= 5;
            if (GUI.Button(clickRect, new GUIContent("", content.tooltip), GUIStyle.none))
            {
                VCGUIControls.DiaplayVCContextMenu(obj);
            }
        }
    }
}