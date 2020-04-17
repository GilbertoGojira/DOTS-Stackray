using Stackray.Renderer;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public class RandomColorSystem : SystemBase {
  protected override void OnUpdate() {
    var seed = (uint)System.DateTime.Now.Millisecond;
    Entities
      .WithAll<RandomColorTag>()
      .ForEach((Entity entity, int entityInQueryIndex, ref ColorProperty colorProp) => {
      var random = new Random(seed + (uint)entityInQueryIndex);
      colorProp.Value = new half4((half3)random.NextFloat3(), (half)1);
    }).ScheduleParallel();
  }
}
