using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MethodBody = Mono.Cecil.Cil.MethodBody;

namespace Stackray.Burst.Editor {

  public struct CallReference {
    public GenericInstanceType Type;
    public MethodDefinition EntryMethod;

    public override string ToString() {
      return $"{Type?.FullName} With Entry -> {EntryMethod?.Name}";
    }
  }

  public static class CecilTypeUtility {

    public static string GetGlobalFullName(MethodReference methodReference) {
      return ResolveName(
        (methodReference.HasGenericParameters ? new GenericInstanceMethod(methodReference) as MethodReference : methodReference).FullName,
        (methodReference as GenericInstanceMethod)?.GenericArguments.Count ?? methodReference.GenericParameters.Count);
    }

    public static string GetGlobalFullName(GenericInstanceMethod genericInstanceMethod) {
      return ResolveName(
        genericInstanceMethod.FullName,
        genericInstanceMethod.GenericArguments.Count);
    }

    static string ResolveName(string value, int paramCount) {
      var parts = value.Split(new string[] { "::" }, StringSplitOptions.None);
      for (var i = 0; i < parts.Length - 1; ++i)
        parts[i] = ReplaceGenerics(parts[i], string.Empty);
      parts[parts.Length - 1] = ReplaceGenerics(parts.Last(), $"`{paramCount}");
      return string.Join("::", parts);
    }

    static string ReplaceGenerics(string value, string replace) {
      var regex = new Regex(@"(\<([^()]*)\>)");
      MatchCollection matches = regex.Matches(value);
      foreach (Match match in matches) {
        var matchValue = match.Groups[1].Value;
        if (string.IsNullOrEmpty(matchValue))
          continue;
        value = value.Replace(matchValue, replace);
      }
      return value;
    }

    public static IEnumerable<Assembly> GetAssemblies(IEnumerable<string> keywords, bool exclude = true) {
      return AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => keywords.Any(ex => (a.FullName.IndexOf(ex, StringComparison.InvariantCultureIgnoreCase) >= 0) != exclude));
    }

    public static void AddType(Assembly assembly, TypeDefinition typeDefinition) {
      var assemblyDef = AssemblyDefinition.ReadAssembly(assembly.Location);
      assemblyDef.MainModule.Types.Add(typeDefinition);
      assemblyDef.Write();
    }

    public static IEnumerable<TypeDefinition> GetTypeDefinitions(AssemblyDefinition assembly) {
      return assembly.MainModule.Types
      .SelectMany(t => new[] { t }.Union(t.NestedTypes))
      .Where(t => t.Name != "<Module>" || t.Name.Contains("AnonymousType"))
      .ToArray();
    }

    public static IEnumerable<MethodDefinition> GetMethodDefinitions(AssemblyDefinition assembly) {
      return assembly.MainModule.Types
      .SelectMany(t => new[] { t }.Union(t.NestedTypes))
      .Where(t => t.Name != "<Module>" || t.Name.Contains("AnonymousType"))
      .SelectMany(t => t.Methods)
      .ToArray();
    }

    public static IEnumerable<CallReference> GetGenericInstanceCalls(TypeDefinition type, Func<GenericInstanceType, bool> predicate) {
      var result = new HashSet<CallReference>();
      foreach (var method in type.Methods.Where(m => m.Body != null && (m.ContainsGenericParameter || type.HasGenericParameters)))
        foreach (var genericJob in GetGenericInstanceTypes(method.Body, predicate))
          result.Add(new CallReference {
            Type = genericJob,
            EntryMethod = method
          });
      return result;
    }

    static IEnumerable<GenericInstanceType> GetGenericInstanceTypes(MethodBody methodBody, Func<GenericInstanceType, bool> predicate) {
      return methodBody.Instructions
        .Where(i => i.Operand is GenericInstanceType && predicate.Invoke(i.Operand as GenericInstanceType))
        .Select(i => i.Operand as GenericInstanceType);
    }

    static TypeReference GetNestedRootType(TypeReference type) {
      if (!type.IsNested)
        return type;
      return GetNestedRootType(type.DeclaringType);
    }

    static Dictionary<string, List<(GenericInstanceMethod, MethodDefinition)>> GetMethodLookup(IEnumerable<AssemblyDefinition> assemblies) {
      var callerTree = new Dictionary<string, List<(GenericInstanceMethod, MethodDefinition)>>();
      foreach (var assembly in assemblies)
        GetMethodLookup(callerTree, assembly);
      return callerTree;
    }

    static void GetMethodLookup(Dictionary<string, List<(GenericInstanceMethod, MethodDefinition)>> lookup, AssemblyDefinition assembly) {
      foreach (var type in GetTypeDefinitions(assembly))
        GetMethodLookup(lookup, type);
    }

    static void GetMethodLookup(Dictionary<string, List<(GenericInstanceMethod, MethodDefinition)>> lookup, TypeDefinition type) {
      foreach (var method in type.Methods) {
        var body = method.Body;
        if (body == null)
          continue;

        foreach (var instruction in body.Instructions) {
          if (instruction.Operand is GenericInstanceMethod) {
            var genericInst = instruction.Operand as GenericInstanceMethod;
            var key = GetGlobalFullName(genericInst);
            if (!lookup.TryGetValue(key, out var methods)) {
              methods = new List<(GenericInstanceMethod, MethodDefinition)>();
              lookup.Add(key, methods);
            }
            methods.Add((genericInst, method));
          }
        }
      }
    }

    public static IEnumerable<CallReference> ResolveCalls(IEnumerable<CallReference> calls, IEnumerable<AssemblyDefinition> assemblies) {
      var methodLookup = GetMethodLookup(assemblies);
      var result = Enumerable.Empty<CallReference>();
      foreach (var call in calls)
        result = result.Concat(ResolveCall(methodLookup, call)).ToArray();
      return result;
    }

    static IEnumerable<CallReference> ResolveCall(Dictionary<string, List<(GenericInstanceMethod, MethodDefinition)>> methodLookup, CallReference callReference) {
      var res = new List<CallReference>();
      var key = GetGlobalFullName(callReference.EntryMethod);
      if (methodLookup.TryGetValue(key, out var methods)) {
        foreach (var (inst, method) in methods)
          res.AddRange(
            ResolveCall(methodLookup, new CallReference {
              Type = ResolveGenericType(inst, callReference.Type) as GenericInstanceType,
              EntryMethod = method
            }));
        return res;
      }
      return new[] { callReference };
    }

    #region Get Hierarchy
    static IEnumerable<TypeReference> GetTypeHierarchy(TypeReference type, TypeReference baseType) {
      return new[] { baseType }
      .Concat(GetHierarchy(type, GetNestedRootType(baseType)));
    }

    static IEnumerable<TypeReference> GetHierarchy(TypeReference type, TypeReference baseType) {
      var result = new[] { type };
      if (type == null || type.Resolve().FullName == baseType.FullName || GetNestedRootType(type).FullName == GetNestedRootType(baseType).FullName)
        return result;
      return GetHierarchy(type.Resolve().BaseType, baseType)
        .Concat(result);
    }
    #endregion Get Hierarchy

    #region Resolve Generic Types

    public static IEnumerable<TypeReference> ResolveGenericTypes(IEnumerable<CallReference> types, IEnumerable<AssemblyDefinition> assemblies) {
      var result = Enumerable.Empty<TypeReference>();
      foreach (var assembly in assemblies)
        result = result.Concat(ResolveGenericTypes(types, assembly));
      return result;
    }

    static IEnumerable<TypeReference> ResolveGenericTypes(IEnumerable<CallReference> types, AssemblyDefinition assembly) {
      var possibleConcreteTypes = GetTypeDefinitions(assembly)
        .Where(t => t.IsClass && t.BaseType.IsGenericInstance && !t.HasGenericParameters)
        .Select(t => t.BaseType as GenericInstanceType)
        .ToArray();
      var result = new HashSet<TypeReference>();
      foreach (var type in types)
        foreach (var concreteType in possibleConcreteTypes)
          result.Add(ResolveGenericType(concreteType, type));
      return result;
    }

    static TypeReference ResolveGenericType(TypeReference type, CallReference callReference) {
      var baseType = callReference.Type;
      if (baseType.DeclaringType == null) {
        var resolveBase = ResolveGenericType(type, callReference.EntryMethod.DeclaringType);
        var genericArguments = ResolveGenericArgumentTypes(resolveBase, baseType);
        for (var i = 0; i < genericArguments.Count(); ++i)
          baseType.GenericArguments[i] = genericArguments.ElementAt(i);
        return baseType;
      }
      return ResolveGenericType(type, baseType);
    }

    static TypeReference ResolveGenericType(TypeReference type, TypeReference baseType) {
      var genericInst = CreateGenericInstanceType(baseType);
      var argumentsHierarchy = GetTypeHierarchy(type, baseType)
        .Select(t => GetGenericArguments(t));
      var genericArguments = ResolveGenericArgumentTypes(argumentsHierarchy);
      for (var i = 0; i < genericArguments.Count(); ++i)
          genericInst.GenericArguments[i] = genericArguments.ElementAt(i);
      return genericInst;
    }

    static TypeReference ResolveGenericType(MethodReference method, TypeReference baseType) {
      var genericInst = CreateGenericInstanceType(baseType);
      if (genericInst == null || method == null)
        return baseType;
      var genericArguments = ResolveGenericArgumentTypes(GetGenericArguments(method), GetGenericArguments(genericInst));
      for (var i = 0; i < genericArguments.Count(); ++i)
        genericInst.GenericArguments[i] = genericArguments.ElementAt(i);
      return genericInst;
    }

    static TypeReference ResolveGenericType(IEnumerable<TypeReference> argumentTypes, TypeReference baseType) {
      var genericInst = CreateGenericInstanceType(baseType);
      var genericArguments = ResolveGenericArgumentTypes(argumentTypes, GetGenericArguments(baseType));
      for (var i = 0; i < genericArguments.Count(); ++i)
        genericInst.GenericArguments[i] = genericArguments.ElementAt(i);
      return genericInst;
    }

    #endregion Resolve Generic Types

    #region Resolve Generic Argument Types
    static IEnumerable<TypeReference> ResolveGenericArgumentTypes(IEnumerable<IEnumerable<TypeReference>> argumentHierarchy) {
      var resolvedArguments = argumentHierarchy.First();
      for (var i = 1; i < argumentHierarchy.Count(); ++i)
        resolvedArguments = ResolveGenericArgumentTypes(argumentHierarchy.ElementAt(i), resolvedArguments);
      return resolvedArguments;
    }

    static IEnumerable<TypeReference> ResolveGenericArgumentTypes(TypeReference type, TypeReference baseType) {
      return ResolveGenericArgumentTypes(GetGenericArguments(type), GetGenericArguments(baseType));
    }

    static IEnumerable<TypeReference> ResolveGenericArgumentTypes(IEnumerable<TypeReference> argumentTypes, IEnumerable<TypeReference> baseArgumentTypes) {
      var result = new List<TypeReference>();
      for (var i = 0; i < baseArgumentTypes.Count(); ++i) {
        var genericParameter = baseArgumentTypes.ElementAt(i);

        if (genericParameter is GenericParameter) {
          var resolvedParameter =
              ResolveGenericParameter(genericParameter as GenericParameter, argumentTypes, baseArgumentTypes);
          if (resolvedParameter != null)
            result.Add(resolvedParameter);
          else
            return Enumerable.Empty<TypeReference>();

        } else if (genericParameter is GenericInstanceType) {
          var resolvedParameter = ResolveGenericType(argumentTypes, genericParameter);
          if (resolvedParameter != null)
            result.Add(resolvedParameter);
          else
            return Enumerable.Empty<TypeReference>();

        } else
          result.Add(genericParameter);
      }
      return result;
    }

    static TypeReference ResolveGenericParameter(GenericParameter genericParameter, IEnumerable<TypeReference> argumentTypes, IEnumerable<TypeReference> baseArgumentTypes) {
      var baseMethodTypesOffset = baseArgumentTypes
        .FirstOrDefault(a => a is GenericParameter)?.DeclaringType?.GenericParameters.Count() ??
        argumentTypes
        .FirstOrDefault(a => a is GenericParameter)?.DeclaringType?.GenericParameters.Count() ?? 0;
      var pos = genericParameter.Position;
      if (genericParameter.Type == GenericParameterType.Type && pos < argumentTypes.Count())
        return argumentTypes.ElementAt(pos);
      else if (genericParameter.Type == GenericParameterType.Method && baseMethodTypesOffset + pos < argumentTypes.Count())
        return argumentTypes.ElementAt(baseMethodTypesOffset + pos);
      return null;
    }

    #endregion Resolve Generic Argument Types

    static IEnumerable<TypeReference> GetGenericArguments(MemberReference method) {
      return (method?.DeclaringType as GenericInstanceType)?.GenericArguments
        .Concat((method as GenericInstanceMethod)?.GenericArguments ?? Enumerable.Empty<TypeReference>());
    }

    static IEnumerable<TypeReference> GetGenericArguments(TypeReference type) {
      return (type as GenericInstanceType)?.GenericArguments ?? 
        type?.GenericParameters ?? Enumerable.Empty<TypeReference>();
    }

    static GenericInstanceType CreateGenericInstanceType(TypeReference instance) {
      if (instance == null)
        return null;
      var newInstance = new GenericInstanceType(instance.Resolve());
      foreach (var arg in GetGenericArguments(instance))
        newInstance.GenericArguments.Add(arg);
      return newInstance;
    }

    public static Type GetType(TypeReference type) {
      var reflectedName = GetReflectionName(type);
      return Type.GetType(reflectedName, false);
    }

    static string GetReflectionName(TypeReference type) {
      if (type.IsGenericInstance) {
        var nameSpace = GetNameSpace(type);
        var declaringName = type.DeclaringType?.FullName ?? type.Namespace;
        declaringName = declaringName.Replace('/', '+');
        var nested = type.DeclaringType != null ? "+" : ".";
        return string.Format("{0}{1}{2}, {3}",
          declaringName,
          nested,
          type.Name,
          nameSpace);
      }
      return type.FullName;
    }

    static string GetNameSpace(TypeReference type) {
      return string.IsNullOrEmpty(type.Namespace) ? GetNameSpace(type.DeclaringType) : type.Namespace;
    }
  }
}
