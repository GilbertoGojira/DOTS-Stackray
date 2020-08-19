using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace Stackray.Transforms {
  public static class Extensions {
    public static NativeArray<SortedEntity> GetAllSortedEntities(this EntityManager entityManager, ComponentSystemBase system, Allocator allocator) {
      return entityManager.GetBuffer<SortedEntity>(system.GetSingletonEntity<SortedEntities>()).ToNativeArray(allocator);
    }

    public static void SetParent(this EntityManager entityManager, Entity parent, Entity child) {
      entityManager.AddComponentData(child, new Parent { Value = parent });
      entityManager.AddComponentData(child, new LocalToParent());
    }

    public static void SetParent(this EntityCommandBuffer entityCommandBuffer, Entity parent, Entity child) {
      entityCommandBuffer.AddComponent(child, new Parent { Value = parent });
      entityCommandBuffer.AddComponent(child, new LocalToParent());
    }

    public static void SetParent(this EntityCommandBuffer.ParallelWriter entityCommandBuffer, int jobIndex, Entity parent, Entity child) {
      entityCommandBuffer.AddComponent(jobIndex, child, new Parent { Value = parent });
      entityCommandBuffer.AddComponent(jobIndex, child, new LocalToParent());
    }

    public static JobHandle DestroyTransformHierarchy<T>(this ComponentSystemBase system, EntityQuery query, EntityCommandBuffer cmdBuffer, JobHandle inputDeps)
      where T : struct, IComponentData {

      return new DestroyTransformHierarchy<T> {
        ChunkEntityType = system.GetEntityTypeHandle(),
        ChildrenFromEntity = system.GetBufferFromEntity<Child>(true),
        CmdBuffer = cmdBuffer.AsParallelWriter()
      }.Schedule(query, inputDeps);
    }
  }
}
