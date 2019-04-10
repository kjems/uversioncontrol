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
    public static class SaveAssetsBeforeOperationManager
    {
        static SaveAssetsBeforeOperationManager()
        {
            VCCommands.Instance.OperationStarting += OnOperationStarting;
            VCCommands.Instance.OperationCompleted += OnOperationCompleted;
        }

        static void OnOperationCompleted(OperationType operationType, VersionControlStatus[] beforeStatuses, VersionControlStatus[] afterStatuses, bool success)
        {
            
        }

        static bool OnOperationStarting(OperationType operationType, VersionControlStatus[] statuses)
        {
            if (operationType == OperationType.Revert || operationType == OperationType.Commit)
            {
                foreach (var status in statuses)
                {
                    // Save Scenes
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
                    
                    // Save Prefabs
                    if (PrefabHelper.IsAssetPathOpenAsPrefabStage(status.assetPath.Compose()))
                    {
                        PrefabHelper.SaveOpenPrefabStage();
                    }
                }
            }
            return true;
        }
    }
}
