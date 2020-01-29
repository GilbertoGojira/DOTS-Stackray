using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Stackray.Transforms {
  class UpdateDisabledSystem : JobComponentSystem {

    BeginInitializationEntityCommandBufferSystem m_entityCommandBufferSystem;
    EntityQuery m_allActivatableQuery;

    protected override void OnCreate() {
      base.OnCreate();
      m_entityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
      m_allActivatableQuery = GetEntityQuery(
        new EntityQueryDesc {
          All = new ComponentType[] { ComponentType.ReadOnly<Active>() },
          Options = EntityQueryOptions.IncludeDisabled
        });
    }

    // TODO: Enable burst when EntityCommandBuffers are supported by burst
    //[BurstCompile]
    struct UpdateDisabled : IJobForEachWithEntity<Active> {
      public EntityCommandBuffer.Concurrent CommandBuffer;
      [ReadOnly]
      public ComponentDataFromEntity<Disabled> DisabledFromEntity;
      public void Execute(Entity entity, int index, [ReadOnly]ref Active active) {
        var isActive = active.Value;
        var disabledExists = DisabledFromEntity.Exists(entity);
        if (isActive && disabledExists)
          CommandBuffer.RemoveComponent<Disabled>(index, entity);
        else if(!isActive && !disabledExists)
          CommandBuffer.AddComponent<Disabled>(index, entity);
      }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {
      inputDeps = new UpdateDisabled {
        CommandBuffer = m_entityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
        DisabledFromEntity = GetComponentDataFromEntity<Disabled>(true)
      }.Schedule(m_allActivatableQuery, inputDeps);
      m_entityCommandBufferSystem.AddJobHandleForProducer(inputDeps);
      return inputDeps;
    }
  }
}
