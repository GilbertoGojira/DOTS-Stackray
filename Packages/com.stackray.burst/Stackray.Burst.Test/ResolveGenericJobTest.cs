using Mono.Cecil;
using NUnit.Framework;
using Stackray.Burst.Editor;
using Stackray.TestGenericJobs;
using System.IO;
using System.Linq;
using UnityEditor.Compilation;
using UnityEngine;

namespace Stackray.Burst.Test {

  public class ResolveGenericJob {

    static string AssembliesPath = Application.dataPath + "/../Library/ScriptAssemblies/";
    static System.Reflection.Assembly TestAssembly = System.Reflection.Assembly.LoadFile(AssembliesPath + "Stackray.TestGenericJobs.dll");

    [Test]
    public void GetGenericJobsCallsTest() {
      var jobResolver = new GenericJobResolver(new[] { "Stackray.TestGenericJobs.dll" }, false);
      var genericJobs = jobResolver.GetGenericJobCalls();
      jobResolver.Dispose();
      Assert.True(genericJobs.Count() == GenericJobs<bool, bool>.GENERIC_UNIQUE_JOB_ENTRIES);
    }

    [Test]
    public void ResolveGenericJobsTest() {
      var jobResolver = new GenericJobResolver(new[] { "Stackray.TestGenericJobs.dll" }, false);
      var resolvedJobs = jobResolver.ResolveGenericJobs();
      jobResolver.Dispose();
      Assert.True(resolvedJobs.Count() == GenericJobs<bool, bool>.CONCRETE_UNIQUE_JOB_ENTRIES);
    }

    [Test]
    public void GetTypesTest() {
      var types = CecilTypeUtility.GetAssemblies(new[] { "Stackray.TestGenericJobs.dll" }, false)
        .SelectMany(a => a.GetTypes())
        .ToArray();
      Assert.True(types != null);
    }

    [Test]
    public void WriteNewAssemblyInjectionTest() {
      var assemblyPath = AssembliesPath + "TestConcreteAssembly.dll";
      var jobResolver = new GenericJobResolver(new[] { "Stackray.TestGenericJobs.dll" }, false);
      var resolvedJobs = jobResolver.ResolveGenericJobs();
      jobResolver.Dispose();
      var outputAssembly = CecilTypeUtility.CreateAssembly("TestConcreteJobs", resolvedJobs);
      outputAssembly.Write(assemblyPath);

      var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
      var methods = CecilTypeUtility.GetMethodDefinitions(assembly).Where(m => m.FullName.Contains("TestConcreteJobs"));
      assembly.Dispose();
      Assert.True(methods.Any());
    }

    [Test]
    public void CheckAssemblyInjectionTest() {
      var assemblyPath = AssembliesPath + "Assembly-CSharp.dll";
      var jobResolver = new GenericJobResolver(new[] { "Stackray.Dummy" }, false);
      var resolvedJobs = jobResolver.ResolveGenericJobs();
      jobResolver.AddTypes(assemblyPath, "TestConcreteJobs", resolvedJobs);
      jobResolver.Dispose();
      var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
      var methods = CecilTypeUtility.GetMethodDefinitions(assembly).Where(m => m.FullName.Contains("TestConcreteJobs"));
      assembly.Dispose();
      Assert.True(methods.Any());
    }

    [Test]
    public void ResolveFullDomainGenericJobsTest() {
      var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player)
        .Select(a => Path.GetFileName(a.outputPath));
      var jobResolver = new GenericJobResolver(assemblies, false);
      var resolvedJobs = jobResolver.ResolveGenericJobs();
      jobResolver.Dispose();
    }

    [Test]
    public void ResolveGenericCascadeCallTest() {
      var jobResolver = new GenericJobResolver(new[] { "Stackray.TestGenericCascadeCall.dll" }, false);
      var resolvedJobs = jobResolver.ResolveGenericJobs();
      jobResolver.Dispose();
      Assert.True(
        resolvedJobs.Count() == 1 &&
        (resolvedJobs.First() as GenericInstanceType).GenericArguments.Count == 1 &&
        (resolvedJobs.First() as GenericInstanceType).GenericArguments.First().Name == typeof(int).Name);
    }

    [Test]
    public void ResolveGenericSystemsTest() {
      var jobResolver = new GenericJobResolver(new[] { "Stackray.TestGenericSystems.dll" }, false);
      var resolvedJobs = jobResolver.ResolveGenericJobs();
      jobResolver.Dispose();
      Assert.True(
        resolvedJobs.Count() == 1 &&
        (resolvedJobs.First() as GenericInstanceType).GenericArguments.Count == 1 &&
        (resolvedJobs.First() as GenericInstanceType).GenericArguments.First().Name == typeof(int).Name);
    }

    [Test]
    public void ResolveNameTest() {
      var result = true;
      using (var assembly = AssemblyDefinition.ReadAssembly(AssembliesPath + "Stackray.TestGenericSystems.dll")) {
        var types = CecilTypeUtility.GetMethodDefinitions(assembly);
        var lookup = CecilTypeUtility.GetGenericMethodTypeLookup(new[] { assembly });

        foreach (var type in lookup.Keys) {
          if (!types.Contains(type)) {
            result = false;
            break;
          }
        }
      };
      Assert.True(result);
    }

    [Test]
    public void DetectGenericJobTest() {
      var jobCount = 0;
      using (var assembly = AssemblyDefinition.ReadAssembly(AssembliesPath + "Stackray.TestGenericJobs.dll")) {
        var genericJobTypes = GenericJobResolver.GetGenericJobCalls(assembly)
          .Select(c => CecilTypeUtility.GetType(c.Type))
          .ToArray();
        jobCount = genericJobTypes.Length;
      }
      Assert.True(jobCount == GenericJobs<bool, bool>.GENERIC_JOB_ENTRIES);
    }
  }
}
