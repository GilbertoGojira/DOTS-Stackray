using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Stackray.Burst.Editor {

  class AssemblyResolver : BaseAssemblyResolver {
    Dictionary<string, AssemblyDefinition> m_usedAssemblyDefinitions;
    bool m_resolveAdditionalAssemblies;

    public IEnumerable<AssemblyDefinition> AssemblyDefinitions {
      get;
      private set;
    }

    public AssemblyResolver(IEnumerable<Assembly> assemblies, bool resolveAdditionalAssemblies = false) {
      m_resolveAdditionalAssemblies = resolveAdditionalAssemblies;
      var paths = assemblies.Where(a => !a.IsDynamic)
        .Select(a => Path.GetDirectoryName(a.Location));
      foreach (var path in paths)
        AddSearchDirectory(path);
      m_usedAssemblyDefinitions = assemblies.Select(a =>
        AssemblyDefinition.ReadAssembly(
          a.Location,
          new ReaderParameters {
            AssemblyResolver = this
          }))
          .ToDictionary(a => a.FullName, a => a);
      AssemblyDefinitions = m_usedAssemblyDefinitions.Values.ToArray();
    }

    public AssemblyDefinition AddAssembly(Assembly assembly, bool read = false, bool write = false) {
      if (m_usedAssemblyDefinitions.TryGetValue(assembly.FullName, out var assemblyDef))
        return assemblyDef;
      return AddAssembly(assembly.Location, read, write);
    }

    public AssemblyDefinition AddAssembly(string path, bool read = false, bool write = false) {
      var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters {
        ReadWrite = read,
        ReadSymbols = write,
        AssemblyResolver = this
      });
      AddSearchDirectory(path);
      if (m_usedAssemblyDefinitions.TryGetValue(assembly.FullName, out var usedAssembly)) {
        usedAssembly.Dispose();
        m_usedAssemblyDefinitions.Remove(usedAssembly.FullName);
      }

      m_usedAssemblyDefinitions.Add(
          assembly.FullName, assembly);
      return assembly;
    }

    public override AssemblyDefinition Resolve(AssemblyNameReference name) {
      if (m_usedAssemblyDefinitions.TryGetValue(name.FullName, out var assembly))
        return assembly;
      if (m_resolveAdditionalAssemblies) {
        var resolvedAssembly = AppDomain.CurrentDomain.GetAssemblies()
          .FirstOrDefault(a => a.FullName == name.FullName);
        if (resolvedAssembly != null)
          return AddAssembly(resolvedAssembly.Location);
      }
      return null;
    }

    protected override void Dispose(bool disposing) {
      base.Dispose(disposing);
      foreach (var assembly in m_usedAssemblyDefinitions.Values)
        assembly.Dispose();
      m_usedAssemblyDefinitions.Clear();
    }
  }
}
