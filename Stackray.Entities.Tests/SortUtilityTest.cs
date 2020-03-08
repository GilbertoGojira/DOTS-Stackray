using Stackray.Collections;
using Stackray.Entities;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

public static class SortUtilityTest {

  #region Jobs
  [BurstCompile]
  struct FillIntArray : IJobParallelFor {
    [WriteOnly]
    public NativeArray<DataWithIndex<int>> Target;
    public uint Seed;
    public void Execute(int index) {
      var random = new Random((uint)(Seed + index));
      Target[index] = new DataWithIndex<int> {
        Index = index,
        Value = random.NextInt()
      };
    }
  }

  [BurstCompile]
  struct FillFloatArray : IJobParallelFor {
    [WriteOnly]
    public NativeArray<DataWithIndex<float>> Target;
    public uint Seed;
    public void Execute(int index) {
      var random = new Random((uint)(Seed + index));
      Target[index] = new DataWithIndex<float> {
        Index = index,
        Value = random.NextFloat()
      };
    }
  }

  [BurstCompile]
  struct ValidateSortedArray<T> : IJobParallelFor where T : struct, IComparable<T> {
    [ReadOnly]
    public NativeArray<T> Source;
    [WriteOnly]
    public NativeCounter.Concurrent Counter;
    public void Execute(int index) {
      if (index == Source.Length - 1 || Source[index + 1].CompareTo(Source[index]) >= 0)
        Counter.Increment(1);
    }
  }

  #endregion jobs

  public static NativeArray<DataWithIndex<int>> GenerateIntNativeArray(int length, out JobHandle inputDeps) {
    var random = new Random((uint)UnityEngine.Random.Range(0, 10000));
    var array = new NativeArray<DataWithIndex<int>>(length, Allocator.TempJob);
    inputDeps = new FillIntArray {
      Target = array,
      Seed = random.NextUInt()
    }.Schedule(array.Length, 128);
    return array;
  }

  public static NativeArray<DataWithIndex<float>> GenerateFloatNativeArray(int length, out JobHandle inputDeps) {
    var random = new Random((uint)UnityEngine.Random.Range(0, 10000));
    var array = new NativeArray<DataWithIndex<float>>(length, Allocator.TempJob);
    inputDeps = new FillFloatArray {
      Target = array,
      Seed = random.NextUInt()
    }.Schedule(array.Length, 128);
    return array;
  }

  public static JobHandle CountSortedData<T>(NativeArray<T> input, NativeCounter output, int length, JobHandle inputDeps)
    where T : struct, IComparable<T> {

    return new ValidateSortedArray<T> {
      Source = input,
      Counter = output
    }.Schedule(length, 128, inputDeps);
  }
}
