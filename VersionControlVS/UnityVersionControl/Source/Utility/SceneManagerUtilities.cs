// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEngine;
using UnityEditor;

namespace VersionControl
{
    using UnityEditor.SceneManagement;
    public static class SceneManagerUtilities
    {
        public static string GetCurrentScenePath()
        {
            return EditorSceneManager.GetActiveScene().path;
        }

        public static void SaveActiveScene()
        {
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        public static void SaveActiveSceneToPath(string scenePath)
        {
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath, true);
        }

        public static string[] LoadedScenePaths()
        {
            string[] scenePaths = new string[EditorSceneManager.loadedSceneCount];
            for(int i = 0, count = EditorSceneManager.loadedSceneCount; i < count; ++i)
            {
                scenePaths[i] = EditorSceneManager.GetSceneAt(i).path;
            }
            return scenePaths;
        }

        public static void SaveCurrentModifiedScenesIfUserWantsTo()
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }
    }
}