using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Stackray.Collections {
  public static class Extensions {

    #region Dispose
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
    #endregion Dispose

    #region Clear Native Containers
    public static JobHandle Clear<T>(this NativeList<T> container, JobHandle inputDeps, int capacity = default) 
      where T : struct {

      return new ClearNativeList<T> {
        Source = container,
        Capacity = capacity
      }.Schedule(inputDeps);
    }

    public static JobHandle Clear<T>(this NativeQueue<T> container, JobHandle inputDeps)
      where T : struct {

      return new ClearNativeQueue<T> {
        Source = container
      }.Schedule(inputDeps);
    }

    public static JobHandle Clear<T1, T2>(this NativeHashMap<T1, T2> container, JobHandle inputDeps, int capacity = default)
      where T1 : struct, IEquatable<T1>
      where T2 : struct {

      return new ClearNativeHashMap<T1, T2> {
        Source = container,
        Capacity = capacity
      }.Schedule(inputDeps);
    }

    public static JobHandle Clear<T1, T2>(this NativeMultiHashMap<T1, T2> container, JobHandle inputDeps, int capacity = default)
      where T1 : struct, IEquatable<T1>
      where T2 : struct {

      return new ClearNativeMultiHashMap<T1, T2> {
        Source = container,
        Capacity = capacity
      }.Schedule(inputDeps);
    }

    public static JobHandle Clear<T>(this NativeHashSet<T> container, JobHandle inputDeps)
     where T : struct, IEquatable<T> {

      return new ClearNativeHashSet<T> {
        Source = container
      }.Schedule(inputDeps);
    }

    #endregion Clear Native Containers

    #region Resize Native Containers

    public static JobHandle Resize<T>(this NativeList<T> container, int length, JobHandle inputDeps)
      where T : struct {

      return new ResizeNativeList<T> {
        Source = container,
        Length = length
      }.Schedule(inputDeps);
    }

    #endregion Resize Native Containers

    #region Copy Between Containers

    public static JobHandle CopyTo<T>(this NativeArray<T> container, NativeArray<T> target, int sourceOffset, int targetOffset, JobHandle inputDeps)
      where T : struct {

      return new CopyToNativeArray<T> {
        Source = container,
        Target = target,
        SourceOffset = sourceOffset,
        TargetOffset = targetOffset
      }.Schedule(container.Length, 128, inputDeps);
    }

    public static JobHandle CopyTo<T>(this NativeList<T> container, NativeArray<T> target, int sourceOffset, int targetOffset, int length, JobHandle inputDeps)
      where T : struct {

      return new CopyToNativeArray<T> {
        Source = container.AsDeferredJobArray(),
        Target = target,
        SourceOffset = sourceOffset,
        TargetOffset = targetOffset
      }.Schedule(length, 128, inputDeps);
    }

    public static JobHandle CopyFrom<T>(this NativeArray<T> container, NativeQueue<T> source, JobHandle inputDeps)
      where T : struct {

      return new CopyFromNativeQueueToArray<T> {
        Source = source,
        Target = container
      }.Schedule(inputDeps);
    }

    public static JobHandle CopyFrom<T>(this NativeList<T> container, NativeQueue<T> source, JobHandle inputDeps)
      where T : struct {

      return new CopyFromNativeQueue<T> {
        Source = source,
        Target = container
      }.Schedule(inputDeps);
    }

    #endregion Copy Between Containers

    #region Container Contains

    public static JobHandle Contains<T>(this NativeArray<T> container, ref NativeUnit<bool> result, T value, JobHandle inputDeps)
       where T : struct, IEquatable<T> {

      return new Contains<T> {
        Source = container,
        Result = result,
        TestValue = value
      }.Schedule(inputDeps);
    }

    public static JobHandle Contains<T>(this NativeList<T> container, ref NativeUnit<bool> result, T value, JobHandle inputDeps)
      where T : struct, IEquatable<T> {

      return new Contains<T> {
        Source = container.AsDeferredJobArray(),
        Result = result,
        TestValue = value
      }.Schedule(inputDeps);
    }

    #endregion Container Contains

    #region Container Memsets

    public static JobHandle Memset<T>(this NativeList<T> container, T value, JobHandle inputDeps)
      where T : struct {

      return new MemsetNativeList<T> {
        Source = container,
        Value = value
      }.Schedule(container.Length, 128, inputDeps);
    }

    public static JobHandle Memset<T>(this NativeCounter container, int value, JobHandle inputDeps)
      where T : struct {

      return new MemsetCounter {
        Counter = container,
        Value = value
      }.Schedule(inputDeps);
    }

    #endregion Container Memsets

    #region Container Sort

    public static JobHandle Sort<T>(this NativeArray<T> container, JobHandle inputDeps)
      where T : struct, IComparable<T> {

      return new SortNativeArray<T> {
        Source = container,
      }.Schedule(inputDeps);
    }

    public static JobHandle Sort<T>(this NativeList<T> container, JobHandle inputDeps)
      where T : struct, IComparable<T> {

      return new SortNativeList<T> {
        Source = container,
      }.Schedule(inputDeps);
    }

    #endregion Container Sort

    public static unsafe ushort GetChar(this NativeString64 str, int pos) {
      var b = &str.buffer.byte0000;
      str.CopyTo(b, out var _, (ushort)pos);
      return b[pos];
    }
  }
}
