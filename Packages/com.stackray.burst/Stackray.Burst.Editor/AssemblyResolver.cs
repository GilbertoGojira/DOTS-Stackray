using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Stackray.Burst.Editor {

  class AssemblyResolver : BaseAssemblyResolver {
    Dictionary<string, AssemblyDefinition> m_usedAssemblyDefinitions;

    public IEnumerable<AssemblyDefinition> AssemblyDefinitions {
      get => m_usedAssemblyDefinitions.Values.ToArray();
    }

    public AssemblyResolver(IEnumerable<Assembly> assemblies) {
      var paths = assemblies.Select(a => Path.GetDirectoryName(a.Location));
      foreach (var path in paths)
        AddSearchDirectory(path);
      m_usedAssemblyDefinitions = assemblies.Select(a =>
        AssemblyDefinition.ReadAssembly(
          a.Location,
          new ReaderParameters {
            AssemblyResolver = this
          }))
          .ToDictionary(a => a.FullName, a => a);
    }

    public AssemblyDefinition AddAssembly(string path) {
      var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters {
        ReadWrite = true,
        ReadSymbols = true,
        AssemblyResolver = this
      });
      AddSearchDirectory(path);
      m_usedAssemblyDefinitions.Add(
          path, assembly);
      return assembly;
    }

    public override AssemblyDefinition Resolve(AssemblyNameReference name) {
      if (m_usedAssemblyDefinitions.TryGetValue(name.FullName, out var assembly))
        return assembly;
      assembly = base.Resolve(name);
      m_usedAssemblyDefinitions.Add(name.FullName, assembly);
      return assembly;
    }

    protected override void Dispose(bool disposing) {
      base.Dispose(disposing);
      foreach (var assembly in m_usedAssemblyDefinitions.Values)
        assembly.Dispose();
      m_usedAssemblyDefinitions.Clear();
    }
  }
}
