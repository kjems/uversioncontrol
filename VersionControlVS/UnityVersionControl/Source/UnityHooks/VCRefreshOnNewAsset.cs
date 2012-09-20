// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using System.Collections.Generic;
using UnityEditor;
using System.Linq;

namespace VersionControl
{
    internal class RefreshOnNewAsset : AssetPostprocessor
    {
        private static List<string> changedAssets = new List<string>();
        private static List<string> removedAssets = new List<string>();
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            //D.Log("OnPostprocessAllAssets : imported: " + importedAssets.Length + ", deleted: " + deletedAssets.Length + ", moved: " + movedAssets.Length + ", movedFrom: " + movedAssets.Length);
            changedAssets.AddRange(importedAssets);
            changedAssets.AddRange(movedAssets);
            if (changedAssets.Count > 0)
            {
                changedAssets = changedAssets.Distinct().ToList();
                VCCommands.Instance.RemoveFromDatabase(changedAssets);
                VCCommands.Instance.RequestStatus(changedAssets, StatusLevel.Previous);
                changedAssets.Clear();
            }

            removedAssets.AddRange(deletedAssets);
            removedAssets.AddRange(movedFromAssetPaths);
            if (removedAssets.Count > 0)
            {
                removedAssets = removedAssets.Distinct().ToList();
                VCCommands.Instance.RemoveFromDatabase(removedAssets);
                VCCommands.Instance.RequestStatus(removedAssets, StatusLevel.Previous);
                removedAssets.Clear();
            }

        }
    }
}
