// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace UVC
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
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
                    bool mergable = MergeHandler.IsMergableAsset(conflictIt);
                    const string explanation = "\nTheirs :\nUse the file from the server and discard local changes to the file\n\nMine :\nUse my version of the file and discard the changes someone else made on the server. File will become 'Local Only'";
                    const string mergeExplanation = "\nMerge External :\nIgnore the conflict in UVC and handle the conflict in an external program";
                    const string ignoreExplanation = "\nIgnore :\nIgnore the conflict for now although the file will not be readable by Unity";
                    string message = $"There is a conflict in the file:\n '{conflictIt.Compose()}'\n\nUse 'Theirs' or 'Mine'?\n {explanation}\n{(mergable ? mergeExplanation : ignoreExplanation)}\n";
                    int result = UserDialog.DisplayDialogComplex("Conflict", message, "Theirs", "Mine", mergable ? "Merge External" : "Ignore");
                    if (result == 0 || result == 1)
                    {
                        VCCommands.Instance.Resolve(new[] { conflictIt.Compose() }, result == 0 ? ConflictResolution.Theirs : ConflictResolution.Mine);
                    }
                    else
                    {
                        ignoredConflicts.Add(conflictIt);
                        if (mergable)
                        {
                            string assetPath = conflictIt.Compose();
                            if(VCCommands.Instance.GetConflict(assetPath, out var basePath, out var yours, out var theirs))
                            {
                                MergeHandler.ResolveConflict(assetPath, basePath, theirs, yours);
                            }
                        }
                    }
                }
                OnNextUpdate.Do(AssetDatabase.Refresh);
            }
        }
    }
}
