using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VersionControl;
using VersionControl.Backend.SVN;

namespace PerformanceTests
{
    class Program
    {
        static readonly IVersionControlCommands vcc = new VCCFilteredAssets(CreateSVNCommands());
        private const string localPathForTest = @"c:\develop\Game2.4";

        static void Main(string[] args)
        {
            Initialize();
            for (int i = 0; i < 10; ++i)
            {
                RunMemoryTestComplex();
                RunMemoryTestSimple();
            }
            Logging("Test Finished!");
        }

        static void Initialize()
        {
            D.writeErrorCallback += Logging;
            D.writeWarningCallback += Logging;
            D.writeLogCallback += Logging;
            D.exceptionCallback += HandleExceptions;
            vcc.ProgressInformation += D.Log;
            vcc.SetWorkingDirectory(localPathForTest);
            Directory.SetCurrentDirectory(localPathForTest);
            vcc.Start();

            vcc.Status(StatusLevel.Remote, DetailLevel.Verbose);
            Logging("Status Complete");
        }

        static IVersionControlCommands CreateSVNCommands()
        {
            return new SVNCommands();
        }

        static void HandleExceptions(Exception e)
        {
            throw e;
        }
        static void Logging(string message)
        {
            Console.WriteLine(message);
        }


        static void RunMemoryTestComplex()
        {
            var assets = vcc.GetFilteredAssets((assetPath, status) =>
            {
                VersionControlStatus metaStatus = status;
                if (!status.assetPath.EndsWith(".meta"))
                {
                    metaStatus = vcc.GetAssetStatus(status.assetPath + ".meta");
                }
                return (status.fileStatus != VCFileStatus.Normal || metaStatus.fileStatus != VCFileStatus.Normal);
            });

            Logging(assets.Aggregate((a, b) => a + "\n" + b));
            Logging("Memory Used Complex: " + GC.GetTotalMemory(true));
        }

        static void RunMemoryTestSimple()
        {
            var assets = vcc.GetFilteredAssets((assetPath, status) => (status.fileStatus != VCFileStatus.Normal));
            Logging(assets.Aggregate((a, b) => a + "\n" + b));
            Logging("Memory Used Simple: " + GC.GetTotalMemory(true));
        }

    }
}
