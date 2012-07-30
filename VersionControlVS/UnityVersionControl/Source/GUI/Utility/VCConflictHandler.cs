// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace VersionControl
{
    [InitializeOnLoad]
    internal static class VCConflictHandler
    {
        static VCConflictHandler()
        {
            VCCommands.Instance.StatusCompleted += HandleConflicts;
        }
        private static readonly List<string> ignoredConflicts = new List<string>();
        private static void HandleConflicts()
        {
            var conflicts = VCCommands.Instance.GetFilteredAssets((a, s) => s.fileStatus == VCFileStatus.Conflicted || s.MetaStatus().fileStatus == VCFileStatus.Conflicted).ToArray();
            if (conflicts.Any())
            {
                foreach (var conflictIt in conflicts)
                {
                    if (ignoredConflicts.Contains(conflictIt)) continue;

                    int result = EditorUtility.DisplayDialogComplex("Conflict", "There is a conflict in the file '" + conflictIt + "'. Use 'Theirs' or 'Mine'?", "Theirs", "Mine", "Ignore");
                    if (result == 0 || result == 1)
                    {
                        VCCommands.Instance.Resolve(new[] { conflictIt, conflictIt + ".meta" }, result == 0 ? ConflictResolution.Theirs : ConflictResolution.Mine);
                    }
                    else
                    {
                        ignoredConflicts.Add(conflictIt);
                    }
                }
                AssetDatabase.Refresh();
            }
        }
    }
}
