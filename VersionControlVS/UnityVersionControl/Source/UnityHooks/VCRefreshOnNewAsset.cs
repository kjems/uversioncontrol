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
            changedAssets.AddRange(importedAssets);
            changedAssets.AddRange(movedAssets);
            changedAssets = changedAssets.Distinct().ToList();
            if (changedAssets.Count > 0)
            {
                VCCommands.Instance.RequestStatus(changedAssets, false);
                changedAssets.Clear();
            }

            removedAssets.AddRange(deletedAssets);
            removedAssets.AddRange(movedFromAssetPaths);
            removedAssets = removedAssets.Distinct().ToList();
            if (removedAssets.Count > 0)
            {
                //VCCommands.Instance.RemoveFromDatabase(removedAssets);
                VCCommands.Instance.RequestStatus(changedAssets, true);
                removedAssets.Clear();
            }

        }
    }
}
