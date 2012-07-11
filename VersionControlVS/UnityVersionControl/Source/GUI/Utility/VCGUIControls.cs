// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace VersionControl.UserInterface
{
    internal static class VCGUIControls
    {
        public static GUIStyle GetPrefabToolbarStyle(GUIStyle style, bool vcRelated)
        {
            var vcStyle = new GUIStyle(style);
            if (vcRelated)
            {
                vcStyle.fontStyle = FontStyle.Bold;
            }
            return vcStyle;
        }

        public static void VersionControlStatusGUI(GUIStyle style, VersionControlStatus assetStatus, Object obj, bool showAddCommit, bool showLockBypass, bool showRevert, bool confirmRevert = false)
        {
            using (new PushState<bool>(GUI.enabled, VCCommands.Instance.Ready, v => GUI.enabled = v))
            {
                if (assetStatus.lockStatus == VCLockStatus.LockedHere || assetStatus.bypassRevisionControl || !VCUtility.ManagedByRepository(assetStatus))
                {
                    if (!assetStatus.bypassRevisionControl && obj.GetAssetPath() != "" && showAddCommit)
                    {
                        if (GUILayout.Button((VCUtility.ManagedByRepository(assetStatus) ? Terminology.commit : Terminology.add), GetPrefabToolbarStyle(style, true)))
                        {
                            VCUtility.ApplyAndCommit(obj, Terminology.commit + " from Inspector");
                        }
                    }
                }

                if (!VCUtility.HaveVCLock(assetStatus) && VCUtility.ManagedByRepository(assetStatus) && showLockBypass)
                {
                    if (assetStatus.lockStatus != VCLockStatus.LockedOther)
                    {
                        if (GUILayout.Button(Terminology.getlock, GetPrefabToolbarStyle(style, true)))
                        {
                            VCCommands.Instance.GetLockTask(obj.ToAssetPaths());
                        }
                    }
                    if (!assetStatus.bypassRevisionControl)
                    {
                        if (GUILayout.Button(Terminology.bypass, GetPrefabToolbarStyle(style, true)))
                        {
                            VCCommands.Instance.BypassRevision(obj.ToAssetPaths());
                        }
                    }
                }

                if (showRevert)
                {
                    if (GUILayout.Button(Terminology.revert, GetPrefabToolbarStyle(style, VCUtility.ShouldVCRevert(obj))))
                    {
                        if ((!confirmRevert || Event.current.shift) || VCUtility.VCDialog(Terminology.revert, obj))
                        {
                            var seletedGo = Selection.activeGameObject;
                            var revertedObj = VCUtility.Revert(obj);
                            OnNextUpdate.Do(() => Selection.activeObject = ((obj is GameObject) ? revertedObj : seletedGo));
                        }
                    }
                }
            }
        }



        public static GUIStyle GetVCBox(VersionControlStatus assetStatus)
        {
            return new GUIStyle(GUI.skin.box) { border = new RectOffset(2, 2, 2, 2), padding = new RectOffset(1, 1, 1, 1), normal = { background = VCStatusIcons.GetStatusIcon(assetStatus, true).BorderIcon } };
        }

        public static GUIStyle GetLockStatusStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel) {normal = {textColor = Color.black}, alignment = TextAnchor.MiddleCenter};
        }

        public static string GetLockStatusMessage(VersionControlStatus assetStatus)
        {
            string lockMessage = assetStatus.lockStatus.ToString();
            if (assetStatus.lockStatus == VCLockStatus.LockedOther) lockMessage = Terminology.getlock + " by: " + assetStatus.owner;
            if (assetStatus.lockStatus == VCLockStatus.LockedHere) lockMessage = Terminology.getlock + " Here: " + assetStatus.owner;
            if (assetStatus.lockStatus == VCLockStatus.NoLock)
            {
                if (string.IsNullOrEmpty(assetStatus.assetPath)) lockMessage = "Not saved";
                else if (assetStatus.fileStatus == VCFileStatus.Added) lockMessage = "Added";
                else lockMessage = VCUtility.ManagedByRepository(assetStatus) ? "Not " + Terminology.getlock : "Not on Version Control";
            }
            if (assetStatus.bypassRevisionControl)
            {
                lockMessage = Terminology.bypass;
                if ((assetStatus.lockStatus == VCLockStatus.LockedOther))
                {
                    lockMessage += " (" + Terminology.getlock + " By: " + assetStatus.owner + " )";
                }
            }
            else if (assetStatus.fileStatus == VCFileStatus.Modified) lockMessage = "Modified";
            return lockMessage;
        }
        
        public static GenericMenu CreateVCContextMenu(IEnumerable<string> assetPaths)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(Terminology.add), false, () => VCCommands.Instance.Add(assetPaths));
            menu.AddItem(new GUIContent(Terminology.getlock), false, () => VCCommands.Instance.GetLock(assetPaths));
            menu.AddItem(new GUIContent(Terminology.commit), false, () => VCCommands.Instance.CommitDialog(assetPaths));
            menu.AddItem(new GUIContent(Terminology.revert), false, () => VCCommands.Instance.Revert(assetPaths));
            menu.AddItem(new GUIContent(Terminology.delete), false, () => VCCommands.Instance.Delete(assetPaths));
            return menu;
        }

        public static GenericMenu CreateVCContextMenu(string assetPath, Object instance = null)
        {
            var menu = new GenericMenu();
            if (!string.IsNullOrEmpty(assetPath))
            {
                var assetStatus = VCCommands.Instance.GetAssetStatus(assetPath);
                if (ObjectExtension.ChangesStoredInScene(AssetDatabase.LoadMainAssetAtPath(assetPath))) assetPath = EditorApplication.currentScene;

                bool ready = VCCommands.Instance.Ready;
                bool isPrefab = instance != null && PrefabHelper.IsPrefab(instance);
                bool isPrefabParent = isPrefab && PrefabHelper.IsPrefabParent(instance);
                bool isFolder = System.IO.Directory.Exists(assetPath);
                bool modifiedTextAsset = VCUtility.IsTextAsset(assetPath) && assetStatus.fileStatus != VCFileStatus.Normal;
                bool modifiedMeta = assetStatus.MetaStatus().fileStatus != VCFileStatus.Normal;
                bool deleted = assetStatus.fileStatus == VCFileStatus.Deleted;
                bool added = assetStatus.fileStatus == VCFileStatus.Added;
                bool unversioned = assetStatus.fileStatus == VCFileStatus.Unversioned;
                bool ignored = assetStatus.fileStatus == VCFileStatus.Ignored;
                bool replaced = assetStatus.fileStatus == VCFileStatus.Replaced;
                bool lockedByOther = assetStatus.lockStatus == VCLockStatus.LockedOther;
                bool managedByRep = VCUtility.ManagedByRepository(assetStatus);
                bool haveControl = VCUtility.HaveAssetControl(assetStatus);
                bool haveLock = VCUtility.HaveVCLock(assetStatus);
                bool bypass = assetStatus.bypassRevisionControl;

                bool showAdd = ready && !ignored && unversioned;
                bool showOpen = ready && !showAdd && !added && !haveLock && !deleted && !isFolder && (!lockedByOther || bypass);
                bool showDiff = ready && !ignored && modifiedTextAsset && managedByRep;
                bool showCommit = ready && !ignored && !bypass && (haveControl || added || deleted || modifiedTextAsset || isFolder || modifiedMeta);
                bool showRevert = ready && !ignored && !unversioned && (haveControl || added || deleted || replaced || modifiedTextAsset || modifiedMeta);
                bool showDelete = ready && !ignored && !deleted && !lockedByOther;
                bool showOpenLocal = ready && !ignored && !deleted && !isFolder && !bypass && !unversioned && !added && !haveLock;
                bool showUnlock = ready && !ignored && !bypass && haveLock;
                bool showUpdate = ready && !ignored && !added && managedByRep && instance != null;
                bool showForceOpen = ready && !ignored && !deleted && !isFolder && !bypass && !unversioned && !added && lockedByOther && Event.current.shift;
                bool showDisconnect = isPrefab && !isPrefabParent;

                if (showAdd) menu.AddItem(new GUIContent(Terminology.add), false, () => VCCommands.Instance.Add(new[] {assetPath}));
                if (showOpen) menu.AddItem(new GUIContent(Terminology.getlock), false, () => VCCommands.Instance.GetLock(new[] {assetPath}));
                if (showOpenLocal) menu.AddItem(new GUIContent(Terminology.bypass), false, () => VCCommands.Instance.BypassRevision(new[] { assetPath }));
                if (showForceOpen) menu.AddItem(new GUIContent("Force " + Terminology.getlock), false, () => VCUtility.VCForceOpen(assetPath, assetStatus));
                if (showCommit) menu.AddItem(new GUIContent(Terminology.commit), false, () => Commit(assetPath, instance));
                if (showDelete) menu.AddItem(new GUIContent(Terminology.delete), false, () => VCCommands.Instance.Delete(new[] {assetPath}, true));
                if (showRevert) menu.AddItem(new GUIContent(Terminology.revert), false, () => Revert(assetPath, instance));
                if (showUnlock) menu.AddItem(new GUIContent(Terminology.unlock), false, () => VCCommands.Instance.ReleaseLock(new[] { assetPath }));
                if (showDisconnect) menu.AddItem(new GUIContent("Disconnect"), false, () => PrefabHelper.DisconnectPrefab(instance as GameObject));
                if (showUpdate) menu.AddItem(new GUIContent(Terminology.update), false, () => VCCommands.Instance.UpdateTask(new[] { assetPath }));
                if (showDiff) menu.AddItem(new GUIContent(Terminology.diff), false, () => VCUtility.DiffWithBase(assetPath));
            }
            return menu;
        }

        private static void Commit(string assetPath, Object instance)
        {
            if (instance != null) VCUtility.ApplyAndCommit(instance, "");
            else VCCommands.Instance.CommitDialog(new[] { assetPath });
        }

        private static void Revert(string assetPath, Object instance)
        {
            if (instance != null) VCUtility.Revert(instance);
            else VCCommands.Instance.Revert(new[] { assetPath });
        }

        public static void DiaplayVCContextMenu(Object instance)
        {
            CreateVCContextMenu(instance.GetAssetPath(), instance).ShowAsContext();
            Event.current.Use();
        }
    }


    internal static class TextureUtils
    {
        public static Texture2D CreateBorderedTexture(Color border, Color body)
        {
            var backgroundTexture = new Texture2D(3, 3, TextureFormat.ARGB32, false) {hideFlags = HideFlags.HideAndDontSave};

            backgroundTexture.SetPixels(new[]
            {
                border, border, border,
                border, body, border,
                border, border, border,
            });
            backgroundTexture.wrapMode = TextureWrapMode.Clamp;
            backgroundTexture.filterMode = FilterMode.Point;
            backgroundTexture.Apply();
            return backgroundTexture;
        }

        public static Texture2D CreateSquareTexture(int size, int borderSize, Color color)
        {
            return CreateSquareTextureWithBorder(size, borderSize, color, color);
        }

        public static Texture2D CreateSquareTextureWithBorder(int size, int borderSize, Color inner, Color border)
        {
            var colors = new Color[size*size];
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    bool onborder = (x < borderSize || x >= size - borderSize || y < borderSize || y >= size - borderSize);
                    colors[x + y * size] = onborder ? border : inner;
                }
            }

            var iconTexture = new Texture2D(size, size, TextureFormat.ARGB32, false) {hideFlags = HideFlags.HideAndDontSave};
            iconTexture.SetPixels(colors);
            iconTexture.wrapMode = TextureWrapMode.Clamp;
            iconTexture.filterMode = FilterMode.Point;
            iconTexture.Apply();
            return iconTexture;
        }
    }

    /*internal sealed class GeneratedTextures
    {
        private static GeneratedTextures instance;
        public static GeneratedTextures Instance
        {
            get { return instance ?? (instance = new GeneratedTextures()); }
        }

        private GeneratedTextures()
        {
            const int size = 7;
            const int border = 1;
            noTexture = TextureUtils.CreateSquareTextureWithBorder(size, border, new Color(0.1f, 0.1f, 0.1f), new Color(0.55f, 0.55f, 0.55f));
            lockedByOtherTexture = TextureUtils.CreateSquareTextureWithBorder(size, border, new Color(0.1f, 0.1f, 0.1f), new Color(0.85f, 0.4f, 0.4f));
            lockedTexture = TextureUtils.CreateSquareTextureWithBorder(size, border, new Color(0.1f, 0.1f, 0.1f), new Color(0.4f, 0.7f, 0.4f));
            noLockTexture = TextureUtils.CreateSquareTextureWithBorder(size, border, new Color(0.1f, 0.1f, 0.1f), Color.white);
            bypassTexture = TextureUtils.CreateSquareTextureWithBorder(size, border, new Color(0.1f, 0.1f, 0.1f), new Color(0.8f, 0.6f, 0.3f));
            addedTexture = TextureUtils.CreateSquareTextureWithBorder(size, border, new Color(0.1f, 0.1f, 0.1f), new Color(0.2f, 0.2f, 0.8f));
            pendingTexture = TextureUtils.CreateSquareTextureWithBorder(size, border, new Color(0.1f, 0.1f, 0.1f), new Color(1, 1, 0.6f, 0.8f));
            conflictTexture = TextureUtils.CreateSquareTextureWithBorder(size, border, new Color(1f, 0f, 0f), new Color(1f, 0f, 0f, 1f));
        }

        public readonly Texture2D lockedTexture;
        public readonly Texture2D noLockTexture;
        public readonly Texture2D bypassTexture;
        public readonly Texture2D noTexture;
        public readonly Texture2D lockedByOtherTexture;
        public readonly Texture2D addedTexture;
        public readonly Texture2D pendingTexture;
        public readonly Texture2D conflictTexture;
    }*/
}

