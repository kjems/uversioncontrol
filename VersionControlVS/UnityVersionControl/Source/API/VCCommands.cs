// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

namespace VersionControl
{
    using Logging;
    using UnityEngine;
    using UnityEditor;
    using UserInterface;

    [InitializeOnLoad]
    public class VCCommandsOnLoad
    {
        static VCCommandsOnLoad() { OnNextUpdate.Do(VCCommands.Initialize); }
    }

    public enum OperationType
    {
        Update,
        Revert,
        Commit,
        GetLock,
        ReleaseLock,
        Delete,
        Move,
        CleanUp,
        Resolve,
    }

    /// <summary>
    /// Wraps the underlying IVersionControlCommands and add handling of .meta files and other Unity specific concerns.
    /// OnNextUpdate.Do is used to delay callback actions to the next Unity update as Unity does not allow calls into UnityEngine
    /// or UnityEditor if not orriginating from Unity's update loop.
    /// </summary>
    public sealed class VCCommands : IVersionControlCommands
    {
        private bool stopping = false;
        private bool updating = false;
        private bool pendingAssetDatabaseRefresh = false;
        static VCCommands instance;
        public static void Initialize() { if (instance == null) { instance = new VCCommands(); } }
        public static VCCommands Instance { get { Initialize(); return instance; } }

        private IVersionControlCommands vcc;
        private bool ignoreStatusRequests = false;
        private Action<Object> saveSceneCallback = o => EditorApplication.SaveScene();

        public event Action<string> ProgressInformation;
        public event Action StatusCompleted;
        public event Action<OperationType> OperationCompleted;
        public event Action<List<string>> PreCommit;
        public event Action Started;

        private VCCommands()
        {
            VersionControlFactory.VersionControlBackendChanged += OnVersionControlBackendChanged;
            if (!VersionControlFactory.CreateVersionControlCommands(VCSettings.VersionControlBackend))
            {
                VCSettings.VersionControlBackend = VCSettings.EVersionControlBackend.None;
            }
        }

        private void OnVersionControlBackendChanged(IVersionControlCommands newVcc)
        {
            if (vcc != null) vcc.Dispose();
            vcc = newVcc;
            vcc.ProgressInformation += progress =>
            {
                if (ProgressInformation != null)
                    OnNextUpdate.Do(() => ProgressInformation(progress));
            };
            vcc.StatusCompleted += OnStatusCompleted;
            OnNextUpdate.Do(Start);
            EditorApplication.playmodeStateChanged += OnPlaymodeStateChanged;
        }

        public static bool Active
        {
            get
            {
                bool playmode = ThreadUtility.IsMainThread() && EditorApplication.isPlayingOrWillChangePlaymode;
                return VCSettings.VCEnabled && !playmode;
            }
        }

        public bool Ready { get { return Active && vcc.IsReady(); } }

        public void Dispose()
        {
            vcc.Dispose();
        }

        public void Start()
        {
            if (Active)
            {
                bool remoteProjectReflection = VCSettings.ProjectReflectionMode == VCSettings.EReflectionLevel.Remote;
                var statusLevel = remoteProjectReflection ? StatusLevel.Remote : StatusLevel.Local;
                var detailLevel = remoteProjectReflection ? DetailLevel.Verbose : DetailLevel.Normal;
                vcc.Start();
                StatusTask(statusLevel, detailLevel).ContinueWithOnNextUpdate(t => ActivateRefreshLoop());
                if (Started != null) Started();
            }
        }

        public void Stop()
        {
            vcc.Stop();
            vcc.DeactivateRefreshLoop();
            vcc.ClearDatabase();
        }

        public void ActivateRefreshLoop()
        {
            vcc.ActivateRefreshLoop();
        }

        public void DeactivateRefreshLoop()
        {
            vcc.DeactivateRefreshLoop();
        }


        #region Private methods

        private bool HandleExceptions(Func<bool> func)
        {
            if (Active)
            {
                try
                {
                    var result = func();
                    return result;
                }
                catch (VCException vcException)
                {
                    VCExceptionHandler.HandleException(vcException);
                }
                catch (Exception e)
                {
                    D.LogError("Unhandled exception: " + e.Message + "\n" + D.GetCallstack());
                    throw;
                }
            }
            else
            {
                D.Log("VC Action ignored due to not being active");
            }
            return false;
        }

        private void OnPlaymodeStateChanged()
        {
            if (updating)
            {
                if (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode && !stopping)
                {
                    stopping = true;
                    EditorApplication.isPlaying = false;
                    SceneView.lastActiveSceneView.ShowNotification(new GUIContent("Playmode cancelled because Version Control 'Update' is in progress"));
                }
                stopping = false;
            }
            else
            {
                if (!Application.isPlaying) Start();
                else Stop();
            }
        }

        private bool RequestAssetDatabaseRefresh()
        {
            // The AssetDatabase will be refreshed on next status update
            pendingAssetDatabaseRefresh = true;
            return true;
        }

        private void RefreshAssetDatabaseAfterStatusUpdate()
        {
            if (pendingAssetDatabaseRefresh)
            {
                pendingAssetDatabaseRefresh = false;
                OnNextUpdate.Do(() =>
                {
                    VCConflictHandler.HandleConflicts();
                    AssetDatabase.Refresh();
                });
            }
        }

        private void FlushFiles()
        {
            if (ThreadUtility.IsMainThread())
            {
                ignoreStatusRequests = true;
                //D.Log("Flusing files");
                EditorApplication.SaveAssets();
                EditorUtility.UnloadUnusedAssets();
                ignoreStatusRequests = false;
            }
            //else Debug.Log("Ignoring 'FlushFiles' due to Execution context");
        }

        private static bool OpenCommitDialogWindow(IEnumerable<string> assets, IEnumerable<string> dependencies)
        {
            var commitWindow = ScriptableObject.CreateInstance<VCCommitWindow>();
            commitWindow.minSize = new Vector2(220, 140);
            commitWindow.title = "Commit...";
            commitWindow.SetAssetPaths(assets, dependencies);
            commitWindow.ShowUtility();
            return commitWindow.commitedFiles.Any();
        }
        #endregion

        #region IVersionControlCommands Tasks

        private Task<bool> StartTask(Func<bool> work)
        {
            var task = Active ? new Task<bool>(work) : new Task<bool>(() => false);
            task.Start(); // Sync: task.RunSynchronously();
            return task;
        }

        public Task<bool> StatusTask(StatusLevel statusLevel, DetailLevel detailLevel)
        {
            return StartTask(() => Status(statusLevel, detailLevel));
        }

        public Task<bool> StatusTask(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            assets = new List<string>(assets);
            return StartTask(() => Status(assets, statusLevel));
        }

        public Task<bool> UpdateTask(IEnumerable<string> assets = null)
        {
            if (assets != null) assets = new List<string>(assets);
            EditorApplication.LockReloadAssemblies();
            var updateTask = StartTask(() => Update(assets));
            updateTask.ContinueWithOnNextUpdate(t => EditorApplication.UnlockReloadAssemblies());
            return updateTask;
        }

        public Task<bool> AddTask(IEnumerable<string> assets)
        {
            assets = new List<string>(assets);
            return StartTask(() => Add(assets));
        }

        public Task<bool> CommitTask(IEnumerable<string> assets, string commitMessage = "")
        {
            assets = new List<string>(assets);
            FlushFiles();
            return StartTask(() => Commit(assets, commitMessage));
        }

        public Task<bool> GetLockTask(IEnumerable<string> assets, OperationMode mode = OperationMode.Normal)
        {
            assets = new List<string>(assets);
            return StartTask(() => GetLock(assets, mode));
        }

        public Task<bool> RevertTask(IEnumerable<string> assets)
        {
            assets = new List<string>(assets);
            FlushFiles();
            return StartTask(() => Revert(assets));
        }
        #endregion

        #region IVersionControlCommands

        public bool IsReady()
        {
            return VCSettings.VCEnabled && vcc.IsReady();
        }
        public bool HasValidLocalCopy()
        {
            return vcc.HasValidLocalCopy();
        }
        public void SetWorkingDirectory(string workingDirectory)
        {
            vcc.SetWorkingDirectory(workingDirectory);
        }
        public bool SetUserCredentials(string userName, string password, bool cacheCredentials)
        {
            return vcc.SetUserCredentials(userName, password, cacheCredentials);
        }
        public VersionControlStatus GetAssetStatus(string assetPath)
        {
            return vcc.GetAssetStatus(new ComposedString(assetPath));
        }
        public VersionControlStatus GetAssetStatus(ComposedString assetPath)
        {
            return vcc.GetAssetStatus(assetPath);
        }
        public IEnumerable<VersionControlStatus> GetFilteredAssets(Func<VersionControlStatus, bool> filter)
        {
            using (PushStateUtility.Profiler("GetFilteredAssets"))
            {
                return vcc.GetFilteredAssets(filter);
            }
        }
        public bool Status(StatusLevel statusLevel, DetailLevel detailLevel)
        {
            return HandleExceptions(() => vcc.Status(statusLevel, detailLevel));
        }

        public bool Status(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            return HandleExceptions(() =>
            {
                bool result = vcc.Status(assets, statusLevel);
                return result;
            });
        }
        public bool RequestStatus(string asset, StatusLevel statusLevel)
        {
            return RequestStatus(new[] { asset }, statusLevel);
        }
        public bool RequestStatus(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            if (ignoreStatusRequests) return false;
            return vcc.RequestStatus(assets, statusLevel);
        }

        public bool Update(IEnumerable<string> assets = null)
        {
            updating = true;
            bool updateResult = vcc.Update(assets);
            updating = false;
            if (updateResult)
            {
                RequestAssetDatabaseRefresh();
                Status(StatusLevel.Local, DetailLevel.Normal);
            }
            if (updateResult) OnOperationCompleted(OperationType.Update);
            return updateResult;
        }

        public bool Commit(IEnumerable<string> assets, string commitMessage = "")
        {
            return HandleExceptions(() =>
            {
                FlushFiles();
                Status(assets, StatusLevel.Local);
                bool commitSuccess = vcc.Commit(assets, commitMessage);
                RequestAssetDatabaseRefresh();
                if (commitSuccess) OnOperationCompleted(OperationType.Commit);
                return commitSuccess;
            });
        }
        public bool Add(IEnumerable<string> assets)
        {
            return HandleExceptions(() => vcc.Add(assets));
        }
        public bool Revert(IEnumerable<string> assets)
        {
            return HandleExceptions(() =>
            {
                FlushFiles();
                Status(assets.ToList(), StatusLevel.Local);
                bool revertSuccess = vcc.Revert(assets);
                RequestAssetDatabaseRefresh();
                if (revertSuccess) OnOperationCompleted(OperationType.Revert);
                return revertSuccess;
            });
        }
        public bool Delete(IEnumerable<string> assets, OperationMode mode = OperationMode.Force)
        {
            return HandleExceptions(() =>
            {
                var deleteAssets = new List<string>();
                foreach (string assetIt in assets)
                {
                    var metaAsset = assetIt + VCCAddMetaFiles.meta;
                    if (GetAssetStatus(assetIt).fileStatus != VCFileStatus.Unversioned)
                    {
                        deleteAssets.Add(metaAsset);
                        deleteAssets.Add(assetIt);
                    }
                    else
                    {
                        if (File.Exists(metaAsset))
                        {
                            File.SetAttributes(metaAsset, FileAttributes.Normal);
                            File.Delete(metaAsset);
                        }
                        if (File.Exists(assetIt))
                        {
                            File.SetAttributes(assetIt, FileAttributes.Normal);
                            File.Delete(assetIt);
                        }
                        if (Directory.Exists(assetIt))
                        {
                            foreach (var subDirFile in Directory.GetFiles(assetIt, "*", SearchOption.AllDirectories))
                            {
                                File.SetAttributes(subDirFile, FileAttributes.Normal);
                                File.Delete(subDirFile);
                            }
                            Directory.Delete(assetIt, true);
                        }
                    }
                }
                bool result = vcc.Delete(deleteAssets, mode);
                RequestAssetDatabaseRefresh();
                if (result) OnOperationCompleted(OperationType.Delete);
                return result;
            });
        }
        public bool GetLock(IEnumerable<string> assets, OperationMode mode = OperationMode.Normal)
        {
            return HandleExceptions(() =>
            {
                bool getlockSuccess = vcc.GetLock(assets, mode);
                if (getlockSuccess) OnOperationCompleted(OperationType.GetLock);
                return getlockSuccess;
            });
        }
        public bool ReleaseLock(IEnumerable<string> assets)
        {
            return HandleExceptions(() =>
            {
                bool result = vcc.ReleaseLock(assets);
                if (result) OnOperationCompleted(OperationType.ReleaseLock);
                return result;
            });
        }
        public bool ChangeListAdd(IEnumerable<string> assets, string changelist)
        {
            return HandleExceptions(() => vcc.ChangeListAdd(assets, changelist));
        }
        public bool ChangeListRemove(IEnumerable<string> assets)
        {
            return HandleExceptions(() => vcc.ChangeListRemove(assets));
        }
        public bool Checkout(string url, string path = "")
        {
            return HandleExceptions(() => vcc.Checkout(url, path));
        }
        public bool Resolve(IEnumerable<string> assets, ConflictResolution conflictResolution)
        {
            return HandleExceptions(() =>
            {
                bool resolveSuccess = vcc.Resolve(assets, conflictResolution);
                RequestAssetDatabaseRefresh();
                if (resolveSuccess) OnOperationCompleted(OperationType.Resolve);
                return resolveSuccess;
            });
        }
        public bool Move(string from, string to)
        {
            return HandleExceptions(() =>
            {
                FlushFiles();
                bool moveSuccess = vcc.Move(from, to);
                RequestAssetDatabaseRefresh();
                if (moveSuccess) OnOperationCompleted(OperationType.Move);
                return moveSuccess;
            });
        }
        public string GetBasePath(string assetPath)
        {
            return vcc.GetBasePath(assetPath);
        }
        public bool CleanUp()
        {
            return HandleExceptions(() =>
            {
                bool result = vcc.CleanUp();
                if (result) OnOperationCompleted(OperationType.CleanUp);
                return result;
            });
        }
        public void ClearDatabase()
        {
            vcc.ClearDatabase();
            OnStatusCompleted();
        }
        public void RemoveFromDatabase(IEnumerable<string> assets)
        {
            vcc.RemoveFromDatabase(assets);
            OnStatusCompleted();
        }

        private void OnStatusCompleted()
        {
            //OnNextUpdate.Do(() => D.Log("Status Updatees : " + (StatusCompleted != null ? StatusCompleted.GetInvocationList().Length : 0) + "\n" + StatusCompleted.GetInvocationList().Select(i => (i.Target ?? "") + ":" + i.Method.ToString()).Aggregate((a, b) => a + "\n" + b)));
            if (StatusCompleted != null) OnNextUpdate.Do(StatusCompleted);
            RefreshAssetDatabaseAfterStatusUpdate();
        }

        private void OnOperationCompleted(OperationType operation)
        {
            if (OperationCompleted != null)
            {
                if (ThreadUtility.IsMainThread())
                    OperationCompleted(operation);
                else
                    OnNextUpdate.Do(() => OperationCompleted(operation));
            }
        }

        public bool CommitDialog(IEnumerable<string> assets, bool showUserConfirmation = false, string commitMessage = "")
        {
            int initialAssetCount = assets.Count();
            if (initialAssetCount == 0) return true;

            assets = AssetpathsFilters.AddFilesInFolders(assets);
            assets = AssetpathsFilters.AddFolders(assets);
            var dependencies = AssetpathsFilters.GetDependencies(assets);
            dependencies = AssetpathsFilters.AddFilesInFolders(dependencies);
            dependencies = AssetpathsFilters.AddFolders(dependencies);
            dependencies = dependencies.Concat(AssetpathsFilters.AddDeletedInFolders(assets));
            var allAssets = assets.Concat(dependencies).Distinct().ToList();
            var localModified = AssetpathsFilters.LocalModified(allAssets);
            if (assets.Contains(EditorApplication.currentScene))
            {
                EditorApplication.SaveCurrentSceneIfUserWantsTo();
            }
            if (PreCommit != null)
            {
                PreCommit(allAssets);
            }
            if (localModified.Any())
            {
                string title = string.Format("{0} '{1}' files?", Terminology.getlock, Terminology.localModified);
                string message = string.Format("You are trying to commit files which are '{0}'.\nDo you want to '{1}' these files first?", Terminology.localModified, Terminology.getlock);
                if (EditorUtility.DisplayDialog(title, message, Terminology.getlock, "Abort"))
                {
                    GetLock(localModified);
                }
                else
                {
                    return false;
                }
            }
            if (showUserConfirmation || initialAssetCount < (assets.Count() + dependencies.Count()))
            {
                return OpenCommitDialogWindow(assets, dependencies);
            }
            return Commit(assets, commitMessage);
        }

        public bool AllowLocalEdit(IEnumerable<string> assets)
        {
            return vcc.AllowLocalEdit(assets);
        }

        #endregion
    }
}
