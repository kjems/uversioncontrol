// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UVC.UserInterface;

namespace UVC
{
    using Logging;
    using AssetPathFilters;
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    internal class VCFileHandler : UnityEditor.AssetModificationProcessor
    {

        private static bool UseTeamLicence 
        { 
            get 
            {
                return VCSettings.HandleFileMove == VCSettings.EHandleFileMove.TeamLicense;
            }
        }
        
        private static bool InUnversionedParentFolder(string asset, out string topUnversionedFolder)
        {
            topUnversionedFolder = "";
            var unversionedFolders = ParentFolders(asset).Where(a => VCCommands.Instance.GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned);
            if (unversionedFolders.Any())
            {
                topUnversionedFolder = unversionedFolders.OrderBy(f => f.Length).First();
                return true;
            }
            return false;
        }
        
        private static IEnumerable<string> ParentFolders(string asset)
        {
            const char pathSeparator = '/';
            var parentFolders = new List<string>();
            if (!string.IsNullOrEmpty(asset))
            {
                string currentFolder = "";
                foreach (var folderIt in Path.GetDirectoryName(asset).Replace("\\","/").Split(pathSeparator))
                {
                    currentFolder += folderIt + pathSeparator;
                    parentFolders.Add(currentFolder.TrimEnd(pathSeparator));
                }
            }
            return parentFolders;
        }

        private static bool DisplayConfirmationDialog(string command, string assetPath, VersionControlStatus assetStatus)
        {
            bool acceptOperation = true;
            if (assetStatus.lockStatus == VCLockStatus.LockedOther)
            {
                acceptOperation = UserDialog.DisplayDialog(command + " on repository?", assetPath + "\nis " + Terminology.getlock + " by [" + assetStatus.owner + "], are you sure you want to " + command + "?", command, "Cancel");
            }
            if (acceptOperation && assetStatus.fileStatus == VCFileStatus.Modified)
            {
                acceptOperation = UserDialog.DisplayDialog(command + " on repository?", assetPath + "\nFile is modified on repository, are you sure you want to " + command + "?", command, "Cancel");
            }
            return acceptOperation;
        }
        
        private static AssetMoveResult OnWillMoveAsset(string from, string to)
        {
            if (!UseTeamLicence) return AssetMoveResult.DidNotMove;

            VersionControlStatus status = VCCommands.Instance.GetAssetStatus(from);
            if (VCUtility.ManagedByRepository(status))
            {
                if (DisplayConfirmationDialog("Move", from, status))
                {
                    string topUnversionedFolder;
                    if (InUnversionedParentFolder(to, out topUnversionedFolder))
                    {
                        int result = UserDialog.DisplayDialogComplex("Add Folder?", "Versioned files are moved into an unversioned folder. Add following unversioned folder first?\n\n" + topUnversionedFolder, "Yes", "No", "Cancel");
                        if (result == 0)
                        {
                            VCCommands.Instance.Add(new[] { topUnversionedFolder });
                            VCCommands.Instance.Status(new[] { topUnversionedFolder }, StatusLevel.Local);
                        }
                        if (result == 2) return AssetMoveResult.FailedMove;
                    }
                    if (InUnversionedParentFolder(from, out topUnversionedFolder))
                    {
                        return AssetMoveResult.DidNotMove;
                    }
                    if (VCCommands.Instance.Move(from, to))
                    {
                        DebugLog.Log("Version Control Move: " + from + " => " + to);
                        return AssetMoveResult.DidMove;
                    }
                    return AssetMoveResult.DidNotMove;
                }
                return AssetMoveResult.FailedMove;
            }
            return AssetMoveResult.DidNotMove;
        }

        private static string[] OnWillSaveAssets(string[] assets)
        {
            if (UnityEditorInternal.InternalEditorUtility.inBatchMode ||
                !VCCommands.Active || 
                VCSettings.SaveStrategy == VCSettings.ESaveAssetsStrategy.Unity || 
                VCCommands.Instance.FlusingFiles ||
                EditorApplication.isCompiling)
            {
                return assets;
            }
                

            if(assets.Any(a => VCCommands.Instance.GetAssetStatus(a).reflectionLevel == VCReflectionLevel.None))
                VCCommands.Instance.Status(assets, StatusLevel.Previous);

            var toBeSaved = new List<string>();
            var noControl = new List<string>();

            foreach(var asset in assets)
            {
                //D.Log(asset+ " has ignored parentfolder: " + VCCommands.Instance.InIgnoredParentFolder(asset));
                if (VCUtility.HaveAssetControl(asset) || !VCUtility.ManagedByRepository(asset) || asset.InUnversionedParentFolder(VCCommands.Instance) || asset.InIgnoredParentFolder(VCCommands.Instance))
                    toBeSaved.Add(asset);
                else
                    noControl.Add(asset);
            }

            if (noControl.Count > 0)
            {
                foreach (var asset in noControl)
                {
                    string message = string.Format("Unity is trying to save following file which is not under control on {1}.\n\n'{0}'", asset, VCSettings.VersionControlBackend, Terminology.getlock);
                    int result = UserDialog.DisplayDialogComplex("Save File?", message, Terminology.allowLocalEdit, Terminology.getlock, "Do not save");
                    if (result == 0 || result == 1)
                    {
                        toBeSaved.Add(asset);
                        if(result == 0)
                            VCCommands.Instance.AllowLocalEdit(new[] { asset });
                        
                        if (result == 1)
                            VCCommands.Instance.GetLock(new[] { asset }, OperationMode.Normal);
                    }
                }
            }
            return toBeSaved.ToArray();
        }

        private static bool IsOpenForEdit(string assetPath, out string message)
        {
            if (VCCommands.Active && VCSettings.LockAssets)
            {
                var ap = new ComposedString(assetPath).TrimEnd(VCCAddMetaFiles.meta);
                if (!MergeHandler.IsMergableAsset(ap) && ap.StartsWith("Assets/"))
                {
                    var status = VCCommands.Instance.GetAssetStatus(ap);
                    message = AssetStatusUtils.GetStatusText(status);
                    return VCUtility.HaveAssetControl(status);
                }
            }
            
            message = "";
            return true;
            
        }
    }
}

