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
    using UnityEngine;
    using UnityEditor;
    using UserInterface;

    [InitializeOnLoad]
    public class VCCommandsOnLoad
    {
        static VCCommandsOnLoad() { OnNextUpdate.Do(VCCommands.Initialize); }
    }

    public static class VersionControlStatusExtension
    {
        public static VersionControlStatus MetaStatus(this VersionControlStatus vcs)
        {
            return VCCommands.Instance.GetAssetStatus(vcs.assetPath + VCCAddMetaFiles.meta);
        }
    }

    /// <summary>
    /// Wraps the underlying IVersionControlCommands and add handling of .meta files and other Unity specific concerns.
    /// OnNextUpdate.Do is used to delay callback actions to the next Unity update as Unity does not allow calls into UnityEngine
    /// or UnityEditor if not orriginating from Unity's update loop.
    /// </summary>
    public sealed class VCCommands : IVersionControlCommands
    {
        private VCCommands()
        {
            vcc.SetWorkingDirectory(Application.dataPath.Remove(Application.dataPath.LastIndexOf("/Assets", StringComparison.Ordinal)));
            vcc.ProgressInformation += progress => { if (ProgressInformation != null) OnNextUpdate.Do(() => ProgressInformation(progress)); };
            vcc.StatusCompleted += OnStatusCompleted;
            OnNextUpdate.Do(Start);
            EditorApplication.playmodeStateChanged += () =>
            {
                if (!Application.isPlaying) Start();
                else Stop();
            };
        }

        static VCCommands instance;
        public static void Initialize() { if (instance == null) { instance = new VCCommands(); } }
        public static VCCommands Instance { get { Initialize(); return instance; } }

        private readonly IVersionControlCommands vcc = VersionControlFactory.CreateVersionControlCommands();
        private List<string> lockedFileResources = new List<string>();
        private bool ignoreStatusRequests = false;
        private Action<Object> saveSceneCallback = o => EditorApplication.SaveScene();

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
                StatusTask(statusLevel, detailLevel)/*.ContinueWithOnNextUpdate(t => vcc.Start())*/;
                vcc.Start();
            }
        }

        public void Stop()
        {
            vcc.Stop();
            vcc.ClearDatabase();
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

        private bool RefreshAssetDatabase()
        {
            OnNextUpdate.Do(AssetDatabase.Refresh);
            return true;
        }

        private void FlushFiles()
        {
            if (ThreadUtility.IsMainThread())
            {
                ignoreStatusRequests = true;
                D.Log("Flusing files");
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

        private bool RemoteHasUnloadableResourceChange()
        {
            var remoteChanged = GetFilteredAssets((a, s) => s.remoteStatus == VCRemoteFileStatus.Modified);
            return lockedFileResources.Any(b => remoteChanged.Any(a => String.CompareOrdinal(a.ToLowerInvariant(), b.ToLowerInvariant()) == 0));
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
            return StartTask(() => Update(assets));
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
        public void SetWorkingDirectory(string workingDirectory)
        {
            vcc.SetWorkingDirectory(workingDirectory);
        }
        public void SetUserCredentials(string userName, string password)
        {
            vcc.SetUserCredentials(userName, password);
        }
        public VersionControlStatus GetAssetStatus(string assetPath)
        {
            return vcc.GetAssetStatus(assetPath);
        }
        public IEnumerable<string> GetFilteredAssets(Func<string, VersionControlStatus, bool> filter)
        {
            return vcc.GetFilteredAssets(filter);
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
        
        public bool Update(IEnumerable<string> assets)
        {
            return HandleExceptions(() =>
            {
                if (RemoteHasUnloadableResourceChange())
                {
                    OnNextUpdate.Do(() => EditorUtility.DisplayDialog("Update in Unity not possible", "The server has changes to files that Unity can not reload. Close Unity and 'update' with an external version control tool.", "OK"));
                    return false;
                }
                return vcc.Update(assets) && RefreshAssetDatabase();
            });
        }

        public bool Commit(IEnumerable<string> assets, string commitMessage = "")
        {
            return HandleExceptions(() =>
            {
                FlushFiles();
                Status(assets, StatusLevel.Local);
                bool commitSuccess = vcc.Commit(assets, commitMessage);
                RefreshAssetDatabase();
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
                bool changeListRemoveSuccess =  vcc.ChangeListRemove(assets);
                bool releaseSuccess = true;
                if (revertSuccess) releaseSuccess = vcc.ReleaseLock(assets);
                RefreshAssetDatabase();
                return (revertSuccess && releaseSuccess) || changeListRemoveSuccess;
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
                return vcc.Delete(deleteAssets, mode) && RefreshAssetDatabase();
            });
        }
        public bool GetLock(IEnumerable<string> assets, OperationMode mode = OperationMode.Normal)
        {
            return HandleExceptions(() =>
            {
                bool getlockSuccess = vcc.GetLock(assets, mode);
                bool removeChangeListSuccess = vcc.ChangeListRemove(assets);
                return getlockSuccess;
            });
        }
        public bool ReleaseLock(IEnumerable<string> assets)
        {
            return HandleExceptions(() => vcc.ReleaseLock(assets));
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
                RefreshAssetDatabase();
                return resolveSuccess;
            });
        }
        public bool Move(string from, string to)
        {
            return HandleExceptions(() =>
            {
                FlushFiles();
                bool moveSuccess = vcc.Move(from, to);
                RefreshAssetDatabase();
                return moveSuccess;
            });
        }
        public string GetBasePath(string assetPath)
        {
            return vcc.GetBasePath(assetPath);
        }
        public bool CleanUp()
        {
            return HandleExceptions(() => vcc.CleanUp());
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
            //D.Log("Status Updatees : " + (StatusCompleted != null ? StatusCompleted.GetInvocationList().Length : 0));
            if (StatusCompleted != null) OnNextUpdate.Do(StatusCompleted);
        }

        public event Action<string> ProgressInformation;
        public event Action StatusCompleted;

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

            if (assets.Contains(EditorApplication.currentScene))
            {
                EditorApplication.SaveCurrentSceneIfUserWantsTo();
            }
            if (showUserConfirmation || initialAssetCount < (assets.Count() + dependencies.Count()))
            {
                return OpenCommitDialogWindow(assets, dependencies);
            }
            return Commit(assets, commitMessage);
        }

        public bool BypassRevision(IEnumerable<string> assets)
        {
            return vcc.ChangeListAdd(assets, "bypass");
        }

        #endregion

        /// <summary>
        /// Add files to a list that requires Unity to be closed to update correctly. Eg. native plugins
        /// </summary>
        /// <param name="assets">List of assetpaths</param>
        public void AddLockedFileResources(IEnumerable<string> assets)
        {
            lockedFileResources = lockedFileResources.Concat(assets).Distinct().ToList();
        }
        public void SetPersistentObjectCallback(Func<Object, string> persistentObjectCallback)
        {
            ObjectUtilities.SetSceneObjectToAssetPathCallback(persistentObjectCallback);
        }
    }
}
