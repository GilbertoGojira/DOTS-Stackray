using NUnit.Framework;
using Stackray.Collections;
using Stackray.Entities;
using System;
using System.Linq;
using Unity.Collections;

public class TimeSpanParallelSortTest
{

  static DataWithIndex<int>[] IntSort(int[] sourceArray, int processAmount, int concurrentJobs) {
    return Sort(
      new NativeArray<DataWithIndex<int>>(
        sourceArray.Select((v, i) => new DataWithIndex<int> { Index = i, Value = v }).ToArray(),
        Allocator.TempJob),
      processAmount,
      concurrentJobs);
  }

  static DataWithIndex<T>[] Sort<T>(NativeArray<DataWithIndex<T>> array, int processAmount, int concurrentJobs) 
    where T : struct, IComparable<T> {
    var length = array.Length;
    var timeSpanParallelSort = new TimeSpanParallelSort<DataWithIndex<T>>();
    var counter = new NativeCounter(Allocator.TempJob);
    var cycles = 0;
    timeSpanParallelSort.Start(array, processAmount, concurrentJobs).Complete();
    while (!timeSpanParallelSort.IsComplete) {
      timeSpanParallelSort.Update().Complete();
      cycles++;
    }

    SortUtilityTest
      .CountSortedData(timeSpanParallelSort.SortedData.AsDeferredJobArray(), counter, length, default)
      .Complete();
    var sortedArray = timeSpanParallelSort.SortedData.ToArray();
    var result = counter.Value;
    counter.Dispose();
    array.Dispose();
    timeSpanParallelSort.Dispose();
    Assert.True(result == length);
    UnityEngine.Debug.Log($"Sorted array in {cycles} cycles");
    return sortedArray;
  }

  static void IntSort(int length, int processAmount, int concurrentJobs) {
    var array = SortUtilityTest.GenerateIntNativeArray(length, out var inputDeps);
    inputDeps.Complete();
    Sort(array, processAmount, concurrentJobs);
  }

  static void FloatSort(int length, int processAmount, int concurrentJobs) {
    var array = SortUtilityTest.GenerateFloatNativeArray(length, out var inputDeps);
    inputDeps.Complete();
    Sort(array, processAmount, concurrentJobs);
  }

  [Test]
  public void FixedIntsSort() {
    var sortedArray = IntSort(new[] { 15, 0, 10, 5, 9, 1, 13, 3, 7, 2, 12, 8, 4, 14, 11, 6, 15 }, 2, Environment.ProcessorCount);
    var output = default(string);
    for (var i = 0; i < sortedArray.Length; ++i)
      output += $"({sortedArray[i].Value}) ";
    UnityEngine.Debug.Log(output);
  }

  [Test]
  public void MillionIntsSort() {
    IntSort(1000_000, 100_000, Environment.ProcessorCount);
  }

  [Test]
  public void MillionFloatsSort() {
    IntSort(2000_000, 131_072, Environment.ProcessorCount);
  }

  [Test]
  public void EmptySort() {
    var sortedArray = IntSort(new int[0], 2, Environment.ProcessorCount);
    var output = default(string);
    for (var i = 0; i < sortedArray.Length; ++i)
      output += $"({sortedArray[i].Value}) ";
    UnityEngine.Debug.Log(output);
  }
}
