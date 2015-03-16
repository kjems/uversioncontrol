// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using VersionControl.Logging;

namespace VersionControl
{
    using UserInterface;
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    internal static class VCConflictHandler
    {
        private static readonly List<ComposedString> ignoredConflicts = new List<ComposedString>();
        public static void HandleConflicts()
        {
            bool detailsToggle = false;
            var conflicts = VCCommands.Instance.GetFilteredAssets(s => s.fileStatus == VCFileStatus.Conflicted || s.MetaStatus().fileStatus == VCFileStatus.Conflicted).Select(status => status.assetPath).ToArray();
            if (conflicts.Any())
            {
                foreach (var conflictIt in conflicts)
                {
                    if (ignoredConflicts.Contains(conflictIt)) continue;
                    bool mergable = VCUtility.IsMergableTextAsset(conflictIt);
                    const string explanation = "Theirs :\nUse the file from the server and discard local changes to the file\n\nMine :\nUse my version of the file and discard the changes someone else made on the server";
                    const string mergeExplanation = "Merge External :\nIgnore the conflict in UVC and handle the conflict in an external program";
                    const string ignoreExplanation = "Ignore :\nIgnore the conflict for now although the file will not be readable by Unity";
                    string message = string.Format("There is a conflict in the file  '{0}'\nUse 'Theirs' or 'Mine'?", conflictIt.Compose());
                    string details = string.Format("{0}\n\n{1}", explanation, mergable ? mergeExplanation : ignoreExplanation);
                                        
                    CustomDialog dialog = CustomDialog.Create("Conflict");
                    dialog
                    .CenterOnScreen()
                    .SetBodyGUI(() =>
                    {                        
                        EditorGUILayout.HelpBox(message, MessageType.Warning);
                        detailsToggle = GUILayout.Toggle(detailsToggle, "Details", EditorStyles.foldout);
                        if (detailsToggle)
                        {
                            EditorGUILayout.HelpBox(details, MessageType.None);                            
                        }
                        GUILayout.FlexibleSpace();
                    })                    
                    .AddButton("Theirs", () => { VCCommands.Instance.Resolve(new[] { conflictIt.Compose() }, ConflictResolution.Theirs); dialog.Close(); }, GUILayout.Width(80f))
                    .AddButton("Mine", () => { VCCommands.Instance.Resolve(new[] { conflictIt.Compose() }, ConflictResolution.Mine); dialog.Close(); }, GUILayout.Width(80f))
                    .AddButton(mergable ? "Merge External" : "Ignore", () => { ignoredConflicts.Add(conflictIt); dialog.Close(); }, GUILayout.Width(100f))
                    .SetSize(() => new Vector2(600f, detailsToggle ? 220f : 100f));
                    
                    dialog.ShowUtility();
                }
                OnNextUpdate.Do(AssetDatabase.Refresh);
            }
        }
    }
}
