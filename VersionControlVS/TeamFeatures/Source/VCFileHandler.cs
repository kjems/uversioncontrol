// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnitySVN@gmail.com>
using UnityEngine;
using UnityEditor;

namespace VersionControl
{
    internal class VCFileHandler : AssetModificationProcessor
    {
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

