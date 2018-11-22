// Copyright (c) <2018>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;

namespace UVC
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
        Add,
        Commit,
        GetLock,
        ReleaseLock,
        Delete,
        Move,
        CleanUp,
        Resolve,
        AllowLocalEdit,
        Checkout,
        CreateBranch,
        MergeBranch,
        SwitchBranch
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
        private bool pauseAssetDatabaseRefresh = false;
        static VCCommands instance;
        public static void Initialize() { if (instance == null) { instance = new VCCommands(); } }
        public static VCCommands Instance { get { Initialize(); return instance; } }
        private static System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
        static readonly VersionControlStatus[] emptyVersionControlStatusArray = new VersionControlStatus[0];

        private IVersionControlCommands vcc;        
        private Action refreshAssetDatabaseSynchronous = () => AssetDatabase.Refresh();
        private string customTrunkPath = null;

        public bool FlusingFiles { get; private set; }
        public event Action<string> ProgressInformation;
        public event Action StatusCompleted;
        public event Action<OperationType, VersionControlStatus[]> OperationStarting;
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
            vcc?.Dispose();
            vcc = newVcc;
            vcc.ProgressInformation += progress =>
            {
                if (ProgressInformation != null)
                    OnNextUpdate.Do(() => ProgressInformation(progress));
            };
            vcc.StatusCompleted += OnStatusCompleted;
            OnNextUpdate.Do(Start);
            EditorApplication.playModeStateChanged += OnPlaymodeStateChanged;
        }

        public static bool Active
        {
            get
            {
                bool playmode = ThreadUtility.IsMainThread() && EditorApplication.isPlayingOrWillChangePlaymode;
                return VCSettings.VCEnabled && !playmode;
            }
        }

        public bool Ready => Active && vcc.IsReady() && !EditorApplication.isCompiling;

        public void Dispose()
        {            
            vcc.Dispose();
        }

        public void StartInPassiveMode()
        {
            if (Active)
            {
                vcc.Start();
                Started?.Invoke();
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
                Started?.Invoke();
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
        
        public void PauseAssetDatabaseRefresh()
        {
            pauseAssetDatabaseRefresh = true;
            DisableAutoRefresh();
        }

        public void ResumeAssetDatabaseRefresh()
        {
            EnableAutoRefresh();
            pauseAssetDatabaseRefresh = false;
            RefreshAssetDatabase();
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
                    DebugLog.LogError("Unhandled exception: " + e.Message + "\n" + DebugLog.GetCallstack());
                    throw;
                }
                finally
                {
                    DebugLog.Log(new System.Diagnostics.StackTrace(1, true).GetFrames()[0].GetMethod().Name + " took " + stopWatch.ElapsedMilliseconds + "ms");
                }
            }
            else
            {
                DebugLog.Log("VC Action ignored due to not being active");
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
            OnOperationStarting(operationType, beforeStatus);
            bool result = operation(assets);
            var afterStatus = StoreCurrentStatus(assets);
            OnOperationCompleted(operationType, beforeStatus, afterStatus, result);
            return result;
        }

        private bool PerformOperation(OperationType operationType, Func<bool> operation)
        {
            OnOperationStarting(operationType, emptyVersionControlStatusArray);
            bool result = operation();
            OnOperationCompleted(operationType, emptyVersionControlStatusArray, emptyVersionControlStatusArray, result);
            return result;
        }

        private void OnPlaymodeStateChanged(PlayModeStateChange playModeStateChange)
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

        private void FlushFiles()
        {
            if (ThreadUtility.IsMainThread() && !pauseAssetDatabaseRefresh)
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
                DebugLog.Log("Resetting kAutoRefreshDisableCount");
            }
            if(EditorPrefs.GetInt("kAutoRefreshDisableCount", 0) < 0)
            {
                EditorPrefs.SetInt("kAutoRefreshDisableCount", 0);
                EditorPrefs.SetBool("kAutoRefresh", true);
                DebugLog.Log("Resetting kAutoRefreshDisableCount");
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

        private async Task<T> StartTask<T>(Func<T> work)
        {
            var task = Active ? new Task<T>(work) : new Task<T>(() => default(T));
            task.Start(); // Sync: task.RunSynchronously();
            return await task;
        }

        public async Task<bool> StatusTask(StatusLevel statusLevel, DetailLevel detailLevel)
        {
            return await StartTask(() => Status(statusLevel, detailLevel));
        }

        public async Task<bool> StatusTask(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            assets = new List<string>(assets);
            return await StartTask(() => Status(assets, statusLevel));
        }

        public async Task<bool> UpdateTask(IEnumerable<string> assets = null)
        {
            if (assets != null) assets = new List<string>(assets);
            OnOperationStarting(OperationType.Update, StoreCurrentStatus(assets));
            DisableAutoRefresh();
            var updateTask = StartTask(() => Update(assets));
            await updateTask.ContinueWithOnNextUpdate(t => EnableAutoRefresh());
            return await updateTask;
        }

        public async Task<bool> AddTask(IEnumerable<string> assets)
        {
            assets = new List<string>(assets);
            OnOperationStarting(OperationType.Add, StoreCurrentStatus(assets));
            return await StartTask(() => Add(assets));
        }

        public async Task<bool> CommitTask(IEnumerable<string> assets, string commitMessage = "")
        {
            assets = new List<string>(assets);
            FlushFiles();
            OnOperationStarting(OperationType.Commit, StoreCurrentStatus(assets));
            return await StartTask(() => Commit(assets, commitMessage));
        }

        public async Task<bool> GetLockTask(IEnumerable<string> assets, OperationMode mode = OperationMode.Normal)
        {
            assets = new List<string>(assets);
            OnOperationStarting(OperationType.GetLock, StoreCurrentStatus(assets));
            return await StartTask(() => GetLock(assets, mode));
        }

        public async Task<bool> RevertTask(IEnumerable<string> assets)
        {
            assets = new List<string>(assets);
            FlushFiles();
            OnOperationStarting(OperationType.Revert, StoreCurrentStatus(assets));
            return await StartTask(() => Revert(assets));
        }

        public async Task<bool> CreateBranchTask(string from, string to)
        {
            OnOperationStarting(OperationType.CreateBranch, null);
            return await StartTask(() => CreateBranch(from, to));
        }
        
        public async Task<bool> MergeBranchTask(string url, string path = "")
        {
            OnOperationStarting(OperationType.MergeBranch, null);
            return await StartTask(() => MergeBranch(url, path));
        }
        
        public async Task<bool> SwitchBranchTask(string url, string path = "")
        {
            OnOperationStarting(OperationType.SwitchBranch, null);
            return await StartTask(() => SwitchBranch(url, path));
        }
        
        public async Task<string> GetCurrentBranchTask()
        {
            return await StartTask(GetCurrentBranch);
        }
        
        public async Task<string> GetBranchDefaultPathTask()
        {
            return await StartTask(GetBranchDefaultPath);
        }
        
        public async Task<string> GetTrunkPathTask()
        {
            return await StartTask(GetTrunkPath);
        }
        
        public async Task<List<BranchStatus>> RemoteListTask(string path)
        {
            return await StartTask(() => RemoteList(path));
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
        public InfoStatus GetInfo(string path)
        {
            return vcc.GetInfo(path);
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
            OnOperationStarting(OperationType.Update, beforeStatus);
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
                OnOperationStarting(OperationType.Commit, beforeStatus);
                bool commitSuccess = vcc.Commit(assets, commitMessage);
                Status(assets, StatusLevel.Local);
                var afterStatus = StoreCurrentStatus(assets);
                OnOperationCompleted(OperationType.Commit, beforeStatus, afterStatus, commitSuccess);
                return commitSuccess;
            });
        }
        public bool Commit(string commitMessage = "")
        {
            return HandleExceptions(() => PerformOperation(OperationType.Commit, () => vcc.Commit(commitMessage)));
        }
        public bool Add(IEnumerable<string> assets)
        {
            return HandleExceptions(() => PerformOperation(OperationType.Add, assets, vcc.Add));
        }
        public bool Revert(IEnumerable<string> assets)
        {
            return HandleExceptions(() =>
            {
                FlushFiles();
                //assets = assets.Concat(assets.Select(vcc.GetAssetStatus).Where(status => !ComposedString.IsNullOrEmpty(status.movedFrom)).Select(status => status.movedFrom.Compose()) ).ToArray();
                Status(assets, StatusLevel.Local);
                var beforeStatus = StoreCurrentStatus(assets);
                OnOperationStarting(OperationType.Revert, beforeStatus);
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
                OnOperationStarting(OperationType.Delete, beforeStatus);
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
            return HandleExceptions(() => PerformOperation(OperationType.Checkout, () => vcc.Checkout(url, path)));
        }        
        public bool CreateBranch(string from, string to)
        {
            return HandleExceptions(() => PerformOperation(OperationType.CreateBranch, () => vcc.CreateBranch(from, to)));
        }        
        public bool MergeBranch(string url, string path = "")
        {
            return HandleExceptions(() => PerformOperation(OperationType.MergeBranch, () => vcc.MergeBranch(url, path)));
        }
        public bool SwitchBranch(string url, string path = "")
        {
            return HandleExceptions(() =>
            {
                var result = PerformOperation(OperationType.SwitchBranch, () => vcc.SwitchBranch(url, path));
                RefreshAssetDatabase();
                return result;
            });
        }
        public string GetCurrentBranch()
        {
            return vcc.GetCurrentBranch();
        }
        public string GetBranchDefaultPath()
        {
            return vcc.GetBranchDefaultPath();
        }
        public string GetTrunkPath()
        {
            return customTrunkPath ?? vcc.GetTrunkPath();
        }
        public void SetCustomTrunkPath(string path)
        {
            customTrunkPath = path;
        }
        public List<BranchStatus> RemoteList(string path)
        {
            return HandleExceptions(() => vcc.RemoteList(path));
        }
        public bool Resolve(IEnumerable<string> assets, ConflictResolution conflictResolution)
        {
            return HandleExceptions(() =>
            {
                var beforeStatus = StoreCurrentStatus(assets);
                OnOperationStarting(OperationType.Resolve, beforeStatus);
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
                OnOperationStarting(OperationType.Move, beforeStatus);
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

        public int GetRevision()
        {
            return vcc.GetRevision();
        }

        public string GetBasePath(string assetPath)
        {
            return vcc.GetBasePath(assetPath);
        }

        public bool GetConflict(string assetPath, out string basePath, out string yours, out string theirs)
        {
            return vcc.GetConflict(assetPath, out basePath, out yours, out theirs);
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

        private void OnOperationStarting(OperationType operation, VersionControlStatus[] statuses)
        {
            if (OperationStarting != null && ThreadUtility.IsMainThread())
            {
                OperationStarting(operation, statuses);
            }
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

        public bool CommitDialog(IEnumerable<string> assets, bool includeDependencies = true, bool showUserConfirmation = false, string commitMessage = "")
        {
            int initialAssetCount = assets.Count();
            if (initialAssetCount == 0) return true;

            assets = assets.AddFilesInFolders().AddFolders(vcc).AddMoveMatches(vcc);
            var dependencies = includeDependencies ? assets.GetDependencies().AddFilesInFolders().AddFolders(vcc).Concat(assets.AddDeletedInFolders(vcc)) : new string[0];
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
                string title = $"{Terminology.getlock} '{Terminology.localModified}' files?";
                string message = $"You are trying to commit files which are '{Terminology.localModified}'.\nDo you want to '{Terminology.getlock}' these files first?";
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
