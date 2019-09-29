using NUnit.Framework;
using Stackray.Entities;
using Unity.Collections;
using Unity.Mathematics;

public class ParallelSortTest {

  // A Test behaves as an ordinary method
  [Test]
  public void MillionIntsSort() {
    var random = new Random((uint)UnityEngine.Random.Range(0, 10000));
    var parallelSort = new ParallelSort<int>();
    var array = new NativeArray<int>(1000000, Allocator.TempJob);
    for (var i = 0; i < array.Length - 1; ++i)
      array[i] = random.NextInt();
    parallelSort.Sort(array, System.Environment.ProcessorCount, default).Complete();
    var result = true;
    for (var i = 0; i < array.Length - 1; ++i)
       result &= array[i + 1] >= array[i];
    array.Dispose();
    parallelSort.Dispose();
    Assert.True(result);
  }

  [Test]
  public void MillionFloatsSort() {
    var random = new Random((uint)UnityEngine.Random.Range(0, 10000));
    var parallelSort = new ParallelSort<float>();
    var array = new NativeArray<float>(1000000, Allocator.TempJob);
    for (var i = 0; i < array.Length - 1; ++i)
      array[i] = random.NextFloat();
    parallelSort.Sort(array, System.Environment.ProcessorCount, default).Complete();
    var result = true;
    for (var i = 0; i < array.Length - 1; ++i)
      result &= array[i + 1] >= array[i];
    array.Dispose();
    parallelSort.Dispose();
    Assert.True(result);
  }

  [Test]
  public void MillionIntsSortSingle() {
    var random = new Random((uint)UnityEngine.Random.Range(0, 10000));
    var parallelSort = new ParallelSort<int>();
    var array = new NativeArray<int>(1000000, Allocator.TempJob);
    for (var i = 0; i < array.Length - 1; ++i)
      array[i] = random.NextInt();
    parallelSort.Sort(array, 1, default).Complete();
    var result = true;
    for (var i = 0; i < array.Length - 1; ++i)
      result &= array[i + 1] >= array[i];
    array.Dispose();
    parallelSort.Dispose();
    Assert.True(result);
  }

  [Test]
  public void MillionFloatsSortSingle() {
    var random = new Random((uint)UnityEngine.Random.Range(0, 10000));
    var parallelSort = new ParallelSort<float>();
    var array = new NativeArray<float>(1000000, Allocator.TempJob);
    for (var i = 0; i < array.Length - 1; ++i)
      array[i] = random.NextFloat();
    parallelSort.Sort(array, 1, default).Complete();
    var result = true;
    for (var i = 0; i < array.Length - 1; ++i)
      result &= array[i + 1] >= array[i];
    array.Dispose();
    parallelSort.Dispose();
    Assert.True(result);
  }
}
