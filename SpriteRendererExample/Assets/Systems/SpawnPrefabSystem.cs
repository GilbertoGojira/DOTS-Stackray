using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

public class SpawnPrefabSystem : SystemBase {

  BeginInitializationEntityCommandBufferSystem m_entityCommandBufferSystem;

  protected override void OnCreate() {
    base.OnCreate();
    m_entityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
  }

  protected override void OnUpdate() {
    if (Input.GetKeyDown(KeyCode.H)) {
      var cmdBuffer = m_entityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
      Entities.ForEach((Entity entity, int entityInQueryIndex, ref PrefabComponent c0) => {
        cmdBuffer.Instantiate(entityInQueryIndex, c0.Prefab);
      }).ScheduleParallel();
    }
    m_entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
  }
}
