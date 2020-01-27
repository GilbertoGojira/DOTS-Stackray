using System;
using System.IO;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

namespace Stackray.Burst.Editor {

  class BurstCompile : IPostBuildPlayerScriptDLLs {

    private const string MainAssemblyFileName = "Assembly-CSharp.dll";

    private const string TempStagingManaged = @"Temp/StagingArea/Data/Managed/";

    public int callbackOrder => -1;

    public static void Compile() {
      var watch = System.Diagnostics.Stopwatch.StartNew();
      var assemblyToInjectPath = TempStagingManaged + MainAssemblyFileName;
      var assemblyPaths = CompilationPipeline.GetAssemblies(AssembliesType.Player)
        .Where(a => File.Exists(TempStagingManaged + Path.GetFileName(a.outputPath)))
        .Select(a => TempStagingManaged + Path.GetFileName(a.outputPath));
      var resolvedTypes = GenericResolver.InjectGenericJobs(assemblyPaths, assemblyToInjectPath);
      watch.Stop();

      var log = $"{watch.ElapsedMilliseconds * 0.001f}s to inject {resolvedTypes.Count()} concrete jobs in assembly '{Path.GetFullPath(assemblyToInjectPath)}'";
      Debug.Log(log);
      log += "\n" + string.Join("\n", resolvedTypes);
      WriteLog(log);
    }

    static void WriteLog(string log) {
      var logDir = Path.Combine(Environment.CurrentDirectory, "Logs");
      var debugLogFile = Path.Combine(logDir, "burst_injected_jobs.log");
      if (!Directory.Exists(logDir))
        Directory.CreateDirectory(logDir);
      File.WriteAllText(debugLogFile, log);
    }

    public void OnPostBuildPlayerScriptDLLs(BuildReport report) {
      Compile();
    }
  }
}
