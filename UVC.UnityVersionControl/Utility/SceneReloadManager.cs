// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using UnityEditor;

namespace UVC
{
    using UnityEditor.SceneManagement;
    using UnityEngine.SceneManagement;
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    [InitializeOnLoad]
    public static class SceneReloadManager
    {
        static SceneReloadManager()
        {
            VCCommands.Instance.OperationStarting += OnOperationStarting;
            //VCCommands.Instance.OperationCompleted += OnOperationCompleted;
        }

        private static bool OnOperationStarting(OperationType operationType, VersionControlStatus[] statuses)
        {
            if (operationType == OperationType.Revert)
            {
                foreach (var status in statuses)
                {
                    for (int i = 0, length = SceneManager.sceneCount; i < length; i++)
                    {
                        var scene = SceneManager.GetSceneAt(i);
                        if (scene.isLoaded && scene.isDirty)
                        {
                            if (status.assetPath.Contains(new ComposedString(scene.path)))
                            {
                                EditorSceneManager.SaveScene(scene);
                            }
                        }
                    }
                }
            }
            return true;
        }

        /*
        private static void OnOperationCompleted(OperationType operationType, VersionControlStatus[] statusesBefore, VersionControlStatus[] statusesAfter, bool success)
        {
            if (success && (operationType == OperationType.Revert || operationType == OperationType.GetLock))
            {
                foreach (var statusBefore in statusesBefore)
                {
                    for (int i = 0, length = SceneManager.sceneCount; i < length; i++)
                    {
                        var scene = SceneManager.GetSceneAt(i);
                        if (scene.isLoaded && scene.isDirty)
                        {
                            if (statusBefore.assetPath.Contains(new ComposedString(scene.path)))
                            {
                                if (operationType == OperationType.GetLock)
                                {
                                    if (statusBefore.fileStatus != VCFileStatus.Unversioned && statusBefore.fileStatus != VCFileStatus.Added && !VCUtility.HaveVCLock(statusBefore))
                                    {
                                        PromptToReloadScene(scene);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void ReloadScene(Scene scene)
        {
            var reloadSceneInfo = typeof(EditorSceneManager).GetMethod("ReloadScene", BindingFlags.Static | BindingFlags.NonPublic);
            reloadSceneInfo.Invoke(null, new[] {(object) scene});
        }

        private static void PromptToReloadScene(Scene scene)
        {
            if (EditorUtility.DisplayDialog("Discard changes?", $"Discard changes from loaded and modified scene: {scene.path}", "Yes", "No"))
            {
                ReloadScene(scene);
            }
        }
        */

    }
}
