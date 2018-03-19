// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using VersionControl.Logging;

namespace VersionControl
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
                    bool mergable = VCUtility.IsMergableAsset(conflictIt);
                    const string explanation = "\nTheirs :\nUse the file from the server and discard local changes to the file\n\nMine :\nUse my version of the file and discard the changes someone else made on the server";
                    const string mergeExplanation = "\nMerge External :\nIgnore the conflict in UVC and handle the conflict in an external program";
                    const string ignoreExplanation = "\nIgnore :\nIgnore the conflict for now although the file will not be readable by Unity";
                    string message = string.Format("There is a conflict in the file:\n '{0}'\n\nUse 'Theirs' or 'Mine'?\n {1}\n{2}\n", conflictIt.Compose(), explanation, mergable ? mergeExplanation : ignoreExplanation);
                    int result = EditorUtility.DisplayDialogComplex("Conflict", message, "Theirs", "Mine", mergable ? "Merge External" : "Ignore");
                    if (result == 0 || result == 1)
                    {
                        VCCommands.Instance.Resolve(new[] { conflictIt.Compose() }, result == 0 ? ConflictResolution.Theirs : ConflictResolution.Mine);
                    }
                    else
                    {
                        ignoredConflicts.Add(conflictIt);
                        /*if (mergable)
                        {
                            string mine, theirs, basePath;
                            if(VCCommands.Instance.GetConflict(conflictIt.Compose(), out basePath, out mine, out theirs))
                            {
                                EditorUtility.InvokeDiffTool("Mine : " + mine, mine, "Theirs : " + theirs, theirs, "Base: " + basePath, basePath);
                            }
                        }*/
                    }
                }
                OnNextUpdate.Do(AssetDatabase.Refresh);
            }
        }
    }
}
