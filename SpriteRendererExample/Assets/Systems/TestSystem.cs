using Stackray.Entities;
using Stackray.SpriteRenderer;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public class TestSystem : JobComponentSystem {

  BeginInitializationEntityCommandBufferSystem m_entityCommandBufferSystem;
  protected override void OnCreate() {
    base.OnCreate();
    m_entityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
  }

  [BurstCompile]
  struct MoveJob : IJobForEachWithEntity<Translation> {

    public void Execute(Entity entity, int index, [WriteOnly]ref Translation c0) {
      //if(index > 100 && index < 200 || index > 500 && index < 550)
      //if(index == 0)
        c0.Value += new float3(0.1f, 0, 0);
    }
  }

  [BurstCompile]
  struct RotateJob : IJobForEachWithEntity<Rotation> {

    public void Execute(Entity entity, int index, ref Rotation c0) {
      c0.Value = math.mul(c0.Value, quaternion.RotateZ(math.radians(1)));
    }
  }

  struct CreateJob : IJobForEachWithEntity<PrefabComponent> {
    public EntityCommandBuffer.Concurrent CmdBuffer;

    public void Execute(Entity entity, int index, ref PrefabComponent c0) {
      CmdBuffer.Instantiate(index, c0.Prefab);
    }
  }

  [BurstCompile]
  [RequireComponentTag(typeof(SpriteRenderMesh))]
  struct DeleteJob : IJobForEachWithEntity<Translation> {
    public EntityCommandBuffer.Concurrent CmdBuffer;
    public void Execute(Entity entity, int index, [ReadOnly]ref Translation c0) {
      if (index == 0)
        CmdBuffer.DestroyEntity(index, entity);
    }
  }

  protected override JobHandle OnUpdate(JobHandle inputDeps) {
    if (Input.GetKeyDown(KeyCode.C))
      inputDeps = new CreateJob {
        CmdBuffer = m_entityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
      }.Schedule(this, inputDeps);
    if (Input.GetKeyDown(KeyCode.D))
      inputDeps = new DeleteJob {
        CmdBuffer = m_entityCommandBufferSystem.CreateCommandBuffer().ToConcurrent()
      }.Schedule(this, inputDeps);
    if (Input.GetKey(KeyCode.R))
      inputDeps = new RotateJob().Schedule(this, inputDeps);
    if (Input.GetKey(KeyCode.M))
      inputDeps = new MoveJob().Schedule(this, inputDeps);
    m_entityCommandBufferSystem.AddJobHandleForProducer(inputDeps);
    return inputDeps;
  }

  [DrawGizmos]
  void OnDrawGizmos() {
    Gizmos.color = Color.green;
    var query = GetEntityQuery(ComponentType.ReadOnly<WorldRenderBounds>());
    using (var bounds = query.ToComponentDataArray<WorldRenderBounds>(Allocator.TempJob)) {
      for (var i = 0; i < bounds.Length; ++i) {
        var b = bounds[i].Value;
        Gizmos.DrawWireCube(b.Center, b.Size);
      }
    }
  }
}
