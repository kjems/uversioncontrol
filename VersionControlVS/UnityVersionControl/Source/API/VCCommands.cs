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
            return VCCommands.Instance.GetAssetStatus(vcs.assetPath + ".meta");
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
            OnNextUpdate.Do(() => StatusTask(false, false));
            EditorApplication.playmodeStateChanged += () => StatusTask(false, false);
        }

        static VCCommands instance;
        public static void Initialize() { if (instance == null) { instance = new VCCommands(); } }
        public static VCCommands Instance { get { Initialize(); return instance; } }

        private readonly IVersionControlCommands vcc = VersionControlFactory.CreateVersionControlCommands();
        private List<string> lockedFileResources = new List<string>();
        private bool ignoreStatusRequests = false;
        private Action<Object> _saveSceneCallback = o => EditorApplication.SaveScene();

        public static bool Active
        {
            get
            {
                bool playmode = ThreadUtility.IsMainThread() && EditorApplication.isPlayingOrWillChangePlaymode;
                return VCSettings.VCEnabled && !playmode;
            }
        }

        public bool Ready { get { return Active && vcc.IsReady(); } }


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
                    Debug.LogError("Unhandled exception: " + e.Message + "\n" + D.GetCallstack());
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

        public Task<bool> StatusTask(bool remote = true, bool full = true)
        {
            return StartTask(() => Status(remote, full));
        }

        public Task<bool> StatusTask(IEnumerable<string> assets, bool remote = true)
        {
            assets = new List<string>(assets);
            return StartTask(() => Status(assets, remote));
        }

        public Task<bool> UpdateTask(IEnumerable<string> assets = null, bool force = true)
        {
            if (assets != null) assets = new List<string>(assets);
            return StartTask(() => Update(assets, force));
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

        public Task<bool> GetLockTask(IEnumerable<string> assets, bool force = false)
        {
            assets = new List<string>(assets);
            return StartTask(() => GetLock(assets, force));
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
            return AssetpathsFilters.RemoveMetaPostFix(vcc.GetFilteredAssets(filter));
        }
        public bool Status(bool remote = true, bool full = true)
        {
            return HandleExceptions(() =>
            {
                bool result = vcc.Status(remote, full);
                return result;
            });
        }

        public bool Status(IEnumerable<string> assets, bool remote = false)
        {
            return HandleExceptions(() =>
            {
                var withMeta = AssetpathsFilters.AddMeta(assets, true);
                bool result = vcc.Status(withMeta, remote);
                return result;
            });
        }

        public bool RequestStatus(IEnumerable<string> assets, bool remote)
        {
            if (ignoreStatusRequests) return false;
            return vcc.RequestStatus(assets, remote);
        }

        public bool RequestStatus(string asset, bool remote)
        {
            if (ignoreStatusRequests) return false;
            return vcc.RequestStatus(asset, remote);
        }

        public bool Update(IEnumerable<string> assets, bool force = true)
        {
            return HandleExceptions(() =>
            {
                if (RemoteHasUnloadableResourceChange())
                {
                    OnNextUpdate.Do(() => EditorUtility.DisplayDialog("Update in Unity not possible", "The server has changes to files that Unity can not reload. Close Unity and 'update' with an external version control tool.", "OK"));
                    return false;
                }
                return vcc.Update(assets, force) && RefreshAssetDatabase();
            });
        }

        public bool Commit(IEnumerable<string> assets, string commitMessage = "")
        {
            return HandleExceptions(() =>
            {
                FlushFiles();
                assets = AssetpathsFilters.AddMeta(assets);
                return Status(assets) && vcc.Commit(assets, commitMessage) && RefreshAssetDatabase();
            });
        }
        public bool Add(IEnumerable<string> assets)
        {
            return HandleExceptions(() =>
            {
                assets = AssetpathsFilters.AddMeta(assets, true);
                return vcc.Add(assets);
            });
        }
        public bool Revert(IEnumerable<string> assets)
        {
            return HandleExceptions(() =>
            {
                FlushFiles();
                Status(assets);
                assets = AssetpathsFilters.AddMeta(assets);
                bool revertResult = vcc.Revert(assets);
                vcc.ChangeListRemove(assets);
                if (revertResult) vcc.ReleaseLock(assets);
                RefreshAssetDatabase();
                return revertResult;
            });
        }
        public bool Delete(IEnumerable<string> assets, bool force = false)
        {
            return HandleExceptions(() =>
            {
                var deleteAssets = new List<string>();
                foreach (string assetIt in assets)
                {
                    var metaAsset = assetIt + ".meta";
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
                return vcc.Delete(deleteAssets, force) && RefreshAssetDatabase();
            });
        }
        public bool GetLock(IEnumerable<string> assets, bool force = false)
        {
            return HandleExceptions(() => vcc.GetLock(assets, force) && vcc.ChangeListRemove(assets));
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
            return HandleExceptions(() => vcc.Resolve(assets, conflictResolution)) && RefreshAssetDatabase();
        }
        public bool Move(string from, string to)
        {
            return HandleExceptions(() =>
                {
                    FlushFiles();
                    return vcc.Move(from, to) && vcc.Move(from + ".meta", to + ".meta");
                }) && RefreshAssetDatabase();
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
        }
        public void RemoveFromDatabase(IEnumerable<string> assets)
        {
            vcc.RemoveFromDatabase(assets);
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

        public void BypassRevision(IEnumerable<string> assets)
        {
            vcc.ChangeListAdd(assets, "bypass");
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
        public void SetSceneObjectToAssetPathCallback(Func<Object, string> sceneObjectToAssetPath)
        {
            ObjectExtension.SetSceneObjectToAssetPath(sceneObjectToAssetPath);
        }
        public void SetSaveSceneCallback(Action<Object> saveSceneCallback)
        {
            _saveSceneCallback = saveSceneCallback;
        }
        public void SaveScene(Object obj)
        {
            _saveSceneCallback(obj);
        }
    }
}
