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
        Status,
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
        SetLocalOnly,
        Checkout,
        ChangeListAdd,
        ChangeListRemove,
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
        static VCCommands instance;
        public static void Initialize() { if (instance == null) { instance = new VCCommands(); } }
        public static VCCommands Instance { get { Initialize(); return instance; } }
        private static System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
        static readonly VersionControlStatus[] emptyVersionControlStatusArray = new VersionControlStatus[0];

        private IVersionControlCommands vcc;
        private string customTrunkPath = null;

        public bool FlusingFiles { get; private set; }
        public event Action<string> ProgressInformation;
        public event Action StatusCompleted;
        public event Func<OperationType, VersionControlStatus[], bool> OperationStarting;
        public event Action<OperationType, VersionControlStatus[], VersionControlStatus[], bool> OperationCompleted;
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



        #region Private methods
        private T HandleExceptions<T>(Func<T> func, Action onException = null)
        {
            string callerMethod = new System.Diagnostics.StackTrace(1, true).GetFrames()?[0]?.GetMethod().Name ?? "UVC Operation (unknown)";
            using (ProfilerScope.Get(callerMethod))
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
                        onException?.Invoke();
                    }
                    catch (Exception e)
                    {
                        DebugLog.LogError("Unhandled exception: " + e.Message + "\n" + DebugLog.GetCallstack());
                        onException?.Invoke();
                        throw;
                    }
                    finally
                    {
                        DebugLog.Log(callerMethod + " took " + stopWatch.ElapsedMilliseconds + "ms");
                    }
                }
                else
                {
                    DebugLog.Log("VC Action ignored due to not being active");
                }
                return default(T);
            }
        }

        private VersionControlStatus[] StoreCurrentStatus(IEnumerable<string> assets)
        {
            return assets != null ? assets.Select(a => GetAssetStatus(a).Clone()).ToArray() : new VersionControlStatus[] { };
        }

        private bool PerformOperation(OperationType operationType, IEnumerable<string> assets, Func<IEnumerable<string>, bool> operation)
        {
            var beforeStatus = StoreCurrentStatus(assets);
            if (!OnOperationStarting(operationType, beforeStatus))
                return false;
            bool result = operation(assets);
            var afterStatus = StoreCurrentStatus(assets);
            OnOperationCompleted(operationType, beforeStatus, afterStatus, result);
            return result;
        }

        private bool PerformOperation(OperationType operationType, Func<bool> operation)
        {
            if (!OnOperationStarting(operationType, emptyVersionControlStatusArray))
                return false;
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
                if (!Application.isPlaying)
                {
                    FlushFiles();
                    Start();
                }
                else Stop();
            }
        }

        public void FlushFiles()
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

        private Task<T> StartTask<T>(Func<T> work)
        {
            var task = Active ? new Task<T>(work) : new Task<T>(() => default(T));
            task.Start(); // Sync: task.RunSynchronously();
            return task;
        }

        public Task<bool> StatusTask(StatusLevel statusLevel, DetailLevel detailLevel)
        {
            return StartTask(() => Status(statusLevel, detailLevel));
        }

        public Task<bool> StatusTask(IEnumerable<string> assets, StatusLevel statusLevel)
        {
            assets = assets.ToArray();
            return StartTask(() => Status(assets, statusLevel));
        }

        public Task<bool> UpdateTask(IEnumerable<string> assets = null)
        {
            if (assets != null) assets = assets.ToArray();
            if (OnOperationStarting(OperationType.Update, StoreCurrentStatus(assets)))
            {
                return StartTask(() => Update(assets));
            }
            return Task.Run(() => false);
        }

        public Task<bool> AddTask(IEnumerable<string> assets)
        {
            assets = assets.ToArray();
            return OnOperationStarting(OperationType.Add, StoreCurrentStatus(assets))
                ? StartTask(() => Add(assets))
                : Task.Run(() => false);
        }

        public Task<bool> CommitTask(IEnumerable<string> assets, string commitMessage = "")
        {
            assets = assets.ToArray();
            FlushFiles();
            return OnOperationStarting(OperationType.Commit, StoreCurrentStatus(assets))
                ? StartTask(() => Commit(assets, commitMessage))
                : Task.Run(() => false);
        }

        public Task<bool> GetLockTask(IEnumerable<string> assets, OperationMode mode = OperationMode.Normal)
        {
            assets = assets.ToArray();
            return OnOperationStarting(OperationType.GetLock, StoreCurrentStatus(assets))
                ? StartTask(() => GetLock(assets, mode))
                : Task.Run(() => false);
        }
        
        public Task<bool> ReleaseLockTask(IEnumerable<string> assets)
        {
            assets = assets.ToArray();
            return OnOperationStarting(OperationType.ReleaseLock, StoreCurrentStatus(assets))
                ? StartTask(() => ReleaseLock(assets))
                : Task.Run(() => false);
        }

        public Task<bool> RevertTask(IEnumerable<string> assets)
        {
            assets = assets.ToArray();
            FlushFiles();
            return OnOperationStarting(OperationType.Revert, StoreCurrentStatus(assets)) ? StartTask(() => Revert(assets)) : Task.Run(() => false);
        }

        public Task<bool> ChangeListAddTask(IEnumerable<string> assets, string changeList)
        {
            assets = assets.ToArray();
            return OnOperationStarting(OperationType.ChangeListAdd, StoreCurrentStatus(assets))
                ? StartTask(() => ChangeListAdd(assets, changeList))
                : Task.Run(() => false);
        }

        public Task<bool> ChangeListRemoveTask(IEnumerable<string> assets)
        {
            assets = assets.ToArray();
            return OnOperationStarting(OperationType.ChangeListRemove, StoreCurrentStatus(assets))
                ? StartTask(() => ChangeListRemove(assets))
                : Task.Run(() => false);
        }

        public Task<bool> AllowLocalEditTask(IEnumerable<string> assets)
        {
            assets = assets.ToArray();
            return OnOperationStarting(OperationType.AllowLocalEdit, StoreCurrentStatus(assets))
                ? StartTask(() => AllowLocalEdit(assets))
                : Task.Run(() => false);
        }

        public Task<bool> SetLocalOnlyTask(IEnumerable<string> assets)
        {
            assets = assets.ToArray();
            return OnOperationStarting(OperationType.SetLocalOnly, StoreCurrentStatus(assets))
                ? StartTask(() => SetLocalOnly(assets))
                : Task.Run(() => false);
        }

        public Task<bool> CreateBranchTask(string from, string to)
        {
            return OnOperationStarting(OperationType.CreateBranch, null)
                ? StartTask(() => CreateBranch(@from, to))
                : Task.Run(() => false);
        }

        public Task<bool> MergeBranchTask(string url, string path = "")
        {
            return OnOperationStarting(OperationType.MergeBranch, null)
                ? StartTask(() => MergeBranch(url, path))
                : Task.Run(() => false);
        }

        public Task<bool> SwitchBranchTask(string url, string path = "")
        {
            return OnOperationStarting(OperationType.SwitchBranch, null)
                ? StartTask(() => SwitchBranch(url, path))
                : Task.Run(() => false);
        }

        public Task<string> GetCurrentBranchTask()
        {
            return StartTask(GetCurrentBranch);
        }

        public Task<string> GetBranchDefaultPathTask()
        {
            return StartTask(GetBranchDefaultPath);
        }

        public Task<string> GetTrunkPathTask()
        {
            return StartTask(GetTrunkPath);
        }

        public Task<List<BranchStatus>> RemoteListTask(string path)
        {
            return StartTask(() => RemoteList(path));
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
            return HandleExceptions(() => vcc.GetInfo(path));
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
            return HandleExceptions(() => vcc.Status(assets, statusLevel));
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
            return HandleExceptions(() =>
            {
                var beforeStatus = StoreCurrentStatus(assets);
                if (!OnOperationStarting(OperationType.Update, beforeStatus))
                    return false;
                updating = true;
                bool updateResult = vcc.Update(assets);
                updating = false;
                if (updateResult) AssetDatabaseRefreshManager.RequestAssetDatabaseRefresh();
                var afterStatus = StoreCurrentStatus(assets);
                OnOperationCompleted(OperationType.Update, beforeStatus, afterStatus, updateResult);
                if (updateResult) Status(StatusLevel.Local, DetailLevel.Normal);
                return updateResult;
            }, () => OnOperationCompleted(OperationType.Update, null, null, false));
        }

        public bool Update(int revision, IEnumerable<string> assets = null)
        {
            return HandleExceptions(() =>
            {
                var beforeStatus = StoreCurrentStatus(assets);
                if (!OnOperationStarting(OperationType.Update, beforeStatus))
                    return false;
                updating = true;
                bool updateResult = vcc.Update(revision, assets);
                updating = false;
                if (updateResult) AssetDatabaseRefreshManager.RequestAssetDatabaseRefresh();
                var afterStatus = StoreCurrentStatus(assets);
                OnOperationCompleted(OperationType.Update, beforeStatus, afterStatus, updateResult);
                if (updateResult) Status(StatusLevel.Local, DetailLevel.Normal);
                return updateResult;
            }, () => OnOperationCompleted(OperationType.Update, null, null, false));
        }

        public bool Commit(IEnumerable<string> assets, string commitMessage = "")
        {
            return HandleExceptions(() =>
            {
                FlushFiles();
                Status(assets, StatusLevel.Local);
                var beforeStatus = StoreCurrentStatus(assets);
                if (!OnOperationStarting(OperationType.Commit, beforeStatus))
                    return false;
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
                if (!OnOperationStarting(OperationType.Revert, beforeStatus))
                    return false;
                bool revertSuccess = vcc.Revert(assets);
                Status(assets, StatusLevel.Local);
                AssetDatabaseRefreshManager.RequestAssetDatabaseRefresh();
                if (revertSuccess)
                {
                    AssetDatabaseRefreshManager.RefreshAssetDatabase();
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
                if (!OnOperationStarting(OperationType.Delete, beforeStatus))
                    return false;
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
                AssetDatabaseRefreshManager.RequestAssetDatabaseRefresh();
                if (filesOSDeleted) AssetDatabaseRefreshManager.RefreshAssetDatabase();
                var afterStatus = StoreCurrentStatus(assets);
                OnOperationCompleted(OperationType.Delete, beforeStatus, afterStatus, result || filesOSDeleted);
                return result;
            });
        }
        public bool GetLock(IEnumerable<string> assets, OperationMode mode = OperationMode.Normal)
        {
            return HandleExceptions(() => PerformOperation(OperationType.GetLock, assets, _assets => vcc.GetLock(_assets, mode)));
        }
        public bool ReleaseLock(IEnumerable<string> assets)
        {
            return HandleExceptions(() => PerformOperation(OperationType.ReleaseLock, assets, vcc.ReleaseLock));
        }
        public bool ChangeListAdd(IEnumerable<string> assets, string changelist)
        {
            return HandleExceptions(() => PerformOperation(OperationType.ChangeListAdd, () => vcc.ChangeListAdd(assets, changelist)));
        }
        public bool ChangeListRemove(IEnumerable<string> assets)
        {
            return HandleExceptions(() => PerformOperation(OperationType.ChangeListRemove, () => vcc.ChangeListRemove(assets)));
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
            return HandleExceptions(() => PerformOperation(OperationType.SwitchBranch, () => vcc.SwitchBranch(url, path)));
        }
        public string GetCurrentBranch()
        {
            return HandleExceptions(() => vcc.GetCurrentBranch());
        }
        public string GetBranchDefaultPath()
        {
            return HandleExceptions(() => vcc.GetBranchDefaultPath());
        }
        public string GetTrunkPath()
        {
            return customTrunkPath ?? HandleExceptions(() => vcc.GetTrunkPath());
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
                if (!OnOperationStarting(OperationType.Resolve, beforeStatus))
                    return false;
                bool resolveSuccess = vcc.Resolve(assets, conflictResolution);
                if (conflictResolution == ConflictResolution.Mine)
                {
                    vcc.SetLocalOnly(assets.Where(asset => !MergeHandler.IsMergableAsset(asset)));
                }
                var afterStatus = StoreCurrentStatus(assets);
                AssetDatabaseRefreshManager.RequestAssetDatabaseRefresh();
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
                if (!OnOperationStarting(OperationType.Move, beforeStatus))
                    return false;
                bool moveSuccess = vcc.Move(from, to);
                AssetDatabaseRefreshManager.RequestAssetDatabaseRefresh();
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
            return HandleExceptions(() => vcc.GetRevision());
        }

        public string GetBasePath(string assetPath)
        {
            return HandleExceptions(() => vcc.GetBasePath(assetPath));
        }

        public bool GetConflict(string assetPath, out string basePath, out string yours, out string theirs)
        {
            string localBasePath = null;
            string localYours = null;
            string localTheirs= null;

            bool result = HandleExceptions(() => vcc.GetConflict(assetPath, out localBasePath, out localYours, out localTheirs));

            basePath = localBasePath;
            yours = localYours;
            theirs = localTheirs;

            return result;
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
            AssetDatabaseRefreshManager.RefreshAssetDatabase();
        }

        private bool OnOperationStarting(OperationType operation, VersionControlStatus[] statuses)
        {
            try
            {
                if (OperationStarting != null && ThreadUtility.IsMainThread())
                {
                    foreach (Func<OperationType, VersionControlStatus[], bool> callback in OperationStarting.GetInvocationList())
                    {
                        if (!callback(operation, statuses))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                DebugLog.ThrowException(e);
                return false;
            }
        }

        private void OnOperationCompleted(OperationType operation, VersionControlStatus[] statusBefore, VersionControlStatus[] statusAfter, bool success)
        {
            if (OperationCompleted != null)
            {
                ThreadUtility.ExecuteOnMainThread(() =>
                {
                    //D.Log(operation + " : " + (success ? "success":"failed"));
                    try
                    {
                        if(statusBefore == null) statusBefore = new VersionControlStatus[0];
                        if(statusAfter == null) statusAfter = new VersionControlStatus[0];
                        OperationCompleted(operation, statusBefore, statusAfter, success);
                    }
                    catch(Exception e)
                    {
                        DebugLog.ThrowException(e);
                    }
                });
            }
        }

        public bool CommitDialog(IEnumerable<string> assets, bool includeDependencies = true, bool showUserConfirmation = false, string commitMessage = "")
        {
            return CommitDialog(assets.ToList(), includeDependencies, showUserConfirmation, commitMessage);
        }
        public bool CommitDialog(List<string> assets, bool includeDependencies = true, bool showUserConfirmation = false, string commitMessage = "")
        {
            int initialAssetCount = assets.Count;
            if (initialAssetCount == 0) return true;

            UnityAssetpathsFilters.AddFilesInFolders(ref assets);
            AssetpathsFilters.AddFolders(ref assets, vcc);
            AssetpathsFilters.AddMoveMatches(ref assets, vcc);

            List<string> dependencies = new List<string>();
            if (includeDependencies)
            {
                dependencies = assets.GetDependencies().ToList();
                UnityAssetpathsFilters.AddFilesInFolders(ref dependencies);
                AssetpathsFilters.AddFolders(ref dependencies, vcc);
                dependencies.AddRange(assets.AddDeletedInFolders(vcc));
            }
            var allAssets = assets.Concat(dependencies).Distinct().ToList();
            var localModified = allAssets.LocalModified(vcc);
            if (assets.Contains(SceneManagerUtilities.GetCurrentScenePath()))
            {
                SceneManagerUtilities.SaveCurrentModifiedScenesIfUserWantsTo();
            }

            var localOnly = allAssets.LocalOnly(vcc);
            if (localOnly.Any())
            {
                return UserDialog.DisplayDialog(
                    title: "Commit Warning!",
                    message: "You have chosen your own content above content from the server, which have made the asset 'Local Only'\n" +
                             "To reduce the risk of removing someones work, you will have to verify you wish to 'commit anyway'\n\n" +
                             $"{localOnly.Aggregate((a, b) => a + "\n" + b)}",
                    ok: "Commit Anyway",
                    cancel: "Cancel"
                );
            }

            if (VCSettings.RequireLockBeforeCommit && localModified.Any())
            {
                string title = $"{Terminology.getlock} '{Terminology.localModified}' files?";
                string message = $"You are trying to commit files which are '{Terminology.localModified}'.\nDo you want to '{Terminology.getlock}' these files first?";
                if (UserDialog.DisplayDialog(title, message, Terminology.getlock, "Abort"))
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

        public bool SetLocalOnly(IEnumerable<string> assets)
        {
            return PerformOperation(OperationType.SetLocalOnly, assets, vcc.SetLocalOnly);
        }

        #endregion
    }
}
