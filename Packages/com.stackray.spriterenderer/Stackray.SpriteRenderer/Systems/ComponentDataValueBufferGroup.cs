using Stackray.Entities;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Stackray.SpriteRenderer {
  public class ComponentDataValueBufferGroup<TSource, TTarget> : BufferGroup<TSource, TTarget>
    where TSource : struct, IDynamicBufferProperty<TTarget>
    where TTarget : struct, IEquatable<TTarget> {

    protected override JobHandle ExtractValues(ComponentSystemBase system, EntityQuery query, int instanceOffset, JobHandle inputDeps) {
      inputDeps = new ExtractValuesPerChunk<TSource, TTarget> {
        ChunkType = system.GetArchetypeChunkComponentType<TSource>(true),
        Values = m_values,
        LastSystemVersion = system.LastSystemVersion,
        Offset = instanceOffset
      }.Schedule(query, inputDeps);
      return inputDeps;
    }

    [BurstCompile]
    struct ExtractValuesPerChunk<T1, T2> : IJobChunk
      where T1 : struct, IDynamicBufferProperty<T2>
      where T2 : struct, IEquatable<T2> {

      [ReadOnly]
      public ArchetypeChunkComponentType<T1> ChunkType;
      [WriteOnly]
      [NativeDisableParallelForRestriction]
      public NativeList<T2> Values;
      public uint LastSystemVersion;
      public int Offset;

      public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
        if (!chunk.DidChange(ChunkType, LastSystemVersion))
          return;
        var components = chunk.GetNativeArray(ChunkType);
        for (var i = 0; i < components.Length; ++i)
          Values[firstEntityIndex + i + Offset] = components[i].Value;
      }
    }
  }
}
