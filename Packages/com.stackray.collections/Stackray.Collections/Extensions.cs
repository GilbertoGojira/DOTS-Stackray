using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Stackray.Collections {
  public static class Extensions {

    [BurstCompile]
    struct DisposeJob<T> : IJob where T : struct, IDisposable {
      [DeallocateOnJobCompletion]
      public T Disposable;
      public void Execute() { }
    }
    public static JobHandle Dispose<T>(this T disposable, JobHandle inputDeps)
      where T : struct , IDisposable {

      return new DisposeJob<T> {
        Disposable = disposable
      }.Schedule(inputDeps);
    }
  }
}
