using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Stackray.Transforms {
  [BurstCompile]
  struct DestroyTransformHierarchy<T> : IJobChunk
    where T : struct, IComponentData {

    [ReadOnly]
    public EntityTypeHandle ChunkEntityType;
    public EntityCommandBuffer.ParallelWriter CmdBuffer;
    [ReadOnly]
    public BufferFromEntity<Child> ChildrenFromEntity;

    public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
      var entities = chunk.GetNativeArray(ChunkEntityType);
      for (var i = 0; i < chunk.Count; ++i)
        DestroyHierarchy(CmdBuffer, entities[i], firstEntityIndex + i, ChildrenFromEntity);
    }

    void DestroyHierarchy(EntityCommandBuffer.ParallelWriter cmdBuffer, Entity entity, int index, BufferFromEntity<Child> childrenFromEntity) {
      cmdBuffer.DestroyEntity(index, entity);
      if (!childrenFromEntity.HasComponent(entity))
        return;
      var children = childrenFromEntity[entity];
      for (var i = 0; i < children.Length; ++i)
        DestroyHierarchy(cmdBuffer, children[i].Value, index, childrenFromEntity);
    }
  }
}
