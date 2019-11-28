using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

public class SpawnPrefabSystem : JobComponentSystem {

  BeginInitializationEntityCommandBufferSystem m_entityCommandBufferSystem;

  protected override void OnCreate() {
    base.OnCreate();
    m_entityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
  }

  [BurstCompile]
  struct InstantiateJob : IJobForEachWithEntity<PrefabComponent> {
    public EntityCommandBuffer.Concurrent CmdBuffer;

    public void Execute(Entity entity, int index, [ReadOnly]ref PrefabComponent c0) {
      CmdBuffer.Instantiate(index, c0.Prefab);
    }
  }

  protected override JobHandle OnUpdate(JobHandle inputDeps) {
    if (Input.GetKeyDown(KeyCode.H))
      inputDeps = new InstantiateJob {
        CmdBuffer = m_entityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
      }.Schedule(this, inputDeps);
    m_entityCommandBufferSystem.AddJobHandleForProducer(inputDeps);
    return inputDeps;
  }
}
