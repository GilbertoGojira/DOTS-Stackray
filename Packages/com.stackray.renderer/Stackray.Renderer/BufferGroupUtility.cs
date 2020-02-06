using Stackray.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Entities;

namespace Stackray.Renderer {

  public static class BufferGroupUtility {
    public static Dictionary<Type, string> GetAvailableBufferProperties(Type componentType, Func<object, string> bufferNameProperty) {
      var bufferTypes = new Dictionary<Type, string>();
      var availableTypes = new List<Type>();
      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
        IEnumerable<Type> allTypes;
        try {
          allTypes = assembly.GetTypes();
        } catch (ReflectionTypeLoadException e) {
          allTypes = e.Types.Where(t => t != null);
        }
        availableTypes.AddRange(allTypes.Where(t => t.IsValueType && t.ImplementsInterface(componentType)));
      }

      foreach (var type in availableTypes) {
        var propertyInterface = type.GetInterfaces().FirstOrDefault(
          t => t.IsGenericType && t.GetGenericTypeDefinition() == componentType);
        var argType = propertyInterface.GetGenericArguments().Length > 0 &&
          propertyInterface.GetGenericArguments()[0].ImplementsInterface(typeof(IComponentData)) ? propertyInterface.GetGenericArguments()[0] : type;
        var instance = Activator.CreateInstance(type);
        var bufferName = bufferNameProperty.Invoke(instance);
        bufferTypes.Add(argType, bufferName);
      }
      return bufferTypes;
    }

    public static IEnumerable<Type> GetFixedBufferProperties() {
      return AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a => a.GetTypes())
        .Where(t => t.IsValueType && t.ImplementsInterface(typeof(IFixedBufferProperty<>)));
    }

    public static IEnumerable<Type> CreatePossibleTypes() {

      return CreatePossibleTypes(
          typeof(ComponentDataValueBufferGroup<,>),
          typeof(IDynamicBufferProperty<>));
    }

    static IEnumerable<Type> CreatePossibleTypes(Type type, Type genericType) {
      return TypeUtility.MakeGenericTypes(
        type,
        genericType,
        t => t,
        t => t.GetInterface(genericType.FullName)
        .GenericTypeArguments.ElementAt(0));
    }
  }
}
