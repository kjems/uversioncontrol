using System;
using UnityEditor;
using UVC.Logging;

namespace UVC
{
    [InitializeOnLoad]
    public static class AssetDatabaseRefreshManager
    {
        private static bool pendingAssetDatabaseRefresh = false;
        private static bool pauseAssetDatabaseRefresh = false;

        private static Action refreshAssetDatabaseSynchronous = () => AssetDatabase.Refresh();

        static AssetDatabaseRefreshManager()
        {
            VCCommands.Instance.OperationStarting += OnOperationStarting;
            VCCommands.Instance.OperationCompleted += OnOperationCompleted;
            VerifyAutoRefresh();
        }

        private static void OnOperationCompleted(OperationType type, VersionControlStatus[] beforeStatuses, VersionControlStatus[] afterStatuses, bool result)
        {
            if(type == OperationType.Update)
                EnableAutoRefresh();
        }

        private static bool OnOperationStarting(OperationType type, VersionControlStatus[] beforeStatuses)
        {
            if(type == OperationType.Update)
                DisableAutoRefresh();

            return true;
        }

        public static void SetImportAssetDatabaseSynchronousCallback(Action refreshSynchronous)
        {
            refreshAssetDatabaseSynchronous = refreshSynchronous;
        }


        public static bool RequestAssetDatabaseRefresh()
        {
            // The AssetDatabase will be refreshed on next status update
            pendingAssetDatabaseRefresh = true;
            return true;
        }

        public static void RefreshAssetDatabase()
        {
            if (pendingAssetDatabaseRefresh && !pauseAssetDatabaseRefresh)
            {
                pendingAssetDatabaseRefresh = false;
                OnNextUpdate.Do(() =>
                {
                    VCConflictHandler.HandleConflicts();
                    refreshAssetDatabaseSynchronous();
                });
            }
        }

        public static void VerifyAutoRefresh()
        {
            EnableAutoRefresh();
            if (EditorPrefs.GetInt("kAutoRefreshDisableCount", 0) > 0 && EditorPrefs.GetBool("kAutoRefresh", false))
            {
                EditorPrefs.SetInt("kAutoRefreshDisableCount", 0);
                DebugLog.Log("Resetting kAutoRefreshDisableCount");
            }
            if(EditorPrefs.GetInt("kAutoRefreshDisableCount", 0) < 0)
            {
                EditorPrefs.SetInt("kAutoRefreshDisableCount", 0);
                EditorPrefs.SetBool("kAutoRefresh", true);
                DebugLog.Log("Resetting kAutoRefreshDisableCount");
            }
        }

        static void DisableAutoRefresh()
        {
            if (EditorPrefs.GetInt("kAutoRefreshDisableCount", 0) == 0)
            {
                EditorPrefs.SetBool("kAutoRefresh", false);
                //D.Log("Set AutoRefresh : " + EditorPrefs.GetBool("kAutoRefresh", true));
            }
            EditorPrefs.SetInt("kAutoRefreshDisableCount", EditorPrefs.GetInt("kAutoRefreshDisableCount", 0) + 1);
            EditorPrefs.SetBool("VCCommands/kAutoRefreshOwner", true);
            //D.Log("kAutoRefreshDisableCount : " + EditorPrefs.GetInt("kAutoRefreshDisableCount", 0));
        }

        static void EnableAutoRefresh()
        {
            if (EditorPrefs.GetBool("VCCommands/kAutoRefreshOwner", false))
            {
                EditorPrefs.SetBool("VCCommands/kAutoRefreshOwner", false);
                EditorPrefs.SetInt("kAutoRefreshDisableCount", EditorPrefs.GetInt("kAutoRefreshDisableCount", 0) - 1);
                if (EditorPrefs.GetInt("kAutoRefreshDisableCount", 0) == 0)
                {
                    EditorPrefs.SetBool("kAutoRefresh", true);
                    //D.Log("Set AutoRefresh : " + EditorPrefs.GetBool("kAutoRefresh", true));
                }
                //D.Log("kAutoRefreshDisableCount : " + EditorPrefs.GetInt("kAutoRefreshDisableCount", 0));
            }
        }

        public static void PauseAssetDatabaseRefresh()
        {
            pauseAssetDatabaseRefresh = true;
            DisableAutoRefresh();
        }

        public static void ResumeAssetDatabaseRefresh()
        {
            EnableAutoRefresh();
            pauseAssetDatabaseRefresh = false;
            RefreshAssetDatabase();
        }
    }
}
