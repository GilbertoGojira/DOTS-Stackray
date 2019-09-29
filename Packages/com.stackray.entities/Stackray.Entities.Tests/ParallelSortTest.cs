using NUnit.Framework;
using Stackray.Collections;
using Stackray.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public class ParallelSortTest {

  [BurstCompile]
  struct FillIntArray : IJobParallelFor {
    [WriteOnly]
    public NativeArray<int> Source;
    public uint Seed;
    public void Execute(int index) {
      var random = new Random((uint)(Seed + index));
      Source[index] = random.NextInt();
    }
  }

  [BurstCompile]
  struct FillFloatArray : IJobParallelFor {
    [WriteOnly]
    public NativeArray<float> Source;
    public uint Seed;
    public void Execute(int index) {
      var random = new Random((uint)(Seed + index));
      Source[index] = random.NextFloat();
    }
  }

  [BurstCompile]
  struct ValidateSortedArray<T> : IJobParallelFor where T : struct, System.IComparable<T> {
    [ReadOnly]
    public NativeArray<T> Source;
    [WriteOnly]
    public NativeCounter.Concurrent Counter;
    public void Execute(int index) {
      if (index == Source.Length - 1 || Source[index + 1].CompareTo(Source[index]) > 0)
        Counter.Increment(1);
    }
  }

  static NativeArray<int> GenerateIntNativeArray(int length, out JobHandle inputDeps) {
    var random = new Random((uint)UnityEngine.Random.Range(0, 10000));
    var array = new NativeArray<int>(length, Allocator.TempJob);
    inputDeps = new FillIntArray {
      Source = array,
      Seed = random.NextUInt()
    }.Schedule(array.Length, 128);
    return array;
  }

  static NativeArray<float> GenerateFloatNativeArray(int length, out JobHandle inputDeps) {
    var random = new Random((uint)UnityEngine.Random.Range(0, 10000));
    var array = new NativeArray<float>(length, Allocator.TempJob);
    inputDeps = new FillFloatArray {
      Source = array,
      Seed = random.NextUInt()
    }.Schedule(array.Length, 128);
    return array;
  }

  static void IntSort(int length, int concurrentJobs) {
    var array = GenerateIntNativeArray(length, out var inputDeps);
    var parallelSort = new ParallelSort<int>();
    var counter = new NativeCounter(Allocator.TempJob);
    inputDeps = parallelSort.Sort(array, concurrentJobs, inputDeps);
    inputDeps = new ValidateSortedArray<int> {
      Source = array,
      Counter = counter
    }.Schedule(array.Length, 128, inputDeps);
    inputDeps.Complete();
    var result = counter.Value;
    counter.Dispose();
    array.Dispose();
    parallelSort.Dispose();
    Assert.True(result == length);
  }

  static void FloatSort(int length, int concurrentJobs) {
    var array = GenerateFloatNativeArray(length, out var inputDeps);
    var parallelSort = new ParallelSort<float>();
    var counter = new NativeCounter(Allocator.TempJob);
    inputDeps = parallelSort.Sort(array, concurrentJobs, inputDeps);
    inputDeps = new ValidateSortedArray<float> {
      Source = array,
      Counter = counter
    }.Schedule(array.Length, 128, inputDeps);
    inputDeps.Complete();
    var result = counter.Value;
    counter.Dispose();
    array.Dispose();
    parallelSort.Dispose();
    Assert.True(result == length);
  }

  [Test]
  public void MillionIntsSort() {
    IntSort(1_000_000, System.Environment.ProcessorCount);
  }

  [Test]
  public void MillionFloatsSort() {
    FloatSort(1_000_000, System.Environment.ProcessorCount);
  }

  [Test]
  public void MillionIntsSortSingle() {
    IntSort(1_000_000, 1);
  }

  [Test]
  public void MillionFloatsSortSingle() {
    FloatSort(1_000_000, 1);
  }
}
