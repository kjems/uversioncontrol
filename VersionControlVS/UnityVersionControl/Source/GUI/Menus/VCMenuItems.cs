// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

// This script includes menu items for common VC operations

using System.IO;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace VersionControl
{
    internal class VCMenuItems : ScriptableObject
    {
        private static IEnumerable<string> GetAssetPathsOfSelected()
        {
            return Selection.objects.Select<Object, string>(ObjectExtension.GetAssetPath);
        }

        [MenuItem("Assets/UVC/" + Terminology.add, true)]
        [MenuItem("Assets/UVC/" + Terminology.delete, true)]
        [MenuItem("Assets/UVC/" + Terminology.bypass, true)]
        [MenuItem("Assets/UVC/" + Terminology.revert, true)]
        [MenuItem("Assets/UVC/" + Terminology.commit, true)]
        [MenuItem("Assets/UVC/Open", true)]
        [MenuItem("CONTEXT/GameObject/" + Terminology.unlock, true)]
        [MenuItem("CONTEXT/GameObject/Force " + Terminology.getlock, true)]
        [MenuItem("CONTEXT/GameObject/" + Terminology.getlock, true)]
        [MenuItem("UVC/Update", true, -12)]
        [MenuItem("UVC/Refresh", true, -11)]
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
                if (EditorUtility.DisplayDialog("Force " + Terminology.getlock, "Are you sure you will steal the file from: [" + assetStatus.owner + "]", "Yes", "Cancel"))
                {
                    VCCommands.Instance.GetLock(new[] {assetPath}, true);
                }
            }
        }

        [MenuItem("CONTEXT/GameObject/" + Terminology.unlock)]
        private static void VCUnLockGameobjectContext(MenuCommand command)
        {
            VCCommands.Instance.ReleaseLock(new[] {command.context.GetAssetPath()});
        }

        [MenuItem("Assets/UVC/Open")]
        private static void VCGetLockProjectContext()
        {
            VCCommands.Instance.GetLock(GetAssetPathsOfSelected().ToArray());
        }

        // Commit
        [MenuItem("Assets/UVC/" + Terminology.commit)]
        private static void VCCommitProjectContext()
        {
            VCCommands.Instance.CommitDialog(GetAssetPathsOfSelected().ToArray());
        }

        [MenuItem("Assets/UVC/" + Terminology.revert)]
        private static void VCRevertProjectContext()
        {
            //D.Log(GetAssetPathsOfSelected().Aggregate((a, b) => a + "\n" + b));
            VCCommands.Instance.Revert(GetAssetPathsOfSelected().ToArray());
        }

        [MenuItem("Assets/UVC/" + Terminology.bypass)]
        public static void VCBypassLockProjectContext()
        {
            VCCommands.Instance.BypassRevision(GetAssetPathsOfSelected().ToArray());
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
            VCCommands.Instance.Add(GetAssetPathsOfSelected().ToArray());
        }

        [MenuItem("UVC/Refresh", false, -12)]
        public static void VCRefresh()
        {
            VCCommands.Instance.RequestStatus();
        }

        [MenuItem("Assets/UVC/" + Terminology.update)]
        public static void VCUpdateSelection()
        {
            VCCommands.Instance.Update(GetAssetPathsOfSelected().ToArray());
        }

        [MenuItem("UVC/Update", false, -11)]
        public static void VCUpdate()
        {
            bool result = VCCommands.Instance.Update(null);
            if(result) Debug.Log("Version Control Update finished successfully");
            else Debug.LogWarning("Version Control Update failed");
        }

        //[MenuItem("UVC/Submit Bug Report")]
        //public static void SubmitBugToGithub()
        //{
            // Make dialog box and submit to fogbugz
        //}
        
    }
}
