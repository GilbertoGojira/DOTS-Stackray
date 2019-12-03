using Stackray.Renderer;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public class RandomColorSystem : JobComponentSystem {

  [BurstCompile]
  [RequireComponentTag(typeof(RandomColorTag))]
  struct RandomColor : IJobForEachWithEntity<ColorProperty> {
    public uint RandomSeed;
    public void Execute(Entity entity, int index,[WriteOnly]ref ColorProperty c0) {
      var random = new Random(1 + RandomSeed + (uint)index);
      c0.Value = new half4((half3)random.NextFloat3(), (half)1);
    }
  }

  protected override JobHandle OnUpdate(JobHandle inputDeps) {
    return new RandomColor {
      RandomSeed = (uint)System.DateTime.Now.Millisecond
    }.Schedule(this, inputDeps);
  }
}
