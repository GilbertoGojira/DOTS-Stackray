using NUnit.Framework;
using Unity.Collections;
using Stackray.Collections;

public class DisposeNativeCollectionsTest {

  [Test]
  public void DisposeNativeArrayTest() {
    var nativeArray = new NativeArray<int>(100, Allocator.TempJob);
    var inputDeps = nativeArray.Dispose(default);
    inputDeps.Complete();
    Assert.Catch(() => nativeArray.Dispose());
  }

  [Test]
  public void DisposeNativeListTest() {
    var nativeList = new NativeList<int>(100, Allocator.TempJob);
    var inputDeps = nativeList.Dispose(default);
    inputDeps.Complete();
    Assert.Catch(() => nativeList.Dispose());
  }

  [Test]
  public void DisposeNativeQueueTest() {
    var nativeQueue = new NativeQueue<int>(Allocator.TempJob);
    nativeQueue.Enqueue(1);
    var inputDeps = nativeQueue.Dispose(default);
    inputDeps.Complete();
    Assert.Catch(() => nativeQueue.Dispose());
  }

  [Test]
  public void DisposeNativeHashMapTest() {
    var nativeHashMap = new NativeHashMap<int, int>(100, Allocator.TempJob);
    var inputDeps = nativeHashMap.Dispose(default);
    inputDeps.Complete();
    Assert.Catch(() => nativeHashMap.Dispose());
  }

  [Test]
  public void DisposeNativeMultiHashMapTest() {
    var nativeMultiHashMap = new NativeMultiHashMap<int, int>(100, Allocator.TempJob);
    var inputDeps = nativeMultiHashMap.Dispose(default);
    inputDeps.Complete();
    Assert.Catch(() => nativeMultiHashMap.Dispose());
  }

  [Test]
  public void DisposeNativeStream() {
    var nativeStream = new NativeStream(100, Allocator.TempJob);
    var inputDeps = nativeStream.Dispose(default);
    inputDeps.Complete();
    Assert.Catch(() => nativeStream.Dispose());
  }

  [Test]
  public void DisposeNativeHashSet() {
    var nativeHashSet = new NativeHashSet<int>(100, Allocator.TempJob);
    var inputDeps = nativeHashSet.Dispose(default);
    inputDeps.Complete();
    Assert.Catch(() => nativeHashSet.Dispose());
  }

  [Test]
  public void DisposeNativeCounter() {
    var nativeCounter = new NativeCounter(Allocator.TempJob);
    var inputDeps = nativeCounter.Dispose(default);
    inputDeps.Complete();
    Assert.Catch(() => nativeCounter.Dispose());
  }

  [Test]
  public void DisposeNativeUnit() {
    var nativeUnit = new NativeUnit<int>(Allocator.TempJob);
    var inputDeps = nativeUnit.Dispose(default);
    inputDeps.Complete();
    Assert.Catch(() => nativeUnit.Dispose());
  }

}
