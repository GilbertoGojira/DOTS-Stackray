using Stackray.Collections;
using Stackray.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Stackray.Transforms {
  [AlwaysUpdateSystem]
  [UpdateAfter(typeof(TransformSystemGroup))]
  public class LookAtSystem : SystemBase {

    EntityQuery m_query;
    EntityQuery m_forbiddenQuery;

    struct Empty { }

    NativeHashMap<Entity, Empty> m_lookAtEntities;
    NativeHashMap<Entity, Empty> m_changedLookAtEntities;

    protected override void OnCreate() {
      base.OnCreate();
      m_query = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] { ComponentType.ReadWrite<LocalToWorld>() },
        Any = new ComponentType[] {
          ComponentType.ReadOnly<LookAtEntity>(),
          ComponentType.ReadOnly<LookAtEntityPlane>(),
          ComponentType.ReadOnly<LookAtPosition>()
        }
      });
      m_forbiddenQuery = GetEntityQuery(typeof(LookAtEntity), typeof(LookAtPosition));

      m_lookAtEntities = new NativeHashMap<Entity, Empty>(0, Allocator.Persistent);
      m_changedLookAtEntities = new NativeHashMap<Entity, Empty>(0, Allocator.Persistent);
    }

    protected override void OnDestroy() {
      base.OnDestroy();
      m_lookAtEntities.Dispose();
      m_changedLookAtEntities.Dispose();
    }

    void LookAtEntity() {
      var parentFromEntity = GetComponentDataFromEntity<Parent>(true);
      var lookFromEntity = GetComponentDataFromEntity<LookAtEntity>(true);
      Entities
        .WithReadOnly(parentFromEntity)
        .WithReadOnly(lookFromEntity)
        .ForEach((Entity entity, int entityInQueryIndex, ref Rotation rotation, in LookAtEntity lookAt) => {
          if (!HasComponent<LocalToWorld>(entity) || !HasComponent<LocalToWorld>(lookAt.Value))
            return;
          // Avoid multiple nested rotations
          if (TransformUtility.ExistsInHierarchy(entity, parentFromEntity, lookFromEntity))
            return;
          var sourcePos = GetComponent<LocalToWorld>(entity).Value.Position();
          var targetPos = GetComponent<LocalToWorld>(lookAt.Value).Value.Position();
          var forward = math.normalize(sourcePos - targetPos);
          var up = math.normalize(GetComponent<LocalToWorld>(lookAt.Value).Value.Up());
          if (!forward.Equals(up) && math.lengthsq(forward + up) != 0)
            rotation.Value = quaternion.LookRotation(forward, up);
        }).ScheduleParallel();
    }

    void GatherLookAtEntities(NativeHashMap<Entity, Empty>.ParallelWriter gatheredEntities) {
      Entities
        .ForEach((in LookAtEntity lookAtEntity) => {
          gatheredEntities.TryAdd(lookAtEntity.Value, default);
        }).ScheduleParallel();
    }

    void GatherLookAtEntityPlanes(NativeHashMap<Entity, Empty>.ParallelWriter gatheredEntities) {
      Entities
        .ForEach((in LookAtEntityPlane lookAtEntity) => {
          gatheredEntities.TryAdd(lookAtEntity.Value, default);
        }).ScheduleParallel();
    }

    void GatherChangedEntities(NativeHashMap<Entity, Empty> entities, NativeHashMap<Entity, Empty>.ParallelWriter changedEntities) {
      Entities
        .WithReadOnly(entities)
        .WithChangeFilter<LocalToWorld>()
        .ForEach((Entity entity, int entityInQueryIndex, in LocalToWorld localToWorld) => {
          if (entities.ContainsKey(entity))
            changedEntities.TryAdd(entity, default);
        }).ScheduleParallel();
    }

    [BurstCompile]
    struct LookAtEntityPlaneJob : IJobChunk {
      [ReadOnly]
      public ComponentTypeHandle<LookAtEntityPlane> LookAtType;
      public ComponentTypeHandle<LocalToWorld> LocalToWorldType;
      [ReadOnly]
      [NativeDisableContainerSafetyRestriction]
      public ComponentDataFromEntity<LocalToWorld> LocalToWorldFromEntity;
      [ReadOnly]
      public ComponentDataFromEntity<Parent> ParentFromEntity;
      [ReadOnly]
      public ComponentDataFromEntity<LookAtEntityPlane> LookFromEntity;
      [ReadOnly]
      public NativeHashMap<Entity, Empty> ChangedLookAtEntities;
      public uint LastSystemVersion;

      public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
        var localToWorldChunkChanged = chunk.DidChange(LocalToWorldType, LastSystemVersion);
        if (ChangedLookAtEntities.Count() == 0 && !localToWorldChunkChanged)
          return;

        var lookAtArray = chunk.GetNativeArray(LookAtType);
        var localToWorldArray = new NativeArray<LocalToWorld>();

        for (var i = 0; i < chunk.Count; ++i) {
          var lookAtEntity = lookAtArray[i].Value;
          if (!LocalToWorldFromEntity.HasComponent(lookAtEntity))
            continue;
          // Avoid multiple nested rotations
          if (TransformUtility.ExistsInHierarchy(lookAtEntity, ParentFromEntity, LookFromEntity))
            continue;
          if (!localToWorldChunkChanged && !ChangedLookAtEntities.ContainsKey(lookAtEntity))
            continue;

          localToWorldArray = localToWorldArray.IsCreated ? localToWorldArray : chunk.GetNativeArray(LocalToWorldType);
          var localToWorld = localToWorldArray[i];
          var forward = math.normalize(LocalToWorldFromEntity[lookAtEntity].Value.Forward());
          var up = math.normalize(LocalToWorldFromEntity[lookAtEntity].Value.Up());
          if (!math.cross(forward, up).Equals(float3.zero))
            localToWorldArray[i] = new LocalToWorld {
              Value = math.mul(new float4x4(quaternion.LookRotation(forward, up), localToWorld.Position),
              float4x4.Scale(localToWorld.Value.Scale()))
            };
        }
      }
    }

    void LookAtPosition() {
      // TODO: Look at position is not accounting for localToWorld change 
      // and that should make a difference on the final rotation
      var localToWorldFromEntity = GetComponentDataFromEntity<LocalToWorld>(true);
      var parentFromEntity = GetComponentDataFromEntity<Parent>(true);
      var lookFromEntity = GetComponentDataFromEntity<LookAtEntity>(true);
      Entities
        .WithNativeDisableContainerSafetyRestriction(localToWorldFromEntity)
        .WithReadOnly(localToWorldFromEntity)
        .WithReadOnly(parentFromEntity)
        .WithReadOnly(lookFromEntity)
        .WithChangeFilter<LookAtPosition>()
        .ForEach((Entity entity, int entityInQueryIndex, ref LocalToWorld localToWorld, in LookAtPosition lookAt) => {
          if (!localToWorldFromEntity.HasComponent(entity))
            return;
          // Avoid multiple nested rotations
          if (TransformUtility.ExistsInHierarchy(entity, parentFromEntity, lookFromEntity))
            return;
          var sourcePos = localToWorldFromEntity[entity].Value.Position();
          var forward = math.normalize(sourcePos - lookAt.Value);
          var up = forward.Up();
          localToWorld.Value = math.mul(new float4x4(quaternion.LookRotation(forward, up), localToWorld.Position),
                float4x4.Scale(localToWorld.Value.Scale()));
        }).ScheduleParallel();
    }

    protected override void OnUpdate() {
      if (m_forbiddenQuery.CalculateEntityCount() > 0)
        throw new System.Exception($"Entity can't have 'LookAtEntityComponent' and 'LookAtPositionComponent' at the same time");
      LookAtEntity();      
      Dependency = JobHandle.CombineDependencies(
        m_changedLookAtEntities.Clear(Dependency, m_query.CalculateEntityCount()),
        m_lookAtEntities.Clear(Dependency, m_query.CalculateEntityCount()));
      GatherLookAtEntities(m_lookAtEntities.AsParallelWriter());
      GatherLookAtEntityPlanes(m_lookAtEntities.AsParallelWriter());
      GatherChangedEntities(m_lookAtEntities, m_changedLookAtEntities.AsParallelWriter());      
      Dependency = new LookAtEntityPlaneJob {
        LookAtType = GetComponentTypeHandle<LookAtEntityPlane>(true),
        LocalToWorldType = GetComponentTypeHandle<LocalToWorld>(false),
        LocalToWorldFromEntity = GetComponentDataFromEntity<LocalToWorld>(true),
        ParentFromEntity = GetComponentDataFromEntity<Parent>(true),
        LookFromEntity = GetComponentDataFromEntity<LookAtEntityPlane>(true),
        ChangedLookAtEntities = m_changedLookAtEntities,
        LastSystemVersion = LastSystemVersion
      }.Schedule(m_query, Dependency);
      LookAtPosition();
    }
  }
}
