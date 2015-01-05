// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace VersionControl
{
    using Logging;
    using AssetPathFilters;
    internal class VCFileHandler : AssetModificationProcessor
    {
        /* Move and Rename Handled by VCRefreshOnNewAsset
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
                foreach (var folderIt in Path.GetDirectoryName(asset).Split(pathSeparator))
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
                acceptOperation = EditorUtility.DisplayDialog(command + " on repository?", assetPath + "\nis " + Terminology.getlock + " by [" + assetStatus.owner + "], are you sure you want to " + command + "?", command, "Cancel");
            }
            if (acceptOperation && assetStatus.fileStatus == VCFileStatus.Modified)
            {
                acceptOperation = EditorUtility.DisplayDialog(command + " on repository?", assetPath + "\nFile is modified on repository, are you sure you want to " + command + "?", command, "Cancel");
            }
            return acceptOperation;
        }
        
        private static AssetMoveResult OnWillMoveAsset(string from, string to)
        {
            if (!UnityEditorInternal.InternalEditorUtility.HasMaint()) return AssetMoveResult.DidNotMove;

            VersionControlStatus status = VCCommands.Instance.GetAssetStatus(from);
            if (VCUtility.ManagedByRepository(status))
            {
                if (DisplayConfirmationDialog("Move", from, status))
                {
                    string topUnversionedFolder;
                    if (InUnversionedParentFolder(to, out topUnversionedFolder))
                    {
                        int result = EditorUtility.DisplayDialogComplex("Add Folder?", "Versioned files are moved into an unversioned folder. Add following unversioned folder first?\n\n" + topUnversionedFolder, "Yes", "No", "Cancel");
                        if (result == 0)
                        {
                            VCCommands.Instance.Add(new[] { topUnversionedFolder });
                            VCCommands.Instance.Status(new[] { topUnversionedFolder }, StatusLevel.Local);
                        }
                        if (result == 2) return AssetMoveResult.FailedMove;
                    }
                    if (VCCommands.Instance.Move(from, to))
                    {
                        D.Log("Version Control Move: " + from + " => " + to);
                        return AssetMoveResult.DidMove;
                    }
                    return AssetMoveResult.DidNotMove;
                }
                return AssetMoveResult.FailedMove;
            }
            return AssetMoveResult.DidNotMove;
        }*/

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
                    int result = EditorUtility.DisplayDialogComplex("Save File?", message, Terminology.allowLocalEdit, Terminology.getlock, "Do not save");
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

    }
}

