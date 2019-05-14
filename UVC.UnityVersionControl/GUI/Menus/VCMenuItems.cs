// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

// This script includes menu items for common VC operations

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UVC.UserInterface;

namespace UVC
{
    using Extensions;
    internal class VCMenuItems : ScriptableObject
    {
        private static List<string> GetAssetPathsOfSelected()
        {
            return Selection.objects.Select(ObjectExtension.GetAssetPath).ToList();
        }

        [MenuItem("Assets/UVC/" + Terminology.add, true)]
        [MenuItem("Assets/UVC/" + Terminology.delete, true)]
        [MenuItem("Assets/UVC/" + Terminology.allowLocalEdit, true)]
        [MenuItem("Assets/UVC/" + Terminology.revert, true)]
        [MenuItem("Assets/UVC/" + Terminology.commit, true)]
        [MenuItem("Assets/UVC/" + Terminology.getlock, true)]
        [MenuItem("Assets/UVC/" + Terminology.unlock, true)]
        [MenuItem("CONTEXT/GameObject/" + Terminology.unlock, true)]
        [MenuItem("CONTEXT/GameObject/Force " + Terminology.getlock, true)]
        [MenuItem("CONTEXT/GameObject/" + Terminology.getlock, true)]
        public static bool VCActive()
        {
            return VCCommands.Instance.IsReady();
        }
        
        [MenuItem("CONTEXT/GameObject/" + Terminology.getlock)]
        private static void VCGetLockGameobjectContext(MenuCommand command)
        {
            VCCommands.Instance.GetLock(new[] {command.context.GetAssetPath()});
        }

        [MenuItem("CONTEXT/GameObject/Force " + Terminology.getlock)]
        private static void VCForceGetLockGameobjectContext(MenuCommand command)
        {
            string assetPath = command.context.GetAssetPath();
            VersionControlStatus assetStatus = VCCommands.Instance.GetAssetStatus(assetPath);
            if (assetStatus.lockStatus == VCLockStatus.LockedOther)
            {
                if (UserDialog.DisplayDialog("Force " + Terminology.getlock, "Are you sure you will steal the file from: [" + assetStatus.owner + "]", "Yes", "Cancel"))
                {
                    VCCommands.Instance.GetLock(new[] {assetPath}, OperationMode.Force);
                }
            }
        }

        [MenuItem("CONTEXT/GameObject/" + Terminology.unlock)]
        private static void VCUnLockGameobjectContext(MenuCommand command)
        {
            VCCommands.Instance.ReleaseLock(new[] {command.context.GetAssetPath()});
        }

        [MenuItem("Assets/UVC/" + Terminology.getlock)]
        private static void VCGetLockProjectContext()
        {
            VCCommands.Instance.GetLock(GetAssetPathsOfSelected());
        }
        
        [MenuItem("Assets/UVC/" + Terminology.unlock)]
        private static void VCGetUnLockProjectContext()
        {
            VCCommands.Instance.ReleaseLock(GetAssetPathsOfSelected());
        }

        // Commit
        [MenuItem("Assets/UVC/" + Terminology.commit)]
        private static void VCCommitProjectContext()
        {
            VCCommands.Instance.CommitDialog(GetAssetPathsOfSelected());
        }

        [MenuItem("Assets/UVC/" + Terminology.revert)]
        private static void VCRevertProjectContext()
        {
            //D.Log(GetAssetPathsOfSelected().Aggregate((a, b) => a + "\n" + b));
            VCCommands.Instance.Revert(GetAssetPathsOfSelected());
        }

        [MenuItem("Assets/UVC/" + Terminology.allowLocalEdit)]
        public static void VCAllowLocalEditProjectContext()
        {
            VCCommands.Instance.AllowLocalEdit(GetAssetPathsOfSelected());
        }

        // Delete
        [MenuItem("Assets/UVC/" + Terminology.delete)]
        public static void VCDeleteProjectContext()
        {
            VCUtility.VCDeleteWithConfirmation(GetAssetPathsOfSelected());
        }

        // Add
        [MenuItem("Assets/UVC/" + Terminology.add)]
        public static void VCAddProjectContext()
        {
            VCCommands.Instance.Add(GetAssetPathsOfSelected());
        }
        
        [MenuItem("Assets/UVC/" + Terminology.update)]
        public static void VCUpdateSelection()
        {
            VCCommands.Instance.Update(GetAssetPathsOfSelected());
        }
        
        [MenuItem("Assets/UVC/" + Terminology.changelist)]
        public static void VCChangeListAdd()
        {
            ChangeListWindow.Open(GetAssetPathsOfSelected());
        }
    }
}
