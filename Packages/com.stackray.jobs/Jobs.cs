using Stackray.Collections;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
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

  [BurstCompile]
  public struct MemsetCounter : IJob {
    [WriteOnly]
    public NativeCounter Counter;
    public int Value;
    public void Execute() {
      Counter.Value = Value;
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

  [BurstCompile]
  public struct ResizeBuferDeferred<T> : IJobForEach_B<T> where T : struct, IBufferElementData {
    [ReadOnly]
    public NativeCounter Length;

    public void Execute(DynamicBuffer<T> buffer) {
      buffer.ResizeUninitialized(Length.Value);
    }
  }

  [BurstCompile]
  public struct ResizeBuffer<T> : IJobForEach_B<T> where T : struct, IBufferElementData {
    public int Length;

    public void Execute(DynamicBuffer<T> buffer) {
      buffer.ResizeUninitialized(Length);
    }
  }

  [BurstCompile]
  public struct ClearNativeList<T> : IJob where T : struct {
    public NativeList<T> Source;
    public int Capacity;
    public void Execute() {
      if (Capacity != default && Capacity > Source.Capacity)
        Source.Capacity = Capacity;
      Source.Clear();
    }
  }

  [BurstCompile]
  public struct ClearNativeQueue<T> : IJob where T : struct {
    public NativeQueue<T> Source;
    public void Execute() {
      Source.Clear();
    }
  }

  [BurstCompile]
  public struct ClearNativeHashMap<T1, T2> : IJob where T1 : struct, IEquatable<T1> where T2 : struct {
    public NativeHashMap<T1, T2> Source;
    public int Capacity;
    public void Execute() {
      if (Capacity != default && Capacity > Source.Capacity)
        Source.Capacity = Capacity;
      Source.Clear();
    }
  }

  [BurstCompile]
  public struct ClearNativeMultiHashMap<T1, T2> : IJob where T1 : struct, IEquatable<T1> where T2 : struct {
    public NativeMultiHashMap<T1, T2> Source;
    public int Capacity;
    public void Execute() {
      if (Capacity != default && Capacity > Source.Capacity)
        Source.Capacity = Capacity;
      Source.Clear();
    }
  }

  [BurstCompile]
  public struct CopyToNativeArrayJob<T> : IJobParallelFor where T : struct {
    [ReadOnly]
    public NativeArray<T> Source;
    [WriteOnly]
    public NativeArray<T> Target;
    public void Execute(int index) {
      Target[index] = Source[index];
    }
  }

  [BurstCompile]
  public struct CopyFromNativeQueue<T> : IJob where T : struct {
    public NativeQueue<T> Source;
    [WriteOnly]
    public NativeList<T> Target;

    public void Execute() {
      while (Source.TryDequeue(out var item))
        Target.Add(item);
    }
  }

  [BurstCompile]
  public struct SortNativeList<T> : IJob where T : struct, IComparable<T> {
    public NativeList<T> NativeList;
    public void Execute() {
      NativeList.Sort();
    }
  }

  [BurstCompile]
  public struct CopyComponentFromEntity<T> : IJobForEachWithEntity<T> where T : struct, IComponentData {
    [WriteOnly]
    public NativeHashMap<Entity, T>.ParallelWriter Result;
    public void Execute(Entity entity, int index, ref T data) {
      Result.TryAdd(entity, data);
    }
  }

  [BurstCompile]
  public struct CopyFromQueueToArray<T> : IJob where T : struct {
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

  [BurstCompile]
  public struct CountBufferElements<T> : IJobForEach_B<T> where T : struct, IBufferElementData {
    [WriteOnly]
    public NativeCounter.Concurrent Counter;
    public void Execute([ReadOnly]DynamicBuffer<T> buffer) {
      Counter.Increment(buffer.Length);
    }
  }
}