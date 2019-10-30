using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Stackray.Transforms {
  public class TransformUtility {

    public static NativeList<T> GetComponentDataFromChildren<T>(Entity entity, BufferFromEntity<Child> childrenFromEntity, ComponentDataFromEntity<T> componentFromEntity, int maxDepth = -2)
        where T : struct, IComponentData {
      var result = new NativeList<T>(Allocator.Temp);
      GetComponentDataFromChildren(entity, childrenFromEntity, componentFromEntity, result, maxDepth);
      return result;
    }

    static void GetComponentDataFromChildren<T>(Entity entity, BufferFromEntity<Child> childrenFromEntity, ComponentDataFromEntity<T> componentFromEntity, NativeList<T> result, int depth)
        where T : struct, IComponentData {
      if (depth == -1)
        return;
      if (componentFromEntity.Exists(entity))
        result.Add(componentFromEntity[entity]);
      if (!childrenFromEntity.Exists(entity))
        return;

      var children = childrenFromEntity[entity];
      for (var i = 0; i < children.Length; ++i)
        GetComponentDataFromChildren(children[i].Value, childrenFromEntity, componentFromEntity, result, depth - 1);
    }

    public static NativeList<T> GetComponentDataFromParents<T>(Entity entity, ComponentDataFromEntity<Parent> parentsFromEntity, ComponentDataFromEntity<T> componentFromEntity, int maxDepth = -2)
    where T : struct, IComponentData {
      var result = new NativeList<T>(Allocator.Temp);
      GetComponentDataFromParents(entity, parentsFromEntity, componentFromEntity, result, maxDepth);
      return result;
    }

    static void GetComponentDataFromParents<T>(Entity entity, ComponentDataFromEntity<Parent> parentsFromEntity, ComponentDataFromEntity<T> componentFromEntity, NativeList<T> result, int depth)
        where T : struct, IComponentData {
      if (depth == -1)
        return;
      if (componentFromEntity.Exists(entity))
        result.Add(componentFromEntity[entity]);
      if (!parentsFromEntity.Exists(entity))
        return;
      GetComponentDataFromParents(parentsFromEntity[entity].Value, parentsFromEntity, componentFromEntity, result, depth - 1);
    }

    public static int GetDepth(Entity entity, ComponentDataFromEntity<Parent> parentFromEntity) {
      if (!parentFromEntity.Exists(entity))
        return 0;
      else
        return GetDepth(parentFromEntity[entity].Value, parentFromEntity) + 1;
    }

    /// <summary>
    /// Gets the root entity in the transform hierarchy
    /// </summary>
    /// <returns>The root entity.</returns>
    /// <param name="entity">Entity.</param>
    /// <param name="parentFromEntity">Parent from entity.</param>
    public static Entity GetRootEntity(Entity entity, ComponentDataFromEntity<Parent> parentFromEntity) {
      if (!parentFromEntity.Exists(entity))
        return entity;
      else
        return GetRootEntity(parentFromEntity[entity].Value, parentFromEntity);
    }

    /// <summary>
    /// Gets first entity that has the component T it upward hierarchy
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="entity"></param>
    /// <param name="parentFromEntity"></param>
    /// <param name="componentFromEntity"></param>
    /// <returns></returns>
    public static Entity GetRootEntity<T>(
      Entity entity, ComponentDataFromEntity<Parent> parentFromEntity, ComponentDataFromEntity<T> componentFromEntity)
      where T : struct, IComponentData {

      if (!parentFromEntity.Exists(entity) || componentFromEntity.Exists(entity))
        return entity;
      else
        return GetRootEntity(parentFromEntity[entity].Value, parentFromEntity, componentFromEntity);
    }

    public static Entity GetRootEntity<T>(
      Entity entity, ComponentDataFromEntity<Parent> parentFromEntity, BufferFromEntity<T> bufferFromEntity)
      where T : struct, IBufferElementData {

      if (!parentFromEntity.Exists(entity) || bufferFromEntity.Exists(entity))
        return entity;
      else
        return GetRootEntity(parentFromEntity[entity].Value, parentFromEntity, bufferFromEntity);
    }

    /// <summary>
    /// Tries the get compoment in parents.
    /// </summary>
    /// <returns><c>true</c>, if get compoment in parents was tryed, <c>false</c> otherwise.</returns>
    /// <param name="entity">Entity.</param>
    /// <param name="parentFromEntity">Parent from entity.</param>
    /// <param name="componentFromEntity">Component from entity.</param>
    /// <param name="foundEntity">Found entity.</param>
    /// <param name="foundComponent">Found component.</param>
    /// <typeparam name="T">The 1st type parameter.</typeparam>
    public static bool TryGetCompomentInParents<T>(
      Entity entity,
      ComponentDataFromEntity<Parent> parentFromEntity,
      ComponentDataFromEntity<T> componentFromEntity,
      out Entity foundEntity,
      out T foundComponent) where T : struct, IComponentData {

      foundEntity = Entity.Null;
      foundComponent = default;
      if (!parentFromEntity.Exists(entity) && !componentFromEntity.Exists(entity))
        return false;
      if (!parentFromEntity.Exists(entity) || componentFromEntity.Exists(entity)) {
        foundEntity = entity;
        foundComponent = componentFromEntity[entity];
        return true;
      }
      return TryGetCompomentInParents(parentFromEntity[entity].Value, parentFromEntity, componentFromEntity, out foundEntity, out foundComponent);
    }

    public static bool ExistsInHierarchy<T>(
      Entity entity, 
      BufferFromEntity<Child> childrenFromEntity,
      ComponentDataFromEntity<T> componentFromEntity, 
      bool includeRoot = false)
      where T : struct, IComponentData {

      if (includeRoot && componentFromEntity.Exists(entity))
        return true;
      var children = childrenFromEntity[entity];
      for (var i = 0; i < children.Length; ++i)
        if (ExistsInHierarchy(children[i].Value, childrenFromEntity, componentFromEntity, true))
          return true;
      return false;
    }

    public static bool ExistsInHierarchy<T>(
      Entity entity, 
      ComponentDataFromEntity<Parent> parentFromEntity,
      ComponentDataFromEntity<T> componentFromEntity,
      bool includeRoot = false)
      where T : struct, IComponentData {

      if (includeRoot && componentFromEntity.Exists(entity))
        return true;
      if (!parentFromEntity.Exists(entity))
        return false;
      return ExistsInHierarchy(parentFromEntity[entity].Value, parentFromEntity, componentFromEntity, true);
    }
  }
}
