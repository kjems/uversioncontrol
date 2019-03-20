// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

namespace UVC
{
    using UnityEditor.SceneManagement;
    using UnityEngine.SceneManagement;
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

        public static Scene GetSceneFromHandle(int handle)
        {
            System.Type T = System.Type.GetType("UnityEditor.SceneManagement.EditorSceneManager,UnityEditor");
            System.Reflection.MethodInfo getSceneByHandleInfo = T.GetMethod("GetSceneByHandle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (Scene)getSceneByHandleInfo.Invoke(null, new object[] {handle});
        }

        public static string GetSceneAssetPathFromHandle(int handle)
        {
            Scene scene = GetSceneFromHandle(handle);
            return scene.path;
        }
    }
}