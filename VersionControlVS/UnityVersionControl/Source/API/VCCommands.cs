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
    using AssetPathFilters;
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;

    [InitializeOnLoad]
    public class VCCommandsOnLoad
    {
        static VCCommandsOnLoad() 
        {
            OnNextUpdate.Do(VCCommands.Initialize); 
        }
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
        AllowLocalEdit,
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
        private static System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();

        private IVersionControlCommands vcc;        
        private Action refreshAssetDatabaseSynchronous = () => AssetDatabase.Refresh();

        public bool FlusingFiles { get; private set; }
        public event Action<string> ProgressInformation;
        public event Action StatusCompleted;
        public event Action<OperationType, VersionControlStatus[], VersionControlStatus[], bool> OperationCompleted;
        public event Action<List<string>> PreCommit;
        public event Action Started;

        private VCCommands()
        {
            VersionControlFactory.VersionControlBackendChanged += OnVersionControlBackendChanged;
            if (!VersionControlFactory.CreateVersionControlCommands(VCSettings.VersionControlBackend))
            {
                VCSettings.VersionControlBackend = VCSettings.EVersionControlBackend.None;
            }
            VerifyAutoRefresh();
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

        public bool Ready { 
            get 
            {
                return Active && vcc.IsReady() && !EditorApplication.isCompiling; 
            } 
        }

        public void Dispose()
        {            
            vcc.Dispose();
        }

        public void StartInPassiveMode()
        {
            if (Active)
            {
                vcc.Start();
                if (Started != null) Started();
            }
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
            EditorUtility.ClearProgressBar();
        }

        public void ActivateRefreshLoop()
        {
            vcc.ActivateRefreshLoop();
        }

        public void DeactivateRefreshLoop()
        {
            vcc.DeactivateRefreshLoop();
        }

        public void SetImportAssetDatabaseSynchronousCallback(Action refreshSynchronous)
        {
            this.refreshAssetDatabaseSynchronous = refreshSynchronous;
        }

        #region Private methods

        private static T HandleExceptions<T>(Func<T> func)
        {
            if (Active)
            {
                stopWatch.Reset();
                stopWatch.Start();
                try
                {
                    return func();
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
                finally
                {
                    D.Log(new System.Diagnostics.StackTrace(1, true).GetFrames()[0].GetMethod().Name + " took " + stopWatch.ElapsedMilliseconds + "ms");
                }
            }
            else
            {
                D.Log("VC Action ignored due to not being active");
            }
            return default(T);
        }

        private VersionControlStatus[] StoreCurrentStatus(IEnumerable<string> assets)
        {
            return assets != null ? assets.Select(a => GetAssetStatus(a).Clone()).ToArray() : new VersionControlStatus[] { };
        }

        private bool PerformOperation(OperationType operationType, IEnumerable<string> assets, Func<IEnumerable<string>, bool> operation)
        {
            var beforeStatus = StoreCurrentStatus(assets);
            bool result = operation(assets);
            var afterStatus = StoreCurrentStatus(assets);
            OnOperationCompleted(operationType, beforeStatus, afterStatus, result);
            return result;
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

        private void RefreshAssetDatabase()
        {
            if (pendingAssetDatabaseRefresh)
            {
                pendingAssetDatabaseRefresh = false;
                OnNextUpdate.Do(() =>
                {
                    VCConflictHandler.HandleConflicts();
                    refreshAssetDatabaseSynchronous();
                });
            }
        }

        private void FlushFiles()
        {
            if (ThreadUtility.IsMainThread())
            {
                FlusingFiles = true;
                //D.Log("Flusing files");
                AssetDatabase.SaveAssets();
                FlusingFiles = false;
            }
            //else Debug.Log("Ignoring 'FlushFiles' due to Execution context");
        }

        private void VerifyAutoRefresh()
        {
            EnableAutoRefresh();
            if (EditorPrefs.GetInt("kAutoRefreshDisableCount", 0) > 0 && EditorPrefs.GetBool("kAutoRefresh", false))
            {
                EditorPrefs.SetInt("kAutoRefreshDisableCount", 0);
                D.Log("Resetting kAutoRefreshDisableCount");
            }
            if(EditorPrefs.GetInt("kAutoRefreshDisableCount", 0) < 0)
            {
                EditorPrefs.SetInt("kAutoRefreshDisableCount", 0);
                EditorPrefs.SetBool("kAutoRefresh", true);
                D.Log("Resetting kAutoRefreshDisableCount");
            }
        }

        private void DisableAutoRefresh()
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

        private void EnableAutoRefresh()
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

        private static bool OpenCommitDialogWindow(IEnumerable<string> assets, IEnumerable<string> dependencies)
        {
            var commitWindow = ScriptableObject.CreateInstance<VCCommitWindow>();
            commitWindow.minSize = new Vector2(220, 140);
            commitWindow.titleContent = new GUIContent("Commit...");
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
            DisableAutoRefresh();
            var updateTask = StartTask(() => Update(assets));
            updateTask.ContinueWithOnNextUpdate(t => EnableAutoRefresh());
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
            if (FlusingFiles) return false;
            return vcc.RequestStatus(assets, statusLevel);
        }

        public bool Update(IEnumerable<string> assets = null)
        {
            var beforeStatus = StoreCurrentStatus(assets);
            updating = true;
            bool updateResult = vcc.Update(assets);
            updating = false;
            if (updateResult) RequestAssetDatabaseRefresh();
            var afterStatus = StoreCurrentStatus(assets);
            OnOperationCompleted(OperationType.Update, beforeStatus, afterStatus, updateResult);
            if (updateResult) Status(StatusLevel.Local, DetailLevel.Normal);
            return updateResult;
        }

        public bool Commit(IEnumerable<string> assets, string commitMessage = "")
        {
            return HandleExceptions(() =>
            {
                FlushFiles();
                Status(assets, StatusLevel.Local);
                var beforeStatus = StoreCurrentStatus(assets);
                bool commitSuccess = vcc.Commit(assets, commitMessage);
                Status(assets, StatusLevel.Local);
                var afterStatus = StoreCurrentStatus(assets); ;
                OnOperationCompleted(OperationType.Commit, beforeStatus, afterStatus, commitSuccess);
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
                Status(assets, StatusLevel.Local);
                var beforeStatus = StoreCurrentStatus(assets);
                bool revertSuccess = vcc.Revert(assets);
                Status(assets, StatusLevel.Local);
                RequestAssetDatabaseRefresh();
                if (revertSuccess)
                {
                    RefreshAssetDatabase();                    
                }
                var afterStatus = StoreCurrentStatus(assets);
                OnOperationCompleted(OperationType.Revert, beforeStatus, afterStatus, revertSuccess);
                return revertSuccess;
            });
        }
        public bool Delete(IEnumerable<string> assets, OperationMode mode = OperationMode.Force)
        {
            return HandleExceptions(() =>
            {
                var beforeStatus = StoreCurrentStatus(assets);
                bool filesOSDeleted = false;
                var deleteAssets = new List<string>();
                foreach (string assetIt in assets)
                {
                    var metaAsset = assetIt + VCCAddMetaFiles.metaStr;
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
                            filesOSDeleted = true;
                        }
                        if (File.Exists(assetIt))
                        {
                            File.SetAttributes(assetIt, FileAttributes.Normal);
                            File.Delete(assetIt);
                            filesOSDeleted = true;
                        }
                        if (Directory.Exists(assetIt))
                        {
                            foreach (var subDirFile in Directory.GetFiles(assetIt, "*", SearchOption.AllDirectories))
                            {
                                File.SetAttributes(subDirFile, FileAttributes.Normal);
                                File.Delete(subDirFile);                                
                            }
                            Directory.Delete(assetIt, true);
                            filesOSDeleted = true;
                        }
                    }
                }
                bool result = vcc.Delete(deleteAssets, mode);
                RequestAssetDatabaseRefresh();
                if (filesOSDeleted) RefreshAssetDatabase();
                var afterStatus = StoreCurrentStatus(assets);
                OnOperationCompleted(OperationType.Delete, beforeStatus, afterStatus, result || filesOSDeleted);
                return result;
            });
        }
        public bool GetLock(IEnumerable<string> assets, OperationMode mode = OperationMode.Normal)
        {
            return HandleExceptions(() => PerformOperation(OperationType.GetLock, assets, _assets => vcc.GetLock(_assets,mode)));
        }
        public bool ReleaseLock(IEnumerable<string> assets)
        {
            return HandleExceptions(() => PerformOperation(OperationType.ReleaseLock, assets, vcc.ReleaseLock));
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
                var beforeStatus = StoreCurrentStatus(assets);
                bool resolveSuccess = vcc.Resolve(assets, conflictResolution);
                var afterStatus = StoreCurrentStatus(assets);
                RequestAssetDatabaseRefresh();
                OnOperationCompleted(OperationType.Resolve, beforeStatus, afterStatus, resolveSuccess);
                return resolveSuccess;
            });
        }
        public bool Move(string from, string to)
        {
            return HandleExceptions(() =>
            {
                FlushFiles();
                var beforeStatus = new[] {GetAssetStatus(from)};
                bool moveSuccess = vcc.Move(from, to);
                RequestAssetDatabaseRefresh();
                OnOperationCompleted(OperationType.Move, beforeStatus, new[] { GetAssetStatus(to) }, moveSuccess);
                return moveSuccess;
            });
        }

        public bool SetIgnore(string path, IEnumerable<string> assets)
        {
            return HandleExceptions(() => vcc.SetIgnore(path, assets));
        }

        public IEnumerable<string> GetIgnore(string path)
        {
            return HandleExceptions(() => vcc.GetIgnore(path));
        }

        public string GetRevision()
        {
            return vcc.GetRevision();
        }

        public string GetBasePath(string assetPath)
        {
            return vcc.GetBasePath(assetPath);
        }

        public bool GetConflict(string assetPath, out string basePath, out string mine, out string theirs)
        {
            return vcc.GetConflict(assetPath, out basePath, out mine, out theirs);
        }
        public bool CleanUp()
        {
            return HandleExceptions(() =>
            {
                bool result = vcc.CleanUp();
                OnOperationCompleted(OperationType.CleanUp, null, null, result);
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
            RefreshAssetDatabase();
        }

        private void OnOperationCompleted(OperationType operation, VersionControlStatus[] statusBefore, VersionControlStatus[] statusAfter, bool success)
        {
            if (OperationCompleted != null)
            {
                ThreadUtility.ExecuteOnMainThread(() => 
                {
                    //D.Log(operation + " : " + (success ? "success":"failed"));
                    OperationCompleted(operation, statusBefore, statusAfter, success);
                });
            }
        }

        public bool CommitDialog(IEnumerable<string> assets, bool showUserConfirmation = false, string commitMessage = "")
        {
            int initialAssetCount = assets.Count();
            if (initialAssetCount == 0) return true;

            assets = assets.AddFilesInFolders().AddFolders(vcc).AddMoveMatches(vcc);
            var dependencies = assets.GetDependencies().AddFilesInFolders().AddFolders(vcc).Concat(assets.AddDeletedInFolders(vcc));
            var allAssets = assets.Concat(dependencies).Distinct().ToList();
            var localModified = allAssets.LocalModified(vcc);
            if (assets.Contains(SceneManagerUtilities.GetCurrentScenePath()))
            {
                SceneManagerUtilities.SaveCurrentModifiedScenesIfUserWantsTo();                
            }
            if (PreCommit != null)
            {
                PreCommit(allAssets);
            }
            if (VCSettings.RequireLockBeforeCommit && localModified.Any())
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
            return PerformOperation(OperationType.AllowLocalEdit, assets, vcc.AllowLocalEdit);
        }

        #endregion
    }
}
