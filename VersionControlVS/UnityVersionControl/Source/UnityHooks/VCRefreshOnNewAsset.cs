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
            changedAssets = AssetpathsFilters.AddMeta(changedAssets.Distinct(), true).ToList();
            if (changedAssets.Count > 0)
            {
                VCCommands.Instance.RequestStatus(changedAssets);
                changedAssets.Clear();
            }

            removedAssets.AddRange(deletedAssets);
            removedAssets.AddRange(movedFromAssetPaths);
            removedAssets = AssetpathsFilters.AddMeta(removedAssets.Distinct(), true).ToList();
            if (removedAssets.Count > 0)
            {
                VCCommands.Instance.RemoveFromDatabase(removedAssets);
                VCCommands.Instance.RequestStatus(removedAssets);
                removedAssets.Clear();
            }

        }
    }
}
