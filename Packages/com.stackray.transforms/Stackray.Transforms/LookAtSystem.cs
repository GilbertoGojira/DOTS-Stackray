using Stackray.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Stackray.Transforms {
  [AlwaysUpdateSystem]
  [UpdateInGroup(typeof(TransformSystemGroup))]
  public class LookAtSystem : JobComponentSystem {

    EntityQuery m_forbiddenQuery;

    protected override void OnCreate() {
      base.OnCreate();
      m_forbiddenQuery = GetEntityQuery(typeof(LookAtEntity), typeof(LookAtPosition));
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
    struct LookAtEntityPlaneJob : IJobForEachWithEntity<LookAtEntityPlane, Rotation> {
      [ReadOnly]
      public ComponentDataFromEntity<LocalToWorld> LocalToWorldFromEntity;
      [ReadOnly]
      public ComponentDataFromEntity<Parent> ParentFromEntity;
      [ReadOnly]
      public ComponentDataFromEntity<LookAtEntityPlane> LookFromEntity;
      public void Execute(Entity entity, int index, [ReadOnly]ref LookAtEntityPlane lookAt, [WriteOnly]ref Rotation rotation) {
        if (!LocalToWorldFromEntity.Exists(lookAt.Value))
          return;
        // Avoid multiple nested rotations
        if (TransformUtility.ExistsInHierarchy(entity, ParentFromEntity, LookFromEntity))
          return;
        var forward = math.normalize(LocalToWorldFromEntity[lookAt.Value].Value.Forward());
        var up = math.normalize(LocalToWorldFromEntity[lookAt.Value].Value.Up());
        if (!math.cross(forward, up).Equals(float3.zero))
          rotation.Value = quaternion.LookRotation(forward, up);
      }
    }

    [BurstCompile]
    struct LookAtPositionJob : IJobForEachWithEntity<LookAtPosition, Rotation> {
      [ReadOnly]
      public ComponentDataFromEntity<LocalToWorld> LocalToWorldFromEntity;
      [ReadOnly]
      public ComponentDataFromEntity<Parent> ParentFromEntity;
      [ReadOnly]
      public ComponentDataFromEntity<LookAtPosition> LookFromEntity;
      public void Execute(Entity entity, int index, [ReadOnly, ChangedFilter]ref LookAtPosition lookAt, [WriteOnly]ref Rotation rotation) {
        if (!LocalToWorldFromEntity.Exists(entity))
          return;
        // Avoid multiple nested rotations
        if (TransformUtility.ExistsInHierarchy(entity, ParentFromEntity, LookFromEntity))
          return;
        var sourcePos = LocalToWorldFromEntity[entity].Value.Position(); ;
        var forward = math.normalize(sourcePos - lookAt.Value);
        var up = forward.Up();
        rotation.Value = quaternion.LookRotation(forward, up);
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
      inputDeps = new LookAtEntityPlaneJob {
        LocalToWorldFromEntity = GetComponentDataFromEntity<LocalToWorld>(true),
        ParentFromEntity = GetComponentDataFromEntity<Parent>(true),
        LookFromEntity = GetComponentDataFromEntity<LookAtEntityPlane>(true)
      }.Schedule(this, inputDeps);
      inputDeps = new LookAtPositionJob {
        LocalToWorldFromEntity = GetComponentDataFromEntity<LocalToWorld>(true),
        ParentFromEntity = GetComponentDataFromEntity<Parent>(true),
        LookFromEntity = GetComponentDataFromEntity<LookAtPosition>(true)
      }.Schedule(this, inputDeps);
      return inputDeps;
    }
  }
}
