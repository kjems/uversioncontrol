// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;

namespace UVC
{
    using Logging;

    internal class RefreshOnNewAsset : AssetPostprocessor
    {
        private static List<string> changedAssets = new List<string>();
        private static int callcount = 0;
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (VCCommands.Active)
            {
                DebugLog.Log("OnPostprocessAllAssets : imported: " + importedAssets.Length + ", deleted: " + deletedAssets.Length + ", moved: " + movedAssets.Length + ", movedFrom: " + movedAssets.Length);

                if (VCSettings.HandleFileMove == VCSettings.EHandleFileMove.Simple)
                {
                    if (deletedAssets.Length == 0 && movedAssets.Length > 0 && movedAssets.Length == movedFromAssetPaths.Length)
                    {
                        callcount++;
                        if (callcount == 1)
                        {
                            var parentFolders = RemoveFilesIfParentFolderInList(movedAssets);
                            for (int i = 0; i < movedAssets.Length; ++i)
                            {
                                string from = movedFromAssetPaths[i];
                                string to = movedAssets[i];
                                if ((File.Exists(to) || Directory.Exists(to) && !File.Exists(from) && !Directory.Exists(from)) && parentFolders.Contains(to))
                                {
                                    ReMoveAssetOnVC(movedFromAssetPaths[i], movedAssets[i]);
                                }
                            }
                            callcount--;
                        }
                    }
                }

                changedAssets.AddRange(importedAssets);
                changedAssets.AddRange(movedAssets);
                changedAssets.AddRange(deletedAssets);
                changedAssets.AddRange(movedFromAssetPaths);
                if (changedAssets.Count > 0)
                {
                    changedAssets = changedAssets.Distinct().ToList();
                    VCCommands.Instance.RemoveFromDatabase(changedAssets);
                    VCCommands.Instance.RequestStatus(changedAssets, StatusLevel.Previous);
                    changedAssets.Clear();
                }
            }
            GameObjectToAssetPathCache.ClearObjectToAssetPathCache();
        }

        static IEnumerable<string> RemoveFilesIfParentFolderInList(IEnumerable<string> assets)
        {
            var folders = assets.Where(a => Directory.Exists(a));
            return assets.Where(a => !folders.Any(f => a.StartsWith(f) && a != f)).ToArray();
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
                acceptOperation = EditorUtility.DisplayDialog(command + " on repository?", assetPath + "\nis " + Terminology.getlock + " by [" + assetStatus.owner + "], are you sure you want to " + command + "?", command, "Cancel");
            }
            if (acceptOperation && assetStatus.fileStatus == VCFileStatus.Modified)
            {
                acceptOperation = EditorUtility.DisplayDialog(command + " on repository?", assetPath + "\nFile is modified on repository, are you sure you want to " + command + "?", command, "Cancel");
            }
            return acceptOperation;
        }

        private static void ReMoveAssetOnVC(string from, string to)
        {
            VersionControlStatus status = VCCommands.Instance.GetAssetStatus(from);
            string topUnversionedFolder;
            if (VCUtility.ManagedByRepository(status) && !InUnversionedParentFolder(from, out topUnversionedFolder))
            {
                if (DisplayConfirmationDialog("Move", from, status))
                {
                    if (InUnversionedParentFolder(to, out topUnversionedFolder))
                    {
                        string msg = "Versioned files are moved into an unversioned folder. Add following unversioned folder first?\n\n" + topUnversionedFolder;
                        int result = EditorUtility.DisplayDialogComplex("Add Folder?", msg, "Yes", "No", "Cancel");
                        if (result == 0)
                        {
                            MoveAssetBack(from, to);
                            VCCommands.Instance.Add(new[] { topUnversionedFolder });
                            VCCommands.Instance.Status(new[] { topUnversionedFolder }, StatusLevel.Local);
                        }
                        if (result == 1)
                        {
                            VCCommands.Instance.Delete(new[] { from }, OperationMode.Force);
                            return;
                        }
                        if (result == 2)
                        {
                            MoveAssetBack(from, to);
                            return;
                        }
                    }
                    else
                    {
                        MoveAssetBack(from, to);
                    }

                    VCCommands.Instance.Move(from, to);
                    AssetDatabase.Refresh();
                    GameObjectToAssetPathCache.ClearObjectToAssetPathCache();

                }
            }
        }

        private static void MoveAssetBack(string from, string to)
        {
            if (Directory.Exists(to))
            {
                //D.Log("Directory Move : " + to + " => " + from);
                Directory.Move(to, from);
                File.Move(to + ".meta", from + ".meta");
            }
            else
            {
                //D.Log("File Move : " + to + " => " + from);
                File.Move(to, from);
                File.Move(to + ".meta", from + ".meta");
            }

            AssetDatabase.Refresh();

        }
    }

    [InitializeOnLoad]
    internal static class GameObjectToAssetPathCache
    {
        private static readonly Dictionary<int, string> gameObjectToAssetPath = new Dictionary<int, string>();

        static GameObjectToAssetPathCache()
        {
            VCCommands.Instance.StatusCompleted += ClearObjectToAssetPathCache;
            EditorApplication.hierarchyChanged += ClearObjectToAssetPathCache;
        }

        public static void ClearObjectToAssetPathCache()
        {
            gameObjectToAssetPath.Clear();
        }

        public static bool TryGetValue(Object obj, out string assetPath)
        {
            return gameObjectToAssetPath.TryGetValue(obj.GetInstanceID(), out assetPath);
        }

        public static void Add(Object obj, string assetPath)
        {
            if (!string.IsNullOrEmpty(assetPath)) gameObjectToAssetPath.Add(obj.GetInstanceID(), assetPath);
        }
    }
}


