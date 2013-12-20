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
    internal class VCFileHandler : AssetModificationProcessor
    {

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
                    parentFolders.Add(System.Uri.EscapeUriString(currentFolder.TrimEnd(pathSeparator)));
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
        }

        private static string[] OnWillSaveAssets(string[] assets)
        {
            if (VCSettings.SaveStrategy == VCSettings.ESaveAssetsStrategy.VersionControl)
            {
                assets = assets.Where(a => VCUtility.HaveAssetControl(a)).ToArray();
                //if (assets.Length > 0) D.Log("OnWillSaveAssets : " + assets.Aggregate((a, b) => a + ", " + b));
            }
            return assets;
        }

        /* // Would be possible to auto Add new files to Version Control, but it feels a bit aggressive
        private static void OnWillCreateAsset(string assetPath)
        {            
            //D.Log("OnWillCreateAsset: " + assetPath);
        }
        */

        /* // Would be possible to auto Delete files in Version Control when deleted in projectview, but it seems to confuse more than help
        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions option)
        {
            if (!UnityEditorInternal.InternalEditorUtility.HasMaint()) return AssetDeleteResult.DidNotDelete;
           
            VersionControlStatus status = VCCommands.instance.GetAssetStatus(assetPath);
            if (VCUtility.ManagedByRepository(status))
            {
                if (DisplayConfirmationDialog("Delete", assetPath, status))
                {
                    VCCommands.instance.Delete(new[] { assetPath }, true);
                    return AssetDeleteResult.DidDelete;
                }
                return AssetDeleteResult.FailedDelete;
            }
            return AssetDeleteResult.DidNotDelete;
        }
        */

    }
}

