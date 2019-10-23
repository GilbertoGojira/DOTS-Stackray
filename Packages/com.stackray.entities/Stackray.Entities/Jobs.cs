using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Stackray.Entities {
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

  [BurstCompile]
  struct ConvertToDataWithIndex<T> : IJobParallelFor where T : struct, IComparable<T> {
    [ReadOnly]
    public NativeArray<T> Source;
    [WriteOnly]
    public NativeArray<DataWithIndex<T>> Target;
    public void Execute(int index) {
      Target[index] = new DataWithIndex<T> {
        Index = index,
        Value = Source[index]
      };
    }
  }

  [BurstCompile]
  struct ConvertToDataWithEntity<TData, TComponentData> : IJobForEachWithEntity<TComponentData>
    where TData : struct, IComparable<TData>
    where TComponentData : struct, IComponentData {

    [ReadOnly]
    public NativeArray<TData> Source;
    [WriteOnly]
    public NativeArray<DataWithEntity<TData>> Target;
    public void Execute(Entity entity, int index, ref TComponentData component) {
      Target[index] = new DataWithEntity<TData> {
        Entity = entity,
        Index = index,
        Value = Source[index]
      };
    }
  }

  [BurstCompile]
  public struct GatherSharedComponentIndices<T> : IJobChunk where T : struct, ISharedComponentData {
    [ReadOnly]
    public ArchetypeChunkSharedComponentType<T> ChunkSharedComponentType;
    [WriteOnly]
    public NativeArray<int> Indices;
    public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
      var sharedComponentIndex = chunk.GetSharedComponentIndex(ChunkSharedComponentType);
      for (var i = 0; i < chunk.Count; ++i)
        Indices[firstEntityIndex + i] = sharedComponentIndex;
    }
  }

  [BurstCompile]
  struct GatherEntityIndexMap : IJobChunk {
    [ReadOnly]
    public ArchetypeChunkEntityType EntityType;
    [WriteOnly]
    public NativeHashMap<Entity, int>.ParallelWriter EntityIndexMap;

    public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
      var entities = chunk.GetNativeArray(EntityType);
      for (var i = 0; i < entities.Length; ++i)
        EntityIndexMap.TryAdd(entities[i], firstEntityIndex + i);
    }
  }

  [BurstCompile]
  struct DestroyEntitiesOnly : IJobChunk {
    public EntityArchetype EntityOnlyArchetype;
    [ReadOnly]
    public ArchetypeChunkEntityType EntityType;
    [WriteOnly]
    public EntityCommandBuffer.Concurrent CmdBuffer;
    public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
      var entities = chunk.GetNativeArray(EntityType);
      for (var i = 0; i < chunk.Count; i++)
        if (chunk.Archetype == EntityOnlyArchetype)
          CmdBuffer.DestroyEntity(firstEntityIndex + i, entities[i]);
    }
  }

}
