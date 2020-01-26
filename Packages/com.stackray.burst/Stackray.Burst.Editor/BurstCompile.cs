using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

namespace Stackray.Burst.Editor {

  [InitializeOnLoad]
  class BurstCompile {

    public static bool ReadyToCompile;

    private const string MainAssemblyFileName = "Assembly-CSharp.dll";

    static string LibraryPlayerScriptAssemblies {
      get => Application.dataPath + "/../Library/PlayerScriptAssemblies/";
    }

    static BurstCompile() {
      CompilationPipeline.compilationFinished += CompilationPipeline_compilationFinished;      
    }

    private static void CompilationPipeline_compilationFinished(object obj) {
      if (!ReadyToCompile)
        return;
      var watch = System.Diagnostics.Stopwatch.StartNew();
      var assemblyToInjectPath = LibraryPlayerScriptAssemblies + MainAssemblyFileName;
      var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player)
        .Select(a => Path.GetFileName(a.outputPath));
      var resolvedTypes =GenericResolver.InjectGenericJobs(assemblies, assemblyToInjectPath);
      watch.Stop();

      var log = $"{watch.ElapsedMilliseconds * 0.001f}s to inject {resolvedTypes.Count()} concrete jobs in assembly '{assemblyToInjectPath}'";
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
  }

  class MyBuildPostprocessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport {
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report) {
      BurstCompile.ReadyToCompile = false;
    }

    public void OnPreprocessBuild(BuildReport report) {
      BurstCompile.ReadyToCompile = true;
    }
  }
}
