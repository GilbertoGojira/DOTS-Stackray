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
    public void ResolveGenericMethodNameTest() {
      ResolveGenericMethodName("DummyCall", 0);
    }

    [Test]
    public void ResolveGenericMethodName1Test() {
      ResolveGenericMethodName("DummyCall", 1);
    }

    [Test]
    public void ResolveGenericMethodName2Test() {
      ResolveGenericMethodName("DummyCall", 2);
    }

    public void ResolveGenericMethodName(string methodName, int genericParamCount) {
      using (var assemblyDef = AssemblyDefinition.ReadAssembly(TestAssembly.Location)) {
        var nestedGenricMethod = CecilTypeUtility.GetMethodDefinitions(assemblyDef)
          .Where(m => m.GenericParameters.Count == genericParamCount)
          .First(m => m.Name.StartsWith(methodName));
        var fullName = CecilTypeUtility.GetGlobalFullName(nestedGenricMethod);
        var expected = $"System.Void Stackray.TestGenericJobs.GenericJobs`2::{methodName}{(genericParamCount > 0 ? $"`{genericParamCount}" : string.Empty)}()";
        Assert.True(fullName == expected);
      }
    }

    [Test]
    public void GetGenericJobsCallsTest() {
      var jobResolver = new GenericJobResolver(new[] { "Stackray.TestGenericJobs.dll" }, false);
      var genericJobs = jobResolver.GetGenericJobCalls();
      jobResolver.Dispose();
      Assert.True(genericJobs.Count() == GenericJobs<bool,bool>.GENERIC_UNIQUE_JOB_ENTRIES);
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
  }
}
