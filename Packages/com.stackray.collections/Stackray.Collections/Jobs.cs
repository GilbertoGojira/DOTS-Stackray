using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Stackray.Collections {

  #region Clear Native Containers
  [BurstCompile]
  struct ClearNativeList<T> : IJob where T : struct {
    public NativeList<T> Source;
    public int Capacity;
    public void Execute() {
      if (Capacity != default && Capacity > Source.Capacity)
        Source.Capacity = Capacity;
      Source.Clear();
    }
  }

  [BurstCompile]
  struct ClearNativeQueue<T> : IJob where T : struct {
    public NativeQueue<T> Source;
    public void Execute() {
      Source.Clear();
    }
  }

  [BurstCompile]
  struct ClearNativeHashMap<T1, T2> : IJob where T1 : struct, IEquatable<T1> where T2 : struct {
    public NativeHashMap<T1, T2> Source;
    public int Capacity;
    public void Execute() {
      if (Capacity != default && Capacity > Source.Capacity)
        Source.Capacity = Capacity;
      Source.Clear();
    }
  }

  [BurstCompile]
  struct ClearNativeMultiHashMap<T1, T2> : IJob where T1 : struct, IEquatable<T1> where T2 : struct {
    public NativeMultiHashMap<T1, T2> Source;
    public int Capacity;
    public void Execute() {
      if (Capacity != default && Capacity > Source.Capacity)
        Source.Capacity = Capacity;
      Source.Clear();
    }
  }

  [BurstCompile]
  struct ClearNativeHashSet<T1> : IJob where T1 : struct, IEquatable<T1> {
    public NativeHashSet<T1> Source;
    public void Execute() {
      Source.Clear();
    }
  }
  #endregion Clear Native Containers

  #region Resize Native Containers

  [BurstCompile]
  struct ResizeNativeList<T> : IJob where T : struct {
    public NativeList<T> Source;
    public int Length;
    public void Execute() {
      Source.ResizeUninitialized(Length);
    }
  }

  [BurstCompile]
  struct ResizeNativeListFromNativeQueueSize<T> : IJob where T : struct {
    public NativeList<T> Source;
    [ReadOnly]
    public NativeQueue<T> Target;
    public void Execute() {
      Source.ResizeUninitialized(Target.Count);
    }
  }

  [BurstCompile]
  struct ResizeNativeListFromNativeArraySize<T> : IJob where T : struct {
    public NativeList<T> Source;
    [ReadOnly]
    public NativeArray<T> Target;
    public void Execute() {
      Source.ResizeUninitialized(Target.Length);
    }
  }

  [BurstCompile]
  public struct MemsetResizeNativeList<T> : IJob where T : struct {
    public NativeList<T> Source;
    public T Value;

    public void Execute() {
      while (Source.Length < Source.Capacity)
        Source.Add(Value);
      for (var i = 0; i < Source.Length; ++i)
        Source[i] = Value;
    }
  }

  #endregion Resize Native Containers

  #region Copy Between Containers

  [BurstCompile]
  struct CopyToNativeArray<T> : IJobParallelFor where T : struct {
    [ReadOnly]
    public NativeArray<T> Source;
    [WriteOnly]
    public NativeArray<T> Target;
    public int SourceOffset;
    public int TargetOffset;
    public void Execute(int index) {
      Target[index + TargetOffset] = Source[index + SourceOffset];
    }
  }

  [BurstCompile]
  struct CopyFromNativeQueue<T> : IJob where T : struct {
    public NativeQueue<T> Source;
    [WriteOnly]
    public NativeList<T> Target;

    public void Execute() {
      while (Source.TryDequeue(out var item))
        Target.Add(item);
    }
  }

  [BurstCompile]
  struct CopyFromNativeQueueToArray<T> : IJob where T : struct {
    public NativeQueue<T> Source;
    [WriteOnly]
    public NativeArray<T> Target;
    public void Execute() {
      var i = 0;
      while (Source.TryDequeue(out var item)) {
        Target[i] = item;
        ++i;
      }
    }
  }

  #endregion Copy Between Containers

  #region Container Contains

  [BurstCompile]
  struct Contains<T> : IJob where T : struct, IEquatable<T> {
    [ReadOnly]
    public NativeArray<T> Source;
    [WriteOnly]
    public NativeUnit<bool> Result;
    public T TestValue;
    public void Execute() {
      Result.Value = Source.Contains(TestValue);
    }
  }

  #endregion Container Contains

  #region Container Memsets

  [BurstCompile]
  struct MemsetNativeList<T> : IJobParallelFor where T : struct {
    [WriteOnly]
    public NativeList<T> Source;
    public T Value;
    public void Execute(int index) {
      Source[index] = Value;
    }
  }

  [BurstCompile]
  struct MemsetCounter : IJob {
    [WriteOnly]
    public NativeCounter Counter;
    public int Value;
    public void Execute() {
      Counter.Value = Value;
    }
  }

  #endregion Container Memsets

  #region Container Sort

  [BurstCompile]
  struct SortNativeArray<T> : IJob where T : struct, IComparable<T> {
    public NativeArray<T> Source;
    public void Execute() {
      Source.Sort();
    }
  }

  [BurstCompile]
  struct SortNativeList<T> : IJob where T : struct, IComparable<T> {
    public NativeList<T> Source;
    public void Execute() {
      Source.Sort();
    }
  }

  #endregion Container Sort
}
