// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using System.IO;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using Object = UnityEngine.Object;
#pragma warning disable CS4014

namespace UVC.UserInterface
{
    using Extensions;
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    
    [System.Flags]
    public enum ValidActions
    {
        Add              = 1 << 0, 
        Open             = 1 << 1, 
        Diff             = 1 << 2, 
        Commit           = 1 << 3, 
        Revert           = 1 << 4, 
        Delete           = 1 << 5, 
        OpenLocal        = 1 << 6, 
        Unlock           = 1 << 7, 
        Update           = 1 << 8,
        UseTheirs        = 1 << 9, 
        UseMine          = 1 << 10, 
        Merge            = 1 << 11, 
        AddChangeList    = 1 << 12, 
        RemoveChangeList = 1 << 13
    }
    
    public static class VCGUIControls
    {
        private static GUIStyle GetPrefabToolbarStyle(GUIStyle style, bool vcRelated)
        {
            var vcStyle = new GUIStyle(style);
            if (vcRelated)
            {
                vcStyle.fontStyle = FontStyle.Bold;
            }
            return vcStyle;
        }

        public static void VersionControlStatusGUI(GUIStyle style, VersionControlStatus assetStatus, Object obj, bool showAddCommit, bool showLockAndAllowLocalEdit, bool showRevert, bool confirmRevert = false)
        {
            using (new PushState<bool>(GUI.enabled, VCCommands.Instance.Ready, v => GUI.enabled = v))
            {
                if (assetStatus.lockStatus == VCLockStatus.LockedHere || assetStatus.ModifiedOrLocalEditAllowed() || !VCUtility.ManagedByRepository(assetStatus))
                {
                    if (!assetStatus.ModifiedOrLocalEditAllowed() && obj.GetAssetPath() != "" && showAddCommit)
                    {
                        if (GUILayout.Button((VCUtility.ManagedByRepository(assetStatus) ? Terminology.commit : Terminology.add), GetPrefabToolbarStyle(style, true)))
                        {
                            VCUtility.ApplyAndCommit(obj, Terminology.commit + " from Inspector");
                        }
                    }
                }

                if (!VCUtility.HaveVCLock(assetStatus) && VCUtility.ManagedByRepository(assetStatus) && showLockAndAllowLocalEdit)
                {
                    if (assetStatus.fileStatus == VCFileStatus.Added)
                    {
                        if (GUILayout.Button(Terminology.commit, GetPrefabToolbarStyle(style, true)))
                        {
                            VCUtility.ApplyAndCommit(obj, Terminology.commit + " from Inspector");
                        }
                    }
                    else if (assetStatus.lockStatus != VCLockStatus.LockedOther)
                    {
                        if (GUILayout.Button(Terminology.getlock, GetPrefabToolbarStyle(style, true)))
                        {
                            VCCommands.Instance.GetLockTask(obj.ToAssetPaths());
                        }
                    }
                    if (!assetStatus.LocalEditAllowed())
                    {
                        if (GUILayout.Button(Terminology.allowLocalEdit, GetPrefabToolbarStyle(style, true)))
                        {
                            VCCommands.Instance.AllowLocalEdit(obj.ToAssetPaths());
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
            return new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(1, 1, 1, 1),
                normal = { background = IconUtils.boxIcon.GetTexture(AssetStatusUtils.GetStatusColor(assetStatus, true)) }
            };
        }

        public static GUIStyle GetLockStatusStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.black }, alignment = TextAnchor.MiddleCenter };
        }

        public static void CreateVCContextMenu(ref GenericMenu menu, IEnumerable<string> assetPaths)
        {
            menu.AddItem(new GUIContent(Terminology.add), false, () => VCCommands.Instance.Add(assetPaths));
            menu.AddItem(new GUIContent(Terminology.getlock), false, () => VCCommands.Instance.GetLock(assetPaths));
            menu.AddItem(new GUIContent(Terminology.commit), false, () => VCCommands.Instance.CommitDialog(assetPaths));
            menu.AddItem(new GUIContent(Terminology.revert), false, () => VCCommands.Instance.Revert(assetPaths));
            menu.AddItem(new GUIContent(Terminology.delete), false, () => VCCommands.Instance.Delete(assetPaths));
            menu.AddItem(new GUIContent("Add to " +Terminology.changelist), false, () => ChangeListWindow.Open(assetPaths));
            menu.AddItem(new GUIContent("Remove from " + Terminology.changelist), false, () => VCCommands.Instance.ChangeListRemove(assetPaths));
        }

        private static ProfilerMarker sceneviewUpdateMarker = new ProfilerMarker("UVC.GetValidActions");
        static readonly ValidActions noAction = new ValidActions();
        public static ValidActions GetValidActions(string assetPath, Object instance = null)
        {
            using (sceneviewUpdateMarker.Auto())
            {
                if (!VCCommands.Active || string.IsNullOrEmpty(assetPath))
                    return noAction;

                var assetStatus = VCCommands.Instance.GetAssetStatus(assetPath);
                
                bool isPrefab = instance != null && PrefabHelper.IsPrefab(instance);
                bool isPrefabParent = isPrefab && PrefabHelper.IsPrefabParent(instance);
                bool isFolder = AssetDatabase.IsValidFolder(assetPath);
                bool diffableAsset = MergeHandler.IsDiffableAsset(assetPath);
                bool mergableAsset = MergeHandler.IsMergableAsset(assetPath);
                bool modifiedDiffableAsset = diffableAsset && assetStatus.fileStatus != VCFileStatus.Normal;
                bool modifiedMeta = assetStatus.MetaStatus().fileStatus != VCFileStatus.Normal;
                bool lockedMeta = assetStatus.MetaStatus().lockStatus == VCLockStatus.LockedHere;
                bool modified = assetStatus.fileStatus == VCFileStatus.Modified;
                bool localOnly = assetStatus.localOnly;
                bool deleted = assetStatus.fileStatus == VCFileStatus.Deleted;
                bool added = assetStatus.fileStatus == VCFileStatus.Added;
                bool unversioned = assetStatus.fileStatus == VCFileStatus.Unversioned;
                bool ignored = assetStatus.fileStatus == VCFileStatus.Ignored;
                bool replaced = assetStatus.fileStatus == VCFileStatus.Replaced;
                bool lockedByOther = assetStatus.lockStatus == VCLockStatus.LockedOther;
                bool managedByRep = VCUtility.ManagedByRepository(assetStatus);
                bool haveControl = VCUtility.HaveAssetControl(assetStatus);
                bool haveLock = VCUtility.HaveVCLock(assetStatus);
                bool allowLocalEdit = assetStatus.LocalEditAllowed();
                bool pending = assetStatus.reflectionLevel == VCReflectionLevel.Pending;
                bool mergeinfo = assetStatus.property == VCProperty.Modified;
                bool conflicted = assetStatus.fileStatus == VCFileStatus.Conflicted;
                bool hasChangeSet = !ComposedString.IsNullOrEmpty(assetStatus.changelist);

                bool showAdd = !pending && !ignored && unversioned;
                bool showOpen = !pending && !showAdd && !added && !haveLock && !deleted && !isFolder && !mergableAsset && ((!lockedByOther && !localOnly) || allowLocalEdit);
                bool showDiff = !pending && !ignored && !deleted && modifiedDiffableAsset && managedByRep;
                bool showCommit = !pending && !ignored && !allowLocalEdit && !localOnly && (haveLock || added || deleted || modifiedDiffableAsset || modifiedMeta || mergeinfo);
                bool showRevert = !pending && !ignored && !unversioned &&
                                 (haveControl || modified || added || deleted || replaced || modifiedDiffableAsset || modifiedMeta || lockedMeta || mergeinfo);
                bool showDelete = !pending && !ignored && !deleted && !lockedByOther;
                bool showOpenLocal = !pending && !ignored && !deleted && !isFolder && !allowLocalEdit && !unversioned && !added && !haveLock && !mergableAsset && !localOnly;
                bool showUnlock = !pending && !ignored && !allowLocalEdit && haveLock;
                bool showUpdate = !pending && !ignored && !added && managedByRep && instance != null;
                bool showUseTheirs = !pending && !ignored && conflicted;
                bool showUseMine = !pending && !ignored && conflicted;
                bool showMerge = !pending && !ignored && conflicted && mergableAsset;
                bool showAddChangeList = !pending && !ignored && !unversioned;
                bool showRemoveChangeList = !pending && !ignored && hasChangeSet;

                ValidActions validActions = 0;
                if (showAdd) validActions |= ValidActions.Add;
                if (showOpen) validActions |= ValidActions.Open;
                if (showDiff) validActions |= ValidActions.Diff;
                if (showCommit) validActions |= ValidActions.Commit;
                if (showRevert) validActions |= ValidActions.Revert;
                if (showDelete) validActions |= ValidActions.Delete;
                if (showOpenLocal) validActions |= ValidActions.OpenLocal;
                if (showUnlock) validActions |= ValidActions.Unlock;
                if (showUpdate) validActions |= ValidActions.Update;
                if (showUseTheirs) validActions |= ValidActions.UseTheirs;
                if (showUseMine) validActions |= ValidActions.UseMine;
                if (showMerge) validActions |= ValidActions.Merge;
                if (showAddChangeList) validActions |= ValidActions.AddChangeList;
                if (showRemoveChangeList) validActions |= ValidActions.RemoveChangeList;
                
                return validActions;
            }
        }
        
        public static ValidActions CombineValidActions(ValidActions a, ValidActions b)
        {
            if (a == (ValidActions)0) return b;
            if (b == (ValidActions)0) return a;
            return
                ValidActions.Add              & (a | b) |
                ValidActions.Open             & (a | b) |
                ValidActions.Diff             & (a & b) |
                ValidActions.Commit           & (a | b) |
                ValidActions.Revert           & (a | b) |
                ValidActions.Delete           & (a | b) |
                ValidActions.OpenLocal        & (a | b) |
                ValidActions.Unlock           & (a | b) |
                ValidActions.Update           & (a | b) |
                ValidActions.UseTheirs        & (a & b) |
                ValidActions.UseMine          & (a & b) |
                ValidActions.Merge            & (a & b) |
                ValidActions.AddChangeList    & (a & b) |
                ValidActions.RemoveChangeList & (a & b) ;
        }

        public static ValidActions GetValidActions(ValidActions[] validAction)
        {
            return validAction.Aggregate(CombineValidActions);
        }

        public static void CreateVCContextMenu(ref GenericMenu menu, string assetPath, Object instance = null)
        {
            if (VCUtility.ValidAssetPath(assetPath))
            {
                bool ready = VCCommands.Instance.Ready;
                if (ready)
                {
                    if (instance && ObjectUtilities.ChangesStoredInScene(instance)) assetPath = SceneManagerUtilities.GetCurrentScenePath();
                    var validActions = GetValidActions(assetPath, instance);

                    if ((validActions & ValidActions.Diff) != 0)       menu.AddItem(new GUIContent(Terminology.diff),              false, () => MergeHandler.DiffWithBase(assetPath));
                    if ((validActions & ValidActions.Add) != 0)        menu.AddItem(new GUIContent(Terminology.add),               false, () => VCCommands.Instance.Add(new[] { assetPath }));
                    if ((validActions & ValidActions.Open) != 0)       menu.AddItem(new GUIContent(Terminology.getlock),           false, () => GetLock(assetPath, instance));
                    if ((validActions & ValidActions.OpenLocal) != 0)  menu.AddItem(new GUIContent(Terminology.allowLocalEdit),    false, () => AllowLocalEdit(assetPath, instance));
                    if ((validActions & ValidActions.Commit) != 0)     menu.AddItem(new GUIContent(Terminology.commit),            false, () => Commit(assetPath, instance));
                    if ((validActions & ValidActions.Unlock) != 0)     menu.AddItem(new GUIContent(Terminology.unlock),            false, () => VCCommands.Instance.ReleaseLock(new[] { assetPath }));
                    if ((validActions & ValidActions.Delete) != 0)     menu.AddItem(new GUIContent(Terminology.delete),            false, () => VCCommands.Instance.Delete(new[] { assetPath }));
                    if ((validActions & ValidActions.Revert) != 0)     menu.AddItem(new GUIContent(Terminology.revert),            false, () => Revert(assetPath, instance));
                    if ((validActions & ValidActions.UseTheirs) != 0)  menu.AddItem(new GUIContent("Use Theirs"),                  false, () => VCCommands.Instance.Resolve(new []{assetPath}, ConflictResolution.Theirs));
                    if ((validActions & ValidActions.UseMine) != 0)    menu.AddItem(new GUIContent("Use Mine"),                    false, () => VCCommands.Instance.Resolve(new []{assetPath}, ConflictResolution.Mine));
                    if ((validActions & ValidActions.Merge) != 0)      menu.AddItem(new GUIContent("Merge"),                       false, () => MergeHandler.ResolveConflict(assetPath));
                    if ((validActions & ValidActions.AddChangeList) != 0) menu.AddItem(new GUIContent("Add To " + Terminology.changelist),false, () => ChangeListWindow.Open(new []{assetPath}));
                    if ((validActions & ValidActions.RemoveChangeList) != 0) menu.AddItem(new GUIContent("Remove From " + Terminology.changelist),false, () => VCCommands.Instance.ChangeListRemove(new []{assetPath}));

                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("..Busy.."));
                }
            }
        }

        private static void GetLock(string assetPath, Object instance, OperationMode operationMode = OperationMode.Normal)
        {
            if (instance != null) VCUtility.GetLock(instance, operationMode);
            else VCCommands.Instance.GetLock(new[] { assetPath }, operationMode);
        }

        private static void AllowLocalEdit(string assetPath, Object instance)
        {
            if (instance != null) VCUtility.AllowLocalEdit(instance);
            else VCCommands.Instance.AllowLocalEdit(new[] { assetPath });
        }

        private static void Commit(string assetPath, Object instance)
        {
            if (instance != null) VCUtility.ApplyAndCommit(instance);
            else VCCommands.Instance.CommitDialog(new[] { assetPath });
        }

        private static void Revert(string assetPath, Object instance)
        {
            if (instance != null) VCUtility.Revert(instance);
            else VCCommands.Instance.Revert(new[] { assetPath });
        }

        public static void DisplayVCContextMenu(string assetPath, Object instance = null, float xoffset = 0.0f, float yoffset = 0.0f, bool showAssetName = false)
        {
            var menu = new GenericMenu();
            if (showAssetName)
            {
                menu.AddDisabledItem(new GUIContent(Path.GetFileName(assetPath)));
                menu.AddSeparator("");
            }
            CreateVCContextMenu(ref menu, assetPath, instance);
            menu.DropDown(new Rect(Event.current.mousePosition.x + xoffset, Event.current.mousePosition.y + yoffset, 0.0f, 0.0f));
            Event.current.Use();
        }
    }
}

