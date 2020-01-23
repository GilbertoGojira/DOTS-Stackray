using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Stackray.Burst.Editor {

  [InitializeOnLoad]
  class BurstCompile {

    private const string MainAssemblyFileName = "Assembly-CSharp.dll";

    static string LibraryPlayerScriptAssemblies {
      get => Application.dataPath + "/../Library/PlayerScriptAssemblies/";
    }

    static BurstCompile() {
      CompilationPipeline.compilationFinished += CompilationPipeline_compilationFinished;      
    }

    private static void CompilationPipeline_compilationFinished(object obj) {
      if (!Directory.Exists(LibraryPlayerScriptAssemblies))
        return;
      var watch = System.Diagnostics.Stopwatch.StartNew();
      var assemblyToInjectPath = LibraryPlayerScriptAssemblies + MainAssemblyFileName;
      var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player)
        .Select(a => Path.GetFileName(a.outputPath));
      var jobResolver = new GenericJobResolver(assemblies, false);
      var resolvedJobs = jobResolver.ResolveGenericJobs();
      jobResolver.AddTypes(assemblyToInjectPath, "ConcreteJobs", resolvedJobs);
      jobResolver.Dispose();
      watch.Stop();

      var log = $"{watch.ElapsedMilliseconds * 0.001f}s to inject {resolvedJobs.Count()} concrete jobs in assembly '{assemblyToInjectPath}'";
      Debug.Log(log);
      log += "\n" + string.Join("\n", resolvedJobs.Select(j => j.FullName));
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
}
