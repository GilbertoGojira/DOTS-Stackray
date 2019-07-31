using Stackray.Collections;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Stackray.Jobs {
  [BurstCompile]
  public struct Contains<T> : IJob
  where T : struct, IEquatable<T> {
    [ReadOnly]
    public NativeArray<T> Values;
    [WriteOnly]
    public NativeUnit<bool> Result;
    public T TestValue;
    public void Execute() {
      Result.Value = Values.Contains(TestValue);
    }
  }

  [BurstCompile]
  public struct ExtractIndices<T> : IJobParallelFor
    where T : struct, IEquatable<T> {
    [ReadOnly]
    public NativeArray<T> Values;
    [WriteOnly]
    public NativeQueue<int>.ParallelWriter Result;
    public T TestValue;
    public void Execute(int index) {
      if (Values[index].Equals(TestValue))
        Result.Enqueue(index);
    }
  }

  [BurstCompile]
  public struct ExpandNativeList<T> : IJob where T : struct {
    public NativeList<T> Source;
    public int Size;

    public void Execute() {
      if (Source.Length == Size)
        return;
      Source.Capacity = Size;
      while (Source.Length != Size)
        Source.Add(default);
    }
  }

  [BurstCompile]
  public struct MemsetNativeList<T> : IJobParallelFor where T : struct {
    [WriteOnly]
    public NativeList<T> Source;
    public T Value;
    public void Execute(int index) {
      Source[index] = Value;
    }
  }

  [BurstCompile]
  public struct ResizeNativeList<T> : IJob where T : struct {
    public NativeList<T> Source;
    public int Length;
    public void Execute() {
      Source.ResizeUninitialized(Length);
    }
  }
}