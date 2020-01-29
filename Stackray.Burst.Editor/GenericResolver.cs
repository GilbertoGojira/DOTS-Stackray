using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Jobs.LowLevel.Unsafe;

namespace Stackray.Burst.Editor {

  public class GenericResolver : IDisposable {

    public IEnumerable<AssemblyDefinition> Assemblies { get => m_resolver.AssemblyDefinitions; }

    AssemblyResolver m_resolver;

    private GenericResolver() { }

    public GenericResolver(IEnumerable<string> assemblyPaths, bool resolveAdditionalAssemblies = false) {
      m_resolver = new AssemblyResolver(assemblyPaths, resolveAdditionalAssemblies);
    }

    public GenericResolver(string assemblyPath) {
      m_resolver = new AssemblyResolver(Enumerable.Empty<string>());
      m_resolver.AddAssembly(assemblyPath, true, true);
    }

    public void Dispose() {
      m_resolver.Dispose();
    }

    public IEnumerable<TypeReference> AddTypes(string assemblyPath, string name, IEnumerable<Type> types) {
      return AddTypes(assemblyPath, name, types.Select(t => m_resolver.AddAssembly(t.Assembly.Location).MainModule.ImportReference(t)));
    }

    public IEnumerable<TypeReference> AddTypes(string assemblyPath, string name, IEnumerable<TypeReference> types) {
      var assembly = m_resolver.AddAssembly(assemblyPath, true, true);
      CecilTypeUtility.AddTypes(assembly, name, types);
      assembly.Write(new WriterParameters { WriteSymbols = true });
      return types;
    }

    public Dictionary<MethodDefinition, List<(GenericInstanceMethod, MethodDefinition)>> GetGenericMethodTypeLookup() {
      return CecilTypeUtility.GetGenericMethodTypeLookup(Assemblies);
    }

    public Dictionary<MethodDefinition, List<(MethodReference, MethodDefinition)>> GetMethodTypeLookup() {
      return CecilTypeUtility.GetMethodTypeLookup(Assemblies);
    }

    public IEnumerable<TypeReference> ResolveGenericJobs() {
      try {
        var genericJobCalls = Enumerable.Empty<CallReference>();
        foreach (var assembly in m_resolver.AssemblyDefinitions)
          genericJobCalls = genericJobCalls.Union(GetGenericJobCalls(assembly));
        return CecilTypeUtility.ResolveCalls(genericJobCalls, m_resolver.AssemblyDefinitions);
      } catch (Exception ex) {
        Dispose();
        throw ex;
      }
    }

    public IEnumerable<CallReference> GetGenericJobCalls() {
      return GetGenericJobCalls(Assemblies);
    }

    public IEnumerable<CallReference> GetGenericJobCalls(IEnumerable<AssemblyDefinition> assemblies) {
      return assemblies
        .SelectMany(a =>
          CecilTypeUtility.GetTypeDefinitions(a)
            .SelectMany(t => GetGenericJobCalls(t)))
        .ToArray();
    }

    public static IEnumerable<CallReference> GetGenericJobCalls(AssemblyDefinition assembly) {
      return CecilTypeUtility.GetTypeDefinitions(assembly)
        .SelectMany(t => GetGenericJobCalls(t))
        .GroupBy(c => c.Type.FullName + c.EntryMethod.FullName)
        .Select(g => g.First());
    }

    public static IEnumerable<CallReference> GetGenericJobCalls(TypeDefinition type) {
      return CecilTypeUtility.GetGenericInstanceCalls(type, inst => IsGenericJobImpl(inst));
    }

    public MethodDefinition GetMethodDefinition(Type type, string methodName) {
      return CecilTypeUtility.GetMethodDefinition(m_resolver.AddAssembly(type.Assembly), type, methodName);
    }

    public static bool IsGenericJobImpl(TypeReference typeRef) {
      if (typeRef?.IsGenericParameter ?? true)
        return false;
      var type = CecilTypeUtility.GetType(typeRef);
      return
        type != null &&
        IsGenericJobImpl(type);
    }

    public static bool IsGenericJobImpl(Type type) {
      return
        type.ContainsGenericParameters &&
        !type.IsInterface &&
        type.GetInterfaces()
        .Any(i => i.GetCustomAttributes(typeof(JobProducerTypeAttribute), false).Length > 0);
    }

    public static string[] InjectGenericJobs(IEnumerable<string> assemblyHints, string assemblyToInjectPath, string mainTypeName = "Concrete") {
      var resolver = new GenericResolver(assemblyHints);
      var resolvedJobs = resolver.ResolveGenericJobs();
      resolver.Dispose();

      resolver = new GenericResolver(Enumerable.Empty<string>());
      resolver.AddTypes(assemblyToInjectPath, mainTypeName, resolvedJobs);
      resolver.Dispose();
      return resolvedJobs.Select(t => t.FullName).ToArray();
    }

    public static string[] InjectTypes(IEnumerable<Type> types, string assemblyToInjectPath, string mainTypeName = "Concrete") {
      var resolver = new GenericResolver(Enumerable.Empty<string>());
      var typeReferences = resolver.AddTypes(assemblyToInjectPath, mainTypeName, types);
      resolver.Dispose();
      return typeReferences.Select(t => t.FullName).ToArray();
    }
  }
}
