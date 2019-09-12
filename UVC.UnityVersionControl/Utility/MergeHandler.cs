using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UVC.Logging;

namespace UVC
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    public static class MergeHandler
    {
        public static (string, string) GetDiffCommandLine(string theirs, string yours)
        {
            StringBuilder args = new StringBuilder(VCSettings.DifftoolArgs);
            args.Replace("[theirs]", theirs);
            args.Replace("[yours]", yours);
            return (VCSettings.DifftoolPath, args.ToString());
        }
        public static (string, string) GetMergeCommandLine(string basepath, string theirs, string yours, string merge)
        {
            StringBuilder args = new StringBuilder(VCSettings.MergetoolArgs);
            args.Replace("[base]", basepath);
            args.Replace("[theirs]", theirs);
            args.Replace("[yours]", yours);
            args.Replace("[merge]", merge);
            return (VCSettings.MergetoolPath, args.ToString());
        }

        static string binary2TextPath = null;
        static string GetBinaryConverterPath()
        {
            if (binary2TextPath == null)
                binary2TextPath = EditorApplication.applicationPath.Replace("Unity.exe", "") + (Application.platform == RuntimePlatform.WindowsEditor ? "Data/Tools/binary2text.exe" : "/Contents/Tools/binary2text");
            return binary2TextPath;
        }

        const string tempDirectory = "Library/UVC/";
        public static void DiffWithBase(string assetPath)
        {
            if (!string.IsNullOrEmpty(assetPath))
            {
                string baseAssetPath = VCCommands.Instance.GetBasePath(assetPath);
                var workingDirectory = GetWorkingDirectory();
                if (!string.IsNullOrEmpty(baseAssetPath))
                {
                    if (EditorSettings.serializationMode == SerializationMode.ForceBinary && requiresTextConversionPostfix.Any(new ComposedString(assetPath).EndsWith))
                    {
                        if (!Directory.Exists(tempDirectory))
                            Directory.CreateDirectory(tempDirectory);
                        string convertedBaseFile = tempDirectory + Path.GetFileName(assetPath) + ".base";
                        string convertedWorkingCopyFile = tempDirectory + Path.GetFileName(assetPath) + ".wc";
                        var baseConvertCommand = new CommandLineExecution.CommandLine(GetBinaryConverterPath(), baseAssetPath + " "  + convertedBaseFile, ".").Execute();
                        if (baseConvertCommand.Failed)
                        {
                            DebugLog.LogError("Command line Error: " + baseConvertCommand.ErrorStr + baseConvertCommand.OutputStr);
                            return;
                        }
                        var workingCopyConvertCommand = new CommandLineExecution.CommandLine(GetBinaryConverterPath(), assetPath + " " + convertedWorkingCopyFile, ".").Execute();
                        if (workingCopyConvertCommand.Failed)
                        {
                            DebugLog.LogError("Command line Error: " + workingCopyConvertCommand.ErrorStr + workingCopyConvertCommand.OutputStr);
                            return;
                        }
                        var (toolpath, args) = GetDiffCommandLine(Path.GetFullPath(convertedBaseFile), Path.GetFullPath(convertedWorkingCopyFile));
                        var diffCommand = new CommandLineExecution.CommandLine(toolpath, args, workingDirectory);
                        Task.Run(() => diffCommand.Execute());
                    }
                    else
                    {
                        var (toolpath, args) = GetDiffCommandLine(baseAssetPath, Path.GetFullPath(assetPath));
                        var baseConvertCommand = new CommandLineExecution.CommandLine(toolpath, args, workingDirectory);
                        Task.Run(() => baseConvertCommand.Execute());
                    }
                }
            }
        }

        public static string GetWorkingDirectory()
        {
            return Application.dataPath.Remove(Application.dataPath.LastIndexOf("/Assets", StringComparison.Ordinal));
        }

        public static void ResolveConflict(string assetPath)
        {
            VCCommands.Instance.GetConflict(assetPath, out var basePath, out var yours, out var theirs);
            ResolveConflict(assetPath, basePath, theirs, yours);
        }
        
        public static void ResolveConflict(string assetPath, string basepath, string theirs, string yours)
        {
            if (!string.IsNullOrEmpty(assetPath))
            {
                var workingDirectory   = GetWorkingDirectory();
                var path               = Path.GetDirectoryName(assetPath);
                var file               = Path.GetFileName(assetPath);
                basepath               = Path.GetFullPath(basepath);
                theirs                 = Path.GetFullPath(theirs);
                yours                  = Path.GetFullPath(yours);
                string merge           = Path.GetFullPath(assetPath);
                DateTime lastWriteTime = File.GetLastWriteTime(assetPath);

                var (toolpath, args) = GetMergeCommandLine(basepath, theirs, yours, merge);
                var mergeCommand = new CommandLineExecution.CommandLine(toolpath, args, workingDirectory);
                Task.Run(() =>
                {
                    while (true)
                    {
                        Thread.Sleep(100);
                        if (File.GetLastWriteTime(assetPath) != lastWriteTime)
                        {
                            return true;
                        }
                    }
                }).ContinueWithOnNextUpdate(modified =>
                    {
                        VCCommands.Instance.Status(new[] {assetPath}, StatusLevel.Local);
                        if (VCCommands.Instance.GetAssetStatus(assetPath).fileStatus == VCFileStatus.Conflicted)
                        {
                            if (UserDialog.DisplayDialog("Merge Successful?", $"Did the merge complete successfully?\n'{assetPath}'", "Yes", "No"))
                            {
                                VCCommands.Instance.Resolve(new[] {assetPath}, ConflictResolution.Mine);
                                VCCommands.Instance.Status(StatusLevel.Previous, DetailLevel.Normal);
                            }
                        }
                    }
                );
                Task.Run(() => mergeCommand.Execute());
            }
        }

        static readonly ComposedString[] requiresTextConversionPostfix  = 
        { 
            ".unity", ".prefab", ".mat", ".asset" 
        };

        static readonly ComposedString[] mergablePostfix = 
        { 
            ".cs", ".js", ".boo", ".text", ".shader", ".txt", ".xml", ".json", ".yml", ".asmdef", ".manifest", 
            ".compute", ".cpp", ".h", ".sh", ".bat", ".mm", "wwu" 
        };
        
        public static bool IsDiffableAsset(ComposedString assetPath)
        {
            return mergablePostfix.Any(assetPath.EndsWith) || requiresTextConversionPostfix.Any(assetPath.EndsWith);
        }

        public static bool IsMergableAsset(ComposedString assetPath)
        {
            return mergablePostfix.Any(assetPath.EndsWith);
        }
    }
}