using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Stackray.Entities {
  public class TypeUtility {

    public static List<Type> GetTypes(Type interfaceType) {
      var availableTypes = new List<Type>();
      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
        IEnumerable<Type> allTypes;
        try {
          allTypes = assembly.GetTypes();
        } catch (ReflectionTypeLoadException e) {
          allTypes = e.Types.Where(t => t != null);
        }
        availableTypes.AddRange(allTypes.Where(t => t.ImplementsInterface(interfaceType)));
      }
      return availableTypes;
    }

    public static object CreateInstance(Type baseType, Type genericType0, Type genericType1, params object[] constructorArgs) {
      if (baseType.GetGenericArguments().Length != 2)
        throw new ArgumentException($"Type {baseType} doesn't take 2 generic arguments");
      var contructorTypes = constructorArgs.Select(o => o.GetType()).ToArray();
      var constructor = baseType.MakeGenericType(genericType0, genericType1)
        .GetConstructor(contructorTypes);
      return constructor.Invoke(constructorArgs);
    }

    /// <summary>
    /// Extracts a generic type from an interface implemented by a certain type
    /// </summary>
    /// <param name="type">The type that implements the interface</param>
    /// <param name="interfaceType">the interface we want to extract the generic type</param>
    /// <param name="genericTypeIndex">the generic type index</param>
    /// <returns></returns>
    public static Type ExtractInterfaceGenericType(Type type, Type interfaceType, int genericTypeIndex) {
      if (!interfaceType.IsInterface)
        throw new ArgumentException($"Type {interfaceType} is not an interface");
      var foundGenericInterface = type.GetInterfaces().FirstOrDefault(
        t => t.IsGenericType && t.GetGenericTypeDefinition() == interfaceType);
      var genericType = foundGenericInterface.GetGenericArguments()[genericTypeIndex];
      return genericType;
    }

    public static IEnumerable<T> CreatePossibleInstances<T>(Type type, Type genericType, params object[] constructorArgs)
      where T : class {

      var contructorTypes = constructorArgs.Select(o => o.GetType())
        .ToArray();
      return MakeGenericTypes(
        type,
        genericType,
        t => t,
        t => t.GetInterface(genericType.FullName)
          .GenericTypeArguments.ElementAt(0))
        .Select(t => t.GetConstructor(contructorTypes).Invoke(constructorArgs) as T);
    }

    public static IEnumerable<Type> MakeGenericTypes(Type type, Type baseInterfaceTypeArgument, params Func<Type, Type>[] argumentSelectorArgs) {
      return GetTypes(baseInterfaceTypeArgument).Select(
        t => type.MakeGenericType(argumentSelectorArgs.Select(a => a.Invoke(t)).ToArray()));
    }
  }

  public static class TypeExtensions {
    public static bool ImplementsInterface(this Type type, Type i) {
      var interfaceTypes = type.GetInterfaces();

      if (i.IsGenericTypeDefinition) {
        foreach (var interfaceType in interfaceTypes) {
          if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == i)
            return true;
        }
      } else {
        foreach (var interfaceType in interfaceTypes) {
          if (interfaceType == i)
            return true;
        }
      }
      return false;
    }
  }
}
