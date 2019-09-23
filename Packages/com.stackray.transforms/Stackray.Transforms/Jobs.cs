using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Stackray.Transforms {
  [BurstCompile]
  public struct DestroyTransformHierarchy<T> : IJobForEachWithEntity<T>
    where T : struct, IComponentData {

    public EntityCommandBuffer.Concurrent CmdBuffer;
    [ReadOnly]
    public BufferFromEntity<Child> ChildrenFromEntity;

    public void Execute(Entity entity, int index, [ReadOnly]ref T _) {
      DestroyHierarchy(CmdBuffer, entity, index, ChildrenFromEntity);
    }

    void DestroyHierarchy(EntityCommandBuffer.Concurrent cmdBuffer, Entity entity, int index, BufferFromEntity<Child> childrenFromEntity) {
      cmdBuffer.DestroyEntity(index, entity);
      if (!childrenFromEntity.Exists(entity))
        return;
      var children = childrenFromEntity[entity];
      for (var i = 0; i < children.Length; ++i)
        DestroyHierarchy(cmdBuffer, children[i].Value, index, childrenFromEntity);
    }
  }
}
