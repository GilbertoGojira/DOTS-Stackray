using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Stackray.Entities {

  /// <summary>
  /// Temp struct until Burst supports ValueTuple<>
  /// </summary>
  /// <typeparam name="T1"></typeparam>
  /// <typeparam name="T2"></typeparam>
  /// <typeparam name="T3"></typeparam>
  public struct VTuple<T1, T2, T3>
  where T1 : struct
  where T2 : struct
  where T3 : struct {

    public T1 Item1;
    public T2 Item2;
    public T3 Item3;

    public VTuple(T1 item1, T2 item2, T3 item3) {
      Item1 = item1;
      Item2 = item2;
      Item3 = item3;
    }

    public override string ToString() {
      return $"({Item1}, {Item2}, {Item3})";
    }
  }

  #region Chunk jobs
  [BurstCompile]
  struct GatherChunkChanged<T> : IJobChunk where T : struct, IComponentData {
    [ReadOnly]
    public ArchetypeChunkComponentType<T> ChunkType;
    [WriteOnly]
    public NativeArray<int> ChangedIndices;
    public uint LastSystemVersion;
    public bool ForceChange;
    public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
      if (!ForceChange && !chunk.DidChange(ChunkType, LastSystemVersion))
        return;
      ChangedIndices[chunkIndex] = firstEntityIndex;
    }
  }

  [BurstCompile]
  struct ExtractChangedSlicesFromChunks : IJobParallelFor {
    [ReadOnly]
    public NativeArray<int> Source;
    [ReadOnly]
    [DeallocateOnJobCompletion]
    public NativeArray<ArchetypeChunk> Chunks;
    [WriteOnly]
    public NativeQueue<VTuple<int, int, int>>.ParallelWriter Slices;
    public int Offset;
    public void Execute(int index) {
      if (index > 0 && Source[index - 1] >= 0)
        return;
      var startEntityIndex = Source[index];
      var count = 0;
      var currIndex = index;
      while (currIndex < Source.Length && Source[currIndex] >= 0) {
        count += Chunks[currIndex].Count;
        currIndex++;
      }
      if (count > 0)
        Slices.Enqueue(new VTuple<int, int, int>(startEntityIndex + Offset, startEntityIndex + Offset, count));
    }
  }

  #endregion Chunk jobs

  public static class Extensions {

    public static JobHandle GetChangedChunks<T>(
      this ComponentSystemBase system,
      EntityQuery query,
      Allocator allocator,
      out NativeArray<int> indicesState,
      out NativeQueue<VTuple<int, int, int>> changedSlices,
      JobHandle inputDeps,
      bool changeAll = false,
      int offset = 0)
      where T : struct, IComponentData {

      var chunks = query.CreateArchetypeChunkArray(allocator, out var createChunksHandle);
      inputDeps = JobHandle.CombineDependencies(inputDeps, createChunksHandle);
      indicesState = new NativeArray<int>(chunks.Length, allocator);
      inputDeps = new MemsetNativeArray<int> {
        Source = indicesState,
        Value = -1
      }.Schedule(indicesState.Length, 64, inputDeps);
      inputDeps = new GatherChunkChanged<T> {
        ChunkType = system.GetArchetypeChunkComponentType<T>(true),
        ChangedIndices = indicesState,
        LastSystemVersion = system.LastSystemVersion,
        ForceChange = changeAll
      }.Schedule(query, inputDeps);

      changedSlices = new NativeQueue<VTuple<int, int, int>>(Allocator.TempJob);
      inputDeps = new ExtractChangedSlicesFromChunks {
        Source = indicesState,
        Chunks = chunks,
        Slices = changedSlices.AsParallelWriter(),
        Offset = offset
      }.Schedule(indicesState.Length, 64, inputDeps);
      return inputDeps;
    }
  }
}