using Stackray.Entities;
using Stackray.Sprite;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public class TestSystem : SystemBase {

  BeginInitializationEntityCommandBufferSystem m_entityCommandBufferSystem;
  EntityQuery m_transformQuery;
  Entity m_targetEntity;
  protected override void OnCreate() {
    base.OnCreate();
    m_entityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
  }

  void Move(float3 value, Entity targetEntity) {
    Entities
      .WithStoreEntityQueryInField(ref m_transformQuery)
      .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
      .ForEach((Entity entity, int entityInQueryIndex, ref LocalToWorld localToWorld) => {
        if (targetEntity == entity)
          localToWorld.Value = math.mul(localToWorld.Value, float4x4.Translate(value));
      }).ScheduleParallel();
  }

  void Rotate(float degrees) {
    Entities
      .ForEach((Entity entity, int entityInQueryIndex, ref Rotation rotation) => {
        rotation.Value = math.mul(rotation.Value, quaternion.RotateZ(math.radians(degrees)));
      }).ScheduleParallel();
  }

  void DeleteEntityAtIndex(int index = 0) {
    var cmdBuffer = m_entityCommandBufferSystem.CreateCommandBuffer().ToConcurrent();
    Entities
      .WithAll<RenderMesh, Translation>()
      .ForEach((Entity entity, int entityInQueryIndex) => {
        if (entityInQueryIndex == index)
          cmdBuffer.DestroyEntity(entityInQueryIndex, entity);
      }).ScheduleParallel();
  }

  protected override void OnUpdate() {
    if (m_targetEntity == Entity.Null) {
      var entities = m_transformQuery.ToEntityArray(Allocator.TempJob);
      m_targetEntity = entities.Length > 1 ? entities[0] : Entity.Null;
      entities.Dispose();
    }

    if (Input.GetKeyDown(KeyCode.D))
      DeleteEntityAtIndex(0);
    if (Input.GetKey(KeyCode.R))
      Rotate(1);
    if (Input.GetKey(KeyCode.M))
      Move(new float3(0, 0, -0.1f), m_targetEntity);
    if (Input.GetKey(KeyCode.J))
      Move(new float3(0, 0, 0.1f), m_targetEntity);
    if (Input.GetKey(KeyCode.Q)) {
      var tmpQuery = EntityManager.CreateEntityQuery(typeof(SpriteAnimation));
      var tmpEntities = tmpQuery.ToEntityArray(Allocator.TempJob);
      if (tmpEntities.Length > 1) {
        var animation = EntityManager.GetSharedComponentData<SpriteAnimation>(tmpEntities[0]);
        animation.ClipIndex = UnityEngine.Random.Range(0, animation.ClipCount);
        EntityManager.SetSharedComponentData(tmpEntities[0], animation);
        animation.ClipIndex = UnityEngine.Random.Range(0, animation.ClipCount);
        EntityManager.SetSharedComponentData(tmpEntities[0], animation);
      }
      tmpEntities.Dispose();
    }
    m_entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
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
