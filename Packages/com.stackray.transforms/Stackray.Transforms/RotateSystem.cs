using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Stackray.Transforms {
  public class RotateSystem : SystemBase {

    void Rotate(float deltaTime) {
      Entities
        .ForEach((ref Rotation rotation, in Rotate rotate) => {
          rotation.Value = math.mul(rotation.Value,
            math.slerp(quaternion.identity, rotate.Value, deltaTime));
        }).ScheduleParallel();
    }

    void RotateAround(float deltaTime) {
      Entities
      .ForEach((ref Translation translation, ref Rotation rotation, in RotateAround rotateAround) => {
        var target = rotateAround.Target;
        if (!HasComponent<LocalToWorld>(target))
          return;
        var startRotation = rotation.Value;
        var deltaRotation = math.slerp(quaternion.identity, rotateAround.Value, deltaTime);
        var center = GetComponent<LocalToWorld>(target).Position;
        translation.Value = center + math.mul(deltaRotation, translation.Value - center);
        rotation.Value =
          math.mul(
            math.mul(
              startRotation,
              math.mul(
                math.inverse(startRotation),
                deltaRotation)),
          startRotation);
      }).ScheduleParallel();
    }

    protected override void OnUpdate() {
      Rotate(Time.DeltaTime);
      RotateAround(Time.DeltaTime);
    }
  }
}
