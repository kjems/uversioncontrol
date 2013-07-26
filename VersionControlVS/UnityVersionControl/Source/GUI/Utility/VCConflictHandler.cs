// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace VersionControl
{
    internal static class VCConflictHandler
    {
        private static readonly List<ComposedString> ignoredConflicts = new List<ComposedString>();
        public static void HandleConflicts()
        {
            var conflicts = VCCommands.Instance.GetFilteredAssets(s => s.fileStatus == VCFileStatus.Conflicted || s.MetaStatus().fileStatus == VCFileStatus.Conflicted).Select(status => status.assetPath).ToArray();
            if (conflicts.Any())
            {
                foreach (var conflictIt in conflicts)
                {
                    if (ignoredConflicts.Contains(conflictIt)) continue;

                    int result = EditorUtility.DisplayDialogComplex("Conflict", "There is a conflict in the file '" + conflictIt.GetString() + "'. Use 'Theirs' or 'Mine'?", "Theirs", "Mine", "Ignore");
                    if (result == 0 || result == 1)
                    {
                        VCCommands.Instance.Resolve(new[] { conflictIt.GetString() }, result == 0 ? ConflictResolution.Theirs : ConflictResolution.Mine);
                    }
                    else
                    {
                        ignoredConflicts.Add(conflictIt);
                    }
                }
                OnNextUpdate.Do(AssetDatabase.Refresh);
            }
        }
    }
}
