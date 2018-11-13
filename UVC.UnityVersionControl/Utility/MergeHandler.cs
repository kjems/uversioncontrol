using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace UVC
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    public static class MergeHandler
    {
        public struct MergeTool
        {
            public string name;
            public string pathDiff;
            public string pathMerge;
            public string argumentsDiff;
            public string argumentsMerge;

            public (string, string) GetDiffCommandLine(string theirs, string yours)
            {
                StringBuilder diffargs = new StringBuilder(argumentsDiff);
                diffargs.Replace("[theirs]", theirs);
                diffargs.Replace("[yours]", yours);
                return (pathDiff, diffargs.ToString());
            }
            public (string, string) GetMergeCommandLine(string basepath, string theirs, string yours, string merge)
            {
                StringBuilder mergeargs = new StringBuilder(argumentsMerge);
                mergeargs.Replace("[base]", basepath);
                mergeargs.Replace("[theirs]", theirs);
                mergeargs.Replace("[yours]", yours);
                mergeargs.Replace("[merge]", merge);
                return (pathMerge, mergeargs.ToString());
            }
        }
        
        public static List<MergeTool> mergeTools = new List<MergeTool>
        {
            #if UNITY_EDITOR_OSX
            new MergeTool
            {
                name = "P4Merge",
                pathDiff  = "/Applications/p4merge.app/Contents/MacOS/p4merge",
                pathMerge = "/Applications/p4merge.app/Contents/MacOS/p4merge",
                argumentsDiff  = "'[theirs]' '[yours]'",
                argumentsMerge = "'[base]' '[theirs]' '[yours]' '[merge]'"
            },
            new MergeTool
            {
                name = "Beyond Compare 4",
                pathDiff  = "/Applications/Beyond Compare.app/Contents/MacOS/bcomp",
                pathMerge = "/Applications/Beyond Compare.app/Contents/MacOS/bcomp",
                argumentsDiff  = "'[theirs]' '[yours]'",
                argumentsMerge = "'[theirs]' '[yours]' '[base]' '[merge]'"
            },
            new MergeTool
            {
                name = "Semantic Merge (P4 diff)",
                pathDiff  = "/Applications/p4merge.app/Contents/MacOS/p4merge",
                pathMerge = "/Applications/semanticmerge.app/Contents/MacOS/semanticmerge",
                argumentsDiff  = "'[theirs]' '[yours]'",
                argumentsMerge = "[yours] [theirs] [base] [merge] " +
                                 "--nolangwarn -emt=\"/Applications/p4merge.app/Contents/MacOS/p4merge '[base]' '[theirs]' '[yours]' '[merge]'\""
            }
            #endif
            #if UNITY_EDITOR_WIN
            new MergeTool
            {
                name = "P4Merge",
                pathDiff  = "D:/Perforce/p4merge.exe",
                pathMerge = "D:/Perforce/p4merge.exe",
                argumentsDiff  = "\"[theirs]\" \"[yours]\"",
                argumentsMerge = "\"[base]\" \"[theirs]\" \"[yours]\" \"[merge]\""
            },
            new MergeTool
            {
                name = "Beyond Compare 4",
                pathDiff  = "C:/Program Files/Beyond Compare 4/BComp.exe",
                pathMerge = "C:/Program Files/Beyond Compare 4/BComp.exe",
                argumentsDiff  = "\"[theirs]\" \"[yours]\"",
                argumentsMerge = "\"[theirs]\" \"[yours]\" \"[base]\" \"[merge]\""
            },
            new MergeTool
            {
                name = "Semantic Merge (P4 diff)",
                pathDiff  = "C:/Users/Kristian Kjems/AppData/Local/semanticmerge/mergetool.exe",
                pathMerge = "C:/Users/Kristian Kjems/AppData/Local/semanticmerge/mergetool.exe",
                argumentsDiff  = "\"[theirs]\" \"[yours]\"",
                argumentsMerge = "\"[yours]\" \"[theirs]\" \"[base]\" \"[merge]\" " +
                                 "--nolangwarn -emt=\"/Applications/p4merge.app/Contents/MacOS/p4merge \"[base]\" \"[theirs]\" \"[yours]\" \"[merge]\"\""
            }
            #endif
        };

        public static void AddMergeTool(MergeTool mergeTool)
        {
            mergeTools.Add(mergeTool);
        }
        
        public static void RemoveMergeTool(Predicate<MergeTool> mergeToolPredicate)
        {
            mergeTools.RemoveAll(mergeToolPredicate);
        }
        
        public static int MergeToolIndex(string name)
        {
            for (int i = 0; i < mergeTools.Count; i++)
            {
                if (mergeTools[i].name == name)
                    return i;
            }
            return -1;
        }
        
        public static (string, string) GetMergeCommandLine(string name, string basepath, string theirs, string yours, string merge)
        {
            return mergeTools.First(mt => mt.name == name).GetMergeCommandLine(basepath, theirs, yours, merge);
        }
        
        public static (string, string) GetDiffCommandLine(string name, string theirs, string yours)
        {
            return mergeTools.First(mt => mt.name == name).GetDiffCommandLine(theirs, yours);
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
                        string convertedBaseFile = tempDirectory + Path.GetFileName(assetPath) + ".svn-base";
                        string convertedWorkingCopyFile = tempDirectory + Path.GetFileName(assetPath) + ".svn-wc";
                        var baseConvertCommand = new CommandLineExecution.CommandLine(GetBinaryConverterPath(), baseAssetPath + " "  + convertedBaseFile, ".").Execute();
                        var workingCopyConvertCommand = new CommandLineExecution.CommandLine(GetBinaryConverterPath(), assetPath + " " + convertedWorkingCopyFile, ".").Execute();
                        var (toolpath, args) = GetDiffCommandLine(VCSettings.Mergetool, Path.GetFullPath(convertedBaseFile), Path.GetFullPath(convertedWorkingCopyFile));
                        var diffCommand = new CommandLineExecution.CommandLine(toolpath, args, workingDirectory);
                        Task.Run(() => diffCommand.Execute());
                    }
                    else
                    {
                        if (!Directory.Exists(tempDirectory))
                            Directory.CreateDirectory(tempDirectory);
                        string copiedBaseAssetPath = Path.GetFullPath(tempDirectory + "base_" + Path.GetFileName(assetPath));
                        if(File.Exists(copiedBaseAssetPath))
                            File.Delete(copiedBaseAssetPath);
                        File.Copy(baseAssetPath, copiedBaseAssetPath);
                        var (toolpath, args) = GetDiffCommandLine(VCSettings.Mergetool, copiedBaseAssetPath, Path.GetFullPath(assetPath));
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
            if (!string.IsNullOrEmpty(assetPath))
            {
                var workingDirectory = GetWorkingDirectory();
                var path = Path.GetDirectoryName(assetPath);
                var file = Path.GetFileName(assetPath);
                string basepath = Path.GetFullPath(Directory.GetFiles(path, $"{file}.merge-left.r*").First());
                string theirs   = Path.GetFullPath(Directory.GetFiles(path, $"{file}.merge-right.r*").First());
                string yours    = Path.GetFullPath($"{assetPath}.working");
                string merge    = Path.GetFullPath(assetPath);
                DateTime lastWriteTime = File.GetLastWriteTime(assetPath);

                var (toolpath, args) = GetMergeCommandLine(VCSettings.Mergetool, basepath, theirs, yours, merge);
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
                            if (EditorUtility.DisplayDialog("Merge Successful?", $"Did the merge complete successfully?\n'{assetPath}'", "Yes", "No"))
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

        static readonly ComposedString[] requiresTextConversionPostfix  = { ".unity", ".prefab", ".mat", ".asset" };
        static readonly ComposedString[] mergablePostfix                = { ".cs", ".js", ".boo", ".text", ".shader", ".txt", ".xml", ".json", ".asmdef", ".manifest", ".compute", ".cpp", ".h" };
        
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