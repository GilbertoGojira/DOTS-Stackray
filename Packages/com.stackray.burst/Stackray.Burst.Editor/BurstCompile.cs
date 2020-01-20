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
      var assemblyPath = LibraryPlayerScriptAssemblies + MainAssemblyFileName;
      var jobResolver = new GenericJobResolver(new[] { "CSharp" }, false);
      var resolvedJobs = jobResolver.ResolveGenericJobs();
      jobResolver.AddTypes(assemblyPath, "ConcreteJobs", resolvedJobs);
      jobResolver.Dispose();

      var log = $"Concrete jobs injected in assembly '{assemblyPath}'";
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
