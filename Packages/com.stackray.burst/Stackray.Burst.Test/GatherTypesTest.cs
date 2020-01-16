using Mono.Cecil;
using NUnit.Framework;
using Stackray.Burst.Editor;
using Stackray.TestAssembly;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Stackray.Burst.Test {

  public class GatherTypesTest {

    static Assembly TestAssembly = Assembly.LoadFile(Application.dataPath + "/../Library/ScriptAssemblies/Stackray.TestAssembly.dll");

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
  }
}
