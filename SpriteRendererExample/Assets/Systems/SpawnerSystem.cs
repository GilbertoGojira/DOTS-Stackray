using Stackray.Transforms;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

// JobComponentSystems can run on worker threads.
// However, creating and removing Entities can only be done on the main thread to prevent race conditions.
// The system uses an EntityCommandBuffer to defer tasks that can't be done inside the Job.

[UpdateInGroup(typeof(SimulationSystemGroup))]
public class SpawnerSystem : SystemBase {
  // BeginInitializationEntityCommandBufferSystem is used to create a command buffer which will then be played back
  // when that barrier system executes.
  // Though the instantiation command is recorded in the SpawnJob, it's not actually processed (or "played back")
  // until the corresponding EntityCommandBufferSystem is updated. To ensure that the transform system has a chance
  // to run on the newly-spawned entities before they're rendered for the first time, the SpawnerSystem_FromEntity
  // will use the BeginSimulationEntityCommandBufferSystem to play back its commands. This introduces a one-frame lag
  // between recording the commands and instantiating the entities, but in practice this is usually not noticeable.
  BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;

  protected override void OnCreate() {
    // Cache the BeginInitializationEntityCommandBufferSystem in a field, so we don't have to create it every frame
    m_EntityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
  }

  void Spawn() {
    var cmdBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
    Entities
      .ForEach((Entity entity, int entityInQueryIndex, in Spawner spawner, in LocalToWorld localToWorld) => {
        var parent = spawner.Parent;
        var horizontalInterval = spawner.HorizontalInterval;
        var verticalInterval = spawner.VerticalInterval;
        var depthInterval = spawner.DepthInterval;
        var origin = parent != Entity.Null ? GetComponent<LocalToWorld>(parent).Value : localToWorld.Value;
        for (var x = 0; x < spawner.CountX; x++)
          for (var y = 0; y < spawner.CountY; y++)
            for (var z = 0; z < spawner.CountZ; z++) {
              // Place the instantiated in a grid
              var instance = cmdBuffer.Instantiate(entityInQueryIndex, spawner.Prefab);
              var position = math.transform(
                origin,
                new float3((x - spawner.CountX * 0.5f) * horizontalInterval,
                (y - spawner.CountY * 0.5f) * verticalInterval,
                (z - spawner.CountZ * 0.5f) * depthInterval));
              cmdBuffer.SetComponent(entityInQueryIndex, instance, new Translation { Value = position });
              if (parent != Entity.Null)
                cmdBuffer.SetParent(entityInQueryIndex, parent, instance);
            }
        cmdBuffer.DestroyEntity(entityInQueryIndex, entity);
      }).ScheduleParallel();
  }

  protected override void OnUpdate() {
    //Instead of performing structural changes directly, a Job can add a command to an EntityCommandBuffer to perform such changes on the main thread after the Job has finished.
    //Command buffers allow you to perform any, potentially costly, calculations on a worker thread, while queuing up the actual insertions and deletions for later.

    // Schedule the job that will add Instantiate commands to the EntityCommandBuffer.
    Spawn();

    // SpawnJob runs in parallel with no sync point until the barrier system executes.
    // When the barrier system executes we want to complete the SpawnJob and then play back the commands (Creating the entities and placing them).
    // We need to tell the barrier system which job it needs to complete before it can play back the commands.
    m_EntityCommandBufferSystem.AddJobHandleForProducer(Dependency);
  }
}
