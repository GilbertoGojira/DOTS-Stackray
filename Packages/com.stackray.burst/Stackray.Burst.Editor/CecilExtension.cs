using Mono.Cecil;
using System;
using System.Linq;

namespace Stackray.Burst.Editor {
  public static class CecilExtension {
    public static bool IsValueType(this TypeReference typeReference) {
      if (typeReference is GenericParameter &&
        (typeReference as GenericParameter).Constraints.Any(p => p.FullName == "System.ValueType"))
        return true;

      if (typeReference is ArrayType)
        return false;

      if (typeReference is PointerType)
        return false;

      if (typeReference is ByReferenceType)
        return false;

      if (typeReference is GenericParameter)
        return false;

      var pinnedType = typeReference as PinnedType;
      if (pinnedType != null)
        return pinnedType.ElementType.IsValueType();

      var requiredModifierType = typeReference as RequiredModifierType;
      if (requiredModifierType != null)
        return requiredModifierType.ElementType.IsValueType();

      var optionalModifierType = typeReference as OptionalModifierType;
      if (optionalModifierType != null)
        return optionalModifierType.ElementType.IsValueType();

      var typeDefinition = typeReference.Resolve();

      if (typeDefinition == null)
        throw new InvalidOperationException($"Unable to locate the definition for {typeReference.FullName}. Is this assembly compiled against an older version of one of its dependencies?");

      return typeDefinition.IsValueType;
    }

    public static TypeReference GetTypeReference(this AssemblyDefinition assemblyDef, Type type) =>
      assemblyDef.Modules
      .Select(m => m.GetType(type.FullName, true))
      .FirstOrDefault();
  }
}
