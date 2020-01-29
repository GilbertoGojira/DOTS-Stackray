using System;
using Unity.Burst;
using Unity.Jobs;

namespace Stackray.TestGenericSystems {

  public class GenericSystem<Type>
  where Type : struct {
    [BurstCompile]
    struct HeavyJob<T> : IJob where T : struct {
      public void Execute() { }
    }

    protected void OnUpdate() {
      new HeavyJob<Type>()
        .Schedule();
    }
  }

  class MyBottstrap {
    public void Initialize() {
      Create(typeof(GenericSystem<double>));
    }

    static void Create(Type type) { }
  }
}
