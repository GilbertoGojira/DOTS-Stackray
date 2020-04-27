using Stackray.Renderer;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;

public class RandomColorSystem : SystemBase {

  protected override void OnUpdate() {
    var seed = (uint)System.DateTime.Now.Millisecond + 1;
    Entities
      .WithAll<RandomColorTag>()
      .ForEach((Entity entity, int entityInQueryIndex, ref ColorProperty colorProp) => {
        var random = new Random(seed + (uint)entityInQueryIndex);
        colorProp.Value = new half4((half3)random.NextFloat3(), (half)1);
      }).ScheduleParallel();

    Entities
    .WithAll<RandomColorTag>()
        .ForEach((Entity entity, int entityInQueryIndex, ref MaterialColor colorProp) => {
          var random = new Random(seed + (uint)entityInQueryIndex);
          colorProp.Value = new float4(random.NextFloat3(), 1);
        }).ScheduleParallel();
  }
}
