using Stackray.Collections;
using Stackray.Mathematics;
using Stackray.Jobs;
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
  public class LookAtSystem : JobComponentSystem {

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

    [BurstCompile]
    struct LookAtEntityJob : IJobForEachWithEntity<LookAtEntity, Rotation> {
      [ReadOnly]
      public ComponentDataFromEntity<LocalToWorld> LocalToWorldFromEntity;
      [ReadOnly]
      public ComponentDataFromEntity<Parent> ParentFromEntity;
      [ReadOnly]
      public ComponentDataFromEntity<LookAtEntity> LookFromEntity;
      public void Execute(Entity entity, int index, [ReadOnly]ref LookAtEntity lookAt, [WriteOnly]ref Rotation rotation) {
        if (!LocalToWorldFromEntity.Exists(entity) || !LocalToWorldFromEntity.Exists(lookAt.Value))
          return;
        // Avoid multiple nested rotations
        if (TransformUtility.ExistsInHierarchy(entity, ParentFromEntity, LookFromEntity))
          return;
        var sourcePos = LocalToWorldFromEntity[entity].Value.Position();
        var targetPos = LocalToWorldFromEntity[lookAt.Value].Value.Position();
        var forward = math.normalize(sourcePos - targetPos);
        var up = math.normalize(LocalToWorldFromEntity[lookAt.Value].Value.Up());
        if (!forward.Equals(up) && math.lengthsq(forward + up) != 0)
          rotation.Value = quaternion.LookRotation(forward, up);
      }
    }

    [BurstCompile]
    struct GatherLookAtEntities : IJobForEach<LookAtEntity> {
      [WriteOnly]
      public NativeHashMap<Entity, Empty>.ParallelWriter LookAtEntities;
      public void Execute([ReadOnly]ref LookAtEntity lookAtEntity) {
        LookAtEntities.TryAdd(lookAtEntity.Value, default);
      }
    }

    [BurstCompile]
    struct GatherLookAtEntityPlanes : IJobForEach<LookAtEntityPlane> {
      [WriteOnly]
      public NativeHashMap<Entity, Empty>.ParallelWriter LookAtEntities;
      public void Execute([ReadOnly]ref LookAtEntityPlane lookAtEntity) {
        LookAtEntities.TryAdd(lookAtEntity.Value, default);
      }
    }

    [BurstCompile]
    struct DidChange : IJobForEachWithEntity<LocalToWorld> {
      [ReadOnly]
      public NativeHashMap<Entity, Empty> LookAtEntities;
      [WriteOnly]
      public NativeHashMap<Entity, Empty>.ParallelWriter ChangedEntities;
      public void Execute(Entity entity, int index, [ReadOnly, ChangedFilter]ref LocalToWorld localToWorld) {
        if (LookAtEntities.ContainsKey(entity))
          ChangedEntities.TryAdd(entity, default);
      }
    }

    [BurstCompile]
    struct LookAtEntityPlaneJob : IJobChunk {
      [ReadOnly]
      public ArchetypeChunkComponentType<LookAtEntityPlane> LookAtType;
      public ArchetypeChunkComponentType<LocalToWorld> LocalToWorldType;
      [ReadOnly]
      [NativeDisableContainerSafetyRestriction]
      public ComponentDataFromEntity<LocalToWorld> LocalToWorldFromEntity;
      [ReadOnly]
      public ComponentDataFromEntity<Parent> ParentFromEntity;
      [ReadOnly]
      public ComponentDataFromEntity<LookAtEntityPlane> LookFromEntity;
      [ReadOnly]
      public NativeHashMap<Entity, Empty> ChangedLookAtEntities;

      public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
        if (ChangedLookAtEntities.Length == 0)
          return;

        var lookAtArray = chunk.GetNativeArray(LookAtType);
        var localToWorldArray = new NativeArray<LocalToWorld>();

        for (var i = 0; i < chunk.Count; ++i) {
          var lookAtEntity = lookAtArray[i].Value;
          if (!LocalToWorldFromEntity.Exists(lookAtEntity))
            continue;
          // Avoid multiple nested rotations
          if (TransformUtility.ExistsInHierarchy(lookAtEntity, ParentFromEntity, LookFromEntity))
            continue;
          if (!ChangedLookAtEntities.ContainsKey(lookAtEntity))
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

    [BurstCompile]
    struct LookAtPositionJob : IJobForEachWithEntity<LookAtPosition, LocalToWorld> {
      [ReadOnly]
      [NativeDisableContainerSafetyRestriction]
      public ComponentDataFromEntity<LocalToWorld> LocalToWorldFromEntity;
      [ReadOnly]
      public ComponentDataFromEntity<Parent> ParentFromEntity;
      [ReadOnly]
      public ComponentDataFromEntity<LookAtPosition> LookFromEntity;
      public void Execute(Entity entity, int index, [ReadOnly, ChangedFilter]ref LookAtPosition lookAt, [WriteOnly]ref LocalToWorld localToWorld) {
        if (!LocalToWorldFromEntity.Exists(entity))
          return;
        // Avoid multiple nested rotations
        if (TransformUtility.ExistsInHierarchy(entity, ParentFromEntity, LookFromEntity))
          return;
        var sourcePos = LocalToWorldFromEntity[entity].Value.Position();
        var forward = math.normalize(sourcePos - lookAt.Value);
        var up = forward.Up();
        localToWorld.Value = math.mul(new float4x4(quaternion.LookRotation(forward, up), localToWorld.Position),
              float4x4.Scale(localToWorld.Value.Scale()));
      }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {
      if (m_forbiddenQuery.CalculateEntityCount() > 0)
        throw new System.Exception($"Entity can't have 'LookAtEntityComponent' and 'LookAtPositionComponent' at the same time");
      inputDeps = new LookAtEntityJob {
        LocalToWorldFromEntity = GetComponentDataFromEntity<LocalToWorld>(true),
        ParentFromEntity = GetComponentDataFromEntity<Parent>(true),
        LookFromEntity = GetComponentDataFromEntity<LookAtEntity>(true)
      }.Schedule(this, inputDeps);

      
      inputDeps = JobHandle.CombineDependencies(
        new ClearNativeHashMap<Entity, Empty> {
          Source = m_changedLookAtEntities,
          Capacity = m_query.CalculateEntityCount()
        }.Schedule(inputDeps),
        new ClearNativeHashMap<Entity, Empty> {
          Source = m_lookAtEntities,
          Capacity = m_query.CalculateEntityCount()
        }.Schedule(inputDeps));

      inputDeps = new GatherLookAtEntities {
        LookAtEntities = m_lookAtEntities.AsParallelWriter()
      }.Schedule(this, inputDeps);

      inputDeps = new GatherLookAtEntityPlanes {
        LookAtEntities = m_lookAtEntities.AsParallelWriter()
      }.Schedule(this, inputDeps);
      
      inputDeps = new DidChange {
        LookAtEntities = m_lookAtEntities,
        ChangedEntities = m_changedLookAtEntities.AsParallelWriter()
      }.Schedule(this, inputDeps);
      
      inputDeps = new LookAtEntityPlaneJob {
        LookAtType = GetArchetypeChunkComponentType<LookAtEntityPlane>(true),
        LocalToWorldType = GetArchetypeChunkComponentType<LocalToWorld>(false),
        LocalToWorldFromEntity = GetComponentDataFromEntity<LocalToWorld>(true),
        ParentFromEntity = GetComponentDataFromEntity<Parent>(true),
        LookFromEntity = GetComponentDataFromEntity<LookAtEntityPlane>(true),
        ChangedLookAtEntities = m_changedLookAtEntities,
      }.Schedule(m_query, inputDeps);
      inputDeps = new LookAtPositionJob {
        LocalToWorldFromEntity = GetComponentDataFromEntity<LocalToWorld>(true),
        ParentFromEntity = GetComponentDataFromEntity<Parent>(true),
        LookFromEntity = GetComponentDataFromEntity<LookAtPosition>(true)
      }.Schedule(this, inputDeps);
      return inputDeps;
    }
  }
}
