using NUnit.Framework;
using Stackray.Collections;
using Stackray.Entities;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public class ParallelSortTest {

  [BurstCompile]
  struct FillIntArray : IJobParallelFor {
    [WriteOnly]
    public NativeArray<DataWithIndex<int>> Target;
    public uint Seed;
    public void Execute(int index) {
      var random = new Random((uint)(Seed + index));
      Target[index] = new DataWithIndex<int>{
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
  struct ValidateSortedArray<T> : IJobParallelFor where T : struct, System.IComparable<T> {
    [ReadOnly]
    public NativeArray<T> Source;
    [WriteOnly]
    public NativeCounter.Concurrent Counter;
    public void Execute(int index) {
      if (index == Source.Length - 1 || Source[index + 1].CompareTo(Source[index]) >= 0)
        Counter.Increment(1);
    }
  }

  static NativeArray<DataWithIndex<int>> GenerateIntNativeArray(int length, out JobHandle inputDeps) {
    var random = new Random((uint)UnityEngine.Random.Range(0, 10000));
    var array = new NativeArray<DataWithIndex<int>>(length, Allocator.TempJob);
    inputDeps = new FillIntArray {
      Target = array,
      Seed = random.NextUInt()
    }.Schedule(array.Length, 128);
    return array;
  }

  static NativeArray<DataWithIndex<float>> GenerateFloatNativeArray(int length, out JobHandle inputDeps) {
    var random = new Random((uint)UnityEngine.Random.Range(0, 10000));
    var array = new NativeArray<DataWithIndex<float>>(length, Allocator.TempJob);
    inputDeps = new FillFloatArray {
      Target = array,
      Seed = random.NextUInt()
    }.Schedule(array.Length, 128);
    return array;
  }

  static DataWithIndex<int>[] IntSort(int[] sourceArray, int concurrentJobs) {
    return IntSort(
      new NativeArray<DataWithIndex<int>>(
        sourceArray.Select((v, i) => new DataWithIndex<int> { Index = i, Value = v }).ToArray(), 
        Allocator.TempJob), 
      concurrentJobs);
  }

  static DataWithIndex<int>[] IntSort(DataWithIndex<int>[] sourceArray, int concurrentJobs) {
    return IntSort(new NativeArray<DataWithIndex<int>>(sourceArray, Allocator.TempJob), concurrentJobs);
  }

  static void IntSort(int length, int concurrentJobs) {
    IntSort(GenerateIntNativeArray(length, out var inputDeps), concurrentJobs, inputDeps);
  }

  static DataWithIndex<int>[] IntSort(NativeArray<DataWithIndex<int>> array, int concurrentJobs, JobHandle inputDeps = default) {
    var length = array.Length;
    var parallelSort = new ParallelSort<DataWithIndex<int>>();
    var counter = new NativeCounter(Allocator.TempJob);
    inputDeps = parallelSort.Sort(array, length, concurrentJobs, inputDeps);
    inputDeps = new ValidateSortedArray<DataWithIndex<int>> {
      Source = parallelSort.SortedData.AsDeferredJobArray(),
      Counter = counter
    }.Schedule(length, 128, inputDeps);
    inputDeps.Complete();
    var sortedArray = parallelSort.SortedData.ToArray();
    var result = counter.Value;
    counter.Dispose();
    array.Dispose();
    parallelSort.Dispose();
    Assert.True(result == length);
    return sortedArray;
  }

  static void FloatSort(int length, int concurrentJobs) {
    var array = GenerateFloatNativeArray(length, out var inputDeps);
    var parallelSort = new ParallelSort<DataWithIndex<float>>();
    var counter = new NativeCounter(Allocator.TempJob);
    inputDeps = parallelSort.Sort(array, array.Length, concurrentJobs, inputDeps: inputDeps);
    inputDeps = new ValidateSortedArray<DataWithIndex<float>> {
      Source = parallelSort.SortedData.AsDeferredJobArray(),
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
  public void FixedIntsSort() {
    var sortedArray = IntSort(new[] { 15, 0, 10, 5, 9, 1, 13, 3, 7, 2, 12, 8, 4, 14, 11, 6, 15 }, System.Environment.ProcessorCount);
    var output = default(string);
    for (var i = 0; i < sortedArray.Length; ++i)
      output += $"({sortedArray[i].Value}) ";
    UnityEngine.Debug.Log(output);
  }

  [Test]
  public void EmptySort() {
    var sortedArray = IntSort(new int[0], System.Environment.ProcessorCount);
    var output = default(string);
    for (var i = 0; i < sortedArray.Length; ++i)
      output += $"({sortedArray[i].Value}) ";
    UnityEngine.Debug.Log(output);
  }

  [Test]
  public void SingleItemSort() {
    var sortedArray = IntSort(new[] { 69 }, System.Environment.ProcessorCount);
    var output = default(string);
    for (var i = 0; i < sortedArray.Length; ++i)
      output += $"({sortedArray[i].Value}) ";
    UnityEngine.Debug.Log(output);
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
  public void IntsSort1Slice() {
    IntSort(1_000, 1);
  }

  [Test]
  public void IntsSort2Slices() {
    IntSort(1_000, 2);
  }

  [Test]
  public void IntsSort3Slices() {
    IntSort(1_000, 3);
  }

  [Test]
  public void IntsSort4Slices() {
    IntSort(1_000, 4);
  }

  [Test]
  public void IntsSort5Slices() {
    IntSort(1_000, 5);
  }

  [Test]
  public void MillionFloatsSort1Slice() {
    FloatSort(1_000_000, 1);
  }

  [Test]
  public void SortMultipleTimes() {

    var length01 = 100;
    var length02 = 1000;
    var parallelSort = new ParallelSort<DataWithIndex<float>>();

    var array01 = GenerateFloatNativeArray(length01, out var inputDeps);
    var counter01 = new NativeCounter(Allocator.TempJob);
    inputDeps = parallelSort.Sort(array01, array01.Length, 8, inputDeps);
    inputDeps = new ValidateSortedArray<DataWithIndex<float>> {
      Source = parallelSort.SortedData.AsDeferredJobArray(),
      Counter = counter01
    }.Schedule(array01.Length, 128, inputDeps);

    var array02 = GenerateFloatNativeArray(length02, out var moreFloatsHandle);
    inputDeps = JobHandle.CombineDependencies(inputDeps, moreFloatsHandle);
    var counter02 = new NativeCounter(Allocator.TempJob);
    inputDeps = parallelSort.Sort(array02, array02.Length, 3, inputDeps);
    inputDeps = new ValidateSortedArray<DataWithIndex<float>> {
      Source = parallelSort.SortedData.AsDeferredJobArray(),
      Counter = counter02
    }.Schedule(array02.Length, 128, inputDeps);

    inputDeps.Complete();
    var result01 = counter01.Value;
    var result02 = counter02.Value;
    counter01.Dispose();
    array01.Dispose();
    counter02.Dispose();
    array02.Dispose();
    parallelSort.Dispose();

    Assert.True(result01 == length01);
    Assert.True(result02 == length02);
  }
}
