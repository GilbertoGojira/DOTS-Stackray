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
  EntityQuery m_transformQuery;
  Entity m_targetEntity;
  protected override void OnCreate() {
    base.OnCreate();
    m_entityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
    m_transformQuery = GetEntityQuery(
      new EntityQueryDesc {
        All = new ComponentType[] { typeof(LocalToWorld) },
        Options = EntityQueryOptions.IncludeDisabled
      });
  }

  [BurstCompile]
  struct MoveJob : IJobForEachWithEntity<LocalToWorld> {
    public float3 MoveValue;
    public Entity Entity;
    public void Execute(Entity entity, int index, [WriteOnly]ref LocalToWorld c0) {
      //if(index > 100 && index < 200 || index > 500 && index < 550)
      if(Entity == Entity.Null && index == 1 || Entity == entity)
        c0.Value = math.mul(c0.Value, float4x4.Translate(MoveValue));
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
    if (m_targetEntity == Entity.Null) {
      var entities = m_transformQuery.ToEntityArray(Allocator.TempJob);
      m_targetEntity = entities.Length > 1 ? entities[0] : Entity.Null;
      entities.Dispose();
    }
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
      inputDeps = new MoveJob {
        MoveValue = new float3(0, 0, -0.1f),
        Entity = m_targetEntity
      }.Schedule(m_transformQuery, inputDeps);
    if (Input.GetKey(KeyCode.J))
      inputDeps = new MoveJob {
        MoveValue = new float3(0, 0, 0.1f),
        Entity = m_targetEntity
      }.Schedule(m_transformQuery, inputDeps);
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
