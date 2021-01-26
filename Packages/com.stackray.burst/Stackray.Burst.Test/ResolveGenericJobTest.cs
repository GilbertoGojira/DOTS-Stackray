using Mono.Cecil;
using NUnit.Framework;
using Stackray.Burst.Editor;
using Stackray.TestGenericJobs;
using System;
using System.IO;
using System.Linq;
using UnityEditor.Compilation;
using UnityEngine;

namespace Stackray.Burst.Test {

  public class ResolveGenericJob {

    static string AssembliesPath = Application.dataPath + "/../Library/ScriptAssemblies/";
    static System.Reflection.Assembly TestAssembly = System.Reflection.Assembly.LoadFile(AssembliesPath + "Stackray.TestGenericJobs.dll");

    struct StructWithGenericArgument<T1, T2>
      where T1 : struct, IComparable
      where T2 : struct { }

    [Test]
    public void TestValueTypeWithGenericArguments() {
      var isValueType = false;
      using (var assembly = AssemblyResolver
        .GetAssemblyDefinition(typeof(StructWithGenericArgument<,>).Assembly)) {
        var tr = assembly.GetTypeReference(typeof(StructWithGenericArgument<,>));
        isValueType =  
          tr.IsValueType() && 
          tr.GenericParameters.All(p => p.IsValueType());
      }
      Assert.True(isValueType);
    }

    [Test]
    public void GetGenericJobsCallsTest() {
      var assemblyPaths = CecilTypeUtility.GetAssemblyPaths(new[] { "Stackray.TestGenericJobs.dll" });
      var jobResolver = new GenericResolver(assemblyPaths);
      var genericJobs = jobResolver.GetGenericJobCalls();
      jobResolver.Dispose();
      Assert.True(genericJobs.Count() == GenericJobs<bool, bool>.GENERIC_UNIQUE_JOB_ENTRIES);
    }

    [Test]
    public void ResolveGenericJobsTest() {
      var assemblyPaths = CecilTypeUtility.GetAssemblyPaths(new[] { "Stackray.TestGenericJobs.dll" });
      var jobResolver = new GenericResolver(assemblyPaths);
      var resolvedJobs = jobResolver.ResolveGenericJobs();
      jobResolver.Dispose();
      Assert.True(resolvedJobs.Count() == GenericJobs<bool, bool>.CONCRETE_UNIQUE_JOB_ENTRIES);
    }

    [Test]
    public void GetTypesTest() {
      var paths = CecilTypeUtility.GetAssemblyPaths(new[] { "Stackray.TestGenericJobs.dll" })
        .ToArray();
      Assert.True(paths != null);
    }

    [Test]
    public void WriteNewAssemblyInjectionTest() {
      var writeAssemblyPath = AssembliesPath + "TestConcreteAssembly.dll";
      var assemblyPaths = CecilTypeUtility.GetAssemblyPaths(new[] { "Stackray.TestGenericJobs.dll" });
      var jobResolver = new GenericResolver(assemblyPaths);
      var resolvedJobs = jobResolver.ResolveGenericJobs();
      jobResolver.Dispose();
      var outputAssembly = CecilTypeUtility.CreateAssembly("TestConcreteJobs", resolvedJobs);
      outputAssembly.Write(writeAssemblyPath);

      var assembly = AssemblyDefinition.ReadAssembly(writeAssemblyPath);
      var methods = CecilTypeUtility.GetMethodDefinitions(assembly).Where(m => m.FullName.Contains("TestConcreteJobs"));
      assembly.Dispose();
      Assert.True(methods.Any());
    }

    [Test]
    public void ResolveFullDomainGenericJobsTest() {
      var assemblyPaths = CompilationPipeline.GetAssemblies(AssembliesType.Player)
        .Select(a => a.outputPath);
      var jobResolver = new GenericResolver(assemblyPaths);
      var resolvedJobs = jobResolver.ResolveGenericJobs();
      jobResolver.Dispose();
    }

    [Test]
    public void ResolveGenericCascadeCallTest() {
      var assemblyPaths = CecilTypeUtility.GetAssemblyPaths(new[] { "Stackray.TestGenericCascadeCall.dll" });
      var jobResolver = new GenericResolver(assemblyPaths);
      var resolvedJobs = jobResolver.ResolveGenericJobs();
      jobResolver.Dispose();
      Assert.True(
        resolvedJobs.Count() == 1 &&
        (resolvedJobs.First() as GenericInstanceType).GenericArguments.Count == 1 &&
        (resolvedJobs.First() as GenericInstanceType).GenericArguments.First().Name == typeof(int).Name);
    }

    [Test]
    public void ResolveGenericSystemsTest() {
      var assemblyPaths = CecilTypeUtility.GetAssemblyPaths(new[] { "Stackray.TestGenericSystems.dll" });
      var jobResolver = new GenericResolver(assemblyPaths);
      var resolvedJobs = jobResolver.ResolveGenericJobs();
      jobResolver.Dispose();
      Assert.True(
        resolvedJobs.Count() == 2 &&
        resolvedJobs.Any(job => (job as GenericInstanceType).GenericArguments.First().Name == typeof(int).Name) &&
        resolvedJobs.Any(job => (job as GenericInstanceType).GenericArguments.First().Name == typeof(double).Name));
    }

    [Test]
    public void ResolveGenericOverrideTest() {
      var assemblyPaths = CecilTypeUtility.GetAssemblyPaths(new[] { "Stackray.TestGenericOverride.dll" });
      var jobResolver = new GenericResolver(assemblyPaths);
      var resolvedJobs = jobResolver.ResolveGenericJobs();
      jobResolver.Dispose();
      Assert.True(
        resolvedJobs.Count() == 4 &&
        resolvedJobs.Any(job => (job as GenericInstanceType).GenericArguments.First().Name == typeof(bool).Name) &&
        resolvedJobs.Any(job => (job as GenericInstanceType).GenericArguments.First().Name == typeof(short).Name) &&
        resolvedJobs.Any(job => (job as GenericInstanceType).GenericArguments.First().Name == typeof(int).Name) &&
        resolvedJobs.Any(job => (job as GenericInstanceType).GenericArguments.First().Name == typeof(float).Name));
    }

    [Test]
    public void DetectGenericJobTest() {
      var jobCount = 0;
      var assemblyPath = CecilTypeUtility.GetAssemblyPaths(new[] { "Stackray.TestGenericJobs.dll" }).Single();
      using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath)) {
        var genericJobTypes = GenericResolver.GetGenericJobCalls(assembly)
          .Select(c => CecilTypeUtility.GetType(c.Type))
          .ToArray();
        jobCount = genericJobTypes.Length;
      }
      Assert.True(jobCount == GenericJobs<bool, bool>.GENERIC_JOB_ENTRIES);
    }
  }
}
