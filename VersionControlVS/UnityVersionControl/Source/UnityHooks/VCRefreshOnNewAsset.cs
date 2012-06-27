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
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var changedAssets = new List<string>();
            changedAssets.AddRange(importedAssets);
            changedAssets.AddRange(deletedAssets);
            changedAssets.AddRange(movedAssets);
            changedAssets.AddRange(movedFromAssetPaths);
            if (changedAssets.Count > 0) VCCommands.Instance.RequestStatus(changedAssets, true);
        }
    }
}
