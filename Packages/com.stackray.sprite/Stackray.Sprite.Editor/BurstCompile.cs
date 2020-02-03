using Stackray.Burst.Editor;
using System;
using System.IO;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;


namespace Stackray.Sprite.Editor {

  class BurstCompile : IPostBuildPlayerScriptDLLs {

    private const string MainAssemblyFileName = "Assembly-CSharp.dll";

    private const string TempStagingManaged = @"Temp/StagingArea/Data/Managed/";

    public int callbackOrder => -2;

    public static void Compile() {
      var watch = System.Diagnostics.Stopwatch.StartNew();
      var assemblyToInjectPath = Path.GetFullPath(TempStagingManaged + MainAssemblyFileName);
      var injectedTypes =
        GenericResolver.InjectTypes(SpritePropertyAnimatorUtility.CreateConverters().Select(c => c.GetType()), assemblyToInjectPath).Union(
          GenericResolver.InjectTypes(SpritePropertyAnimatorUtility.CreatePossibleTypes(), assemblyToInjectPath));
      watch.Stop();

      var log = $"{watch.ElapsedMilliseconds * 0.001f}s to inject {injectedTypes.Count()} concrete types in assembly '{Path.GetFullPath(assemblyToInjectPath)}'";
      Debug.Log(log);
      log += "\n" + string.Join("\n", injectedTypes);
      WriteLog(log);
    }

    static void WriteLog(string log) {
      var logDir = Path.Combine(Environment.CurrentDirectory, "Logs");
      var debugLogFile = Path.Combine(logDir, $"burst_injected_{nameof(Stackray)}_{nameof(Sprite)}_types.log");
      if (!Directory.Exists(logDir))
        Directory.CreateDirectory(logDir);
      File.WriteAllText(debugLogFile, log);
    }

    public void OnPostBuildPlayerScriptDLLs(BuildReport report) {
      Compile();
    }
  }
}