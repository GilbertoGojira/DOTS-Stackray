using NUnit.Framework;
using Stackray.Collections;
using Stackray.Entities;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;

public class ParallelSortTest {

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
    IntSort(SortUtilityTest.GenerateIntNativeArray(length, out var inputDeps), concurrentJobs, inputDeps);
  }

  static DataWithIndex<int>[] IntSort(NativeArray<DataWithIndex<int>> array, int concurrentJobs, JobHandle inputDeps = default) {
    var length = array.Length;
    var parallelSort = new ParallelSort<DataWithIndex<int>>();
    var counter = new NativeCounter(Allocator.TempJob);
    inputDeps = parallelSort.Sort(array, length, concurrentJobs, inputDeps);
    inputDeps = SortUtilityTest.CountSortedData(parallelSort.SortedData.AsDeferredJobArray(), counter, length, inputDeps);
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
    var array = SortUtilityTest.GenerateFloatNativeArray(length, out var inputDeps);
    var parallelSort = new ParallelSort<DataWithIndex<float>>();
    var counter = new NativeCounter(Allocator.TempJob);
    inputDeps = parallelSort.Sort(array, array.Length, concurrentJobs, inputDeps: inputDeps);
    inputDeps = SortUtilityTest.CountSortedData(parallelSort.SortedData.AsDeferredJobArray(), counter, array.Length, inputDeps);
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

    var array01 = SortUtilityTest.GenerateFloatNativeArray(length01, out var inputDeps);
    var counter01 = new NativeCounter(Allocator.TempJob);
    inputDeps = parallelSort.Sort(array01, array01.Length, 8, inputDeps);
    inputDeps = SortUtilityTest.CountSortedData(parallelSort.SortedData.AsDeferredJobArray(), counter01, array01.Length, inputDeps);

    var array02 = SortUtilityTest.GenerateFloatNativeArray(length02, out var moreFloatsHandle);
    inputDeps = JobHandle.CombineDependencies(inputDeps, moreFloatsHandle);
    var counter02 = new NativeCounter(Allocator.TempJob);
    inputDeps = parallelSort.Sort(array02, array02.Length, 3, inputDeps);
    inputDeps = SortUtilityTest.CountSortedData(parallelSort.SortedData.AsDeferredJobArray(), counter02, array02.Length, inputDeps);

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
