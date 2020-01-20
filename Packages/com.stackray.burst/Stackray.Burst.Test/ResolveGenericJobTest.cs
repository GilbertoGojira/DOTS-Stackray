using Mono.Cecil;
using NUnit.Framework;
using Stackray.Burst.Editor;
using Stackray.TestAssembly;
using System;
using System.IO;
using System.Linq;
using UnityEditor.Compilation;
using UnityEngine;

namespace Stackray.Burst.Test {

  public class ResolveGenericJobTest {

    static string AssembliesPath = Application.dataPath + "/../Library/ScriptAssemblies/";
    static System.Reflection.Assembly TestAssembly = System.Reflection.Assembly.LoadFile(AssembliesPath + "Stackray.TestAssembly.dll");

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
        var expected = $"System.Void Stackray.TestAssembly.GenericJobs`2::{methodName}{(genericParamCount > 0 ? $"`{genericParamCount}" : string.Empty)}()";
        Assert.True(fullName == expected);
      }
    }

    [Test]
    public void GetGenericJobsCallsTest() {
      var jobResolver = new GenericJobResolver(new[] { "Stackray.TestAssembly" }, false);
      var genericJobs = jobResolver.GetGenericJobCalls();
      jobResolver.Dispose();
      Assert.True(genericJobs.Count() == GenericJobs<bool,bool>.GENERIC_UNIQUE_JOB_ENTRIES);
    }

    [Test]
    public void ResolveJobCallsTest() {
      var jobResolver = new GenericJobResolver(new[] { "Stackray.TestAssembly" }, false);
      var allGenericJobCallers = jobResolver.ResolveJobCalls();
      jobResolver.Dispose();
      Assert.True(allGenericJobCallers.Count() == GenericJobs<bool, bool>.GENERIC_JOB_ENTRIES);
    }

    [Test]
    public void ResolveGenericJobsTest() {
      var jobResolver = new GenericJobResolver(new[] { "Stackray.TestAssembly" }, false);
      var resolvedJobs = jobResolver.ResolveGenericJobs();
      jobResolver.Dispose();
      Assert.True(resolvedJobs.Count() == GenericJobs<bool, bool>.CONCRETE_UNIQUE_JOB_ENTRIES);
    }

    [Test]
    public void GetTypesTest() {
      var types = CecilTypeUtility.GetAssemblies(new[] { "Stackray.TestAssembly" }, false)
        .SelectMany(a => a.GetTypes())
        .ToArray();
      Assert.True(types != null);
    }

    [Test]
    public void WriteNewAssemblyInjectionTest() {
      var assemblyPath = AssembliesPath + "TestConcreteAssembly.dll";
      var jobResolver = new GenericJobResolver(new[] { "Stackray.TestAssembly" }, false);
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
  }
}
