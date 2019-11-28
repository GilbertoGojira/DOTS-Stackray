using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Stackray.Transforms {
  public class RotateSystem : JobComponentSystem {

    [BurstCompile]
    struct RotateJob : IJobForEach<Rotate, Rotation> {
      public float DeltaTime;
      public void Execute([ReadOnly]ref Rotate c0, [WriteOnly]ref Rotation c1) {        
        c1.Value = math.mul(c1.Value, 
          math.slerp(quaternion.identity, c0.Value, DeltaTime));
      }
    }

    [BurstCompile]
    struct RotateAroundJob : IJobForEach<RotateAround, Translation, Rotation> {
      [ReadOnly]
      public ComponentDataFromEntity<LocalToWorld> LocalToWorldFromEntity;
      public float DeltaTime;
      public void Execute([ReadOnly]ref RotateAround c0, [WriteOnly]ref Translation translation, ref Rotation rotation) {
        var target = c0.Target;
        if (!LocalToWorldFromEntity.Exists(target))
          return;
        var startRotation = rotation.Value;
        var deltaRotation = math.slerp(quaternion.identity, c0.Value, DeltaTime);
        var center = LocalToWorldFromEntity[target].Position;
        translation.Value = center + math.mul(deltaRotation, translation.Value - center);
        rotation.Value =
          math.mul(
            math.mul(
              startRotation,
              math.mul(
                math.inverse(startRotation),
                deltaRotation)),
          startRotation);
      }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {
      inputDeps = new RotateJob {
          DeltaTime = Time.DeltaTime
        }.Schedule(this, inputDeps);
      inputDeps = new RotateAroundJob {
          DeltaTime = Time.DeltaTime,
          LocalToWorldFromEntity = GetComponentDataFromEntity<LocalToWorld>(true)
        }.Schedule(this, inputDeps);
      return inputDeps;
    }
  }
}
