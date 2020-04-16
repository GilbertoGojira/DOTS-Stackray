using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Stackray.Transforms {
  class UpdateDisabledSystem : SystemBase {

    BeginInitializationEntityCommandBufferSystem m_entityCommandBufferSystem;

    protected override void OnCreate() {
      base.OnCreate();
      m_entityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
    }

    void UpdateDisabled() {
      var cmdBuffer = m_entityCommandBufferSystem.CreateCommandBuffer().ToConcurrent();
      Entities
        .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
        .ForEach((Entity entity, int entityInQueryIndex, in Active active) => {
          var isActive = active.Value;
          var disabledExists = HasComponent<Disabled>(entity);
          if (isActive && disabledExists)
            cmdBuffer.RemoveComponent<Disabled>(entityInQueryIndex, entity);
          else if (!isActive && !disabledExists)
            cmdBuffer.AddComponent<Disabled>(entityInQueryIndex, entity);
        }).Schedule();
    }

    protected override void OnUpdate() {
      UpdateDisabled();
      m_entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
    }
  }
}
