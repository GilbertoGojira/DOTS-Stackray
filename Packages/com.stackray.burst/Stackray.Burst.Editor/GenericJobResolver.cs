using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Jobs.LowLevel.Unsafe;

namespace Stackray.Burst.Editor {

  public class GenericJobResolver : IDisposable {

    AssemblyResolver m_resolver;

    private GenericJobResolver() { }

    public GenericJobResolver(IEnumerable<string> assemblyHints, bool exclude = true) {
      m_resolver = new AssemblyResolver(CecilTypeUtility.GetAssemblies(assemblyHints, exclude));
    }

    public void Dispose() {
      m_resolver.Dispose();
    }

    public void AddTypes(string assemblyPath, string name, IEnumerable<TypeReference> types) {
      var assembly = m_resolver.AddAssembly(assemblyPath);
      CecilTypeUtility.AddTypes(assembly, name, types);
      assembly.Write(new WriterParameters { WriteSymbols = true });
    }

    public IEnumerable<TypeReference> ResolveGenericJobs() {
      var genericJobCalls = ResolveJobCalls();
      return CecilTypeUtility.ResolveGenericTypes(genericJobCalls, m_resolver.AssemblyDefinitions)
        .GroupBy(t => t.FullName)
        .Select(g => g.First());
    }

    public IEnumerable<CallReference> ResolveJobCalls() {
      var genericJobCalls = Enumerable.Empty<CallReference>();
      foreach (var assembly in m_resolver.AssemblyDefinitions)
        genericJobCalls = genericJobCalls.Union(GetGenericJobCalls(assembly)).ToArray();
      return CecilTypeUtility.ResolveCalls(genericJobCalls, m_resolver.AssemblyDefinitions);
    }

    public IEnumerable<CallReference> GetGenericJobCalls() {
      return m_resolver.AssemblyDefinitions
        .SelectMany(a =>
          CecilTypeUtility.GetTypeDefinitions(a)
            .SelectMany(t => GetGenericJobCalls(t)))
        .ToArray();
    }

    public static IEnumerable<CallReference> GetGenericJobCalls(AssemblyDefinition assembly) {
      return CecilTypeUtility.GetTypeDefinitions(assembly)
        .SelectMany(t => GetGenericJobCalls(t))
        .ToArray();
    }

    public static IEnumerable<CallReference> GetGenericJobCalls(TypeDefinition type) {
      return CecilTypeUtility.GetGenericInstanceCalls(type, inst => IsGenericJobImpl(inst));
    }

    public static bool IsGenericJobImpl(Type type) {
      return type.ContainsGenericParameters && IsJobImpl(type);
    }

    public static bool IsGenericJobImpl(TypeReference typeRef) {
      if (typeRef?.IsGenericParameter ?? true)
        return false;
      var type = CecilTypeUtility.GetType(typeRef);
      return
        type != null &&
        type.ContainsGenericParameters &&
        IsJobImpl(type);
    }

    public static bool IsJobImpl(Type type) {
      return
        !type.IsInterface &&
        type.GetInterfaces()
        .Any(i => i.GetCustomAttributes(typeof(JobProducerTypeAttribute), false).Length > 0);
    }
  }
}
