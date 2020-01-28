using Stackray.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;

namespace Stackray.Renderer {

  public static class SpritePropertyAnimatorUtility {

    public static IEnumerable<ISpritePropertyAnimator> CreatePossibleInstances(JobComponentSystem system, EntityQuery query) {

      return TypeUtility.CreatePossibleInstances<ISpritePropertyAnimator>(
          typeof(SpritePropertyAnimator<,>),
          typeof(IDynamicBufferProperty<>),
          system,
          query).ToList();
    }

    public static IEnumerable<Type> CreatePossibleTypes() {

      return CreatePossibleTypes(
          typeof(SpritePropertyAnimator<,>),
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

    static IEnumerable<T> CreatePossibleInstances<T>(Type type, Type genericType, params object[] constructorArgs)
      where T : class {

      var contructorTypes = constructorArgs.Select(o => o.GetType())
        .ToArray();
      return CreatePossibleTypes(type, genericType)
        .Select(t => t.GetConstructor(contructorTypes).Invoke(constructorArgs) as T);
    }
  }
}