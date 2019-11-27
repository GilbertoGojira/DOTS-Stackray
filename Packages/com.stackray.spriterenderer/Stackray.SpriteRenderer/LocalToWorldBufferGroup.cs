using Stackray.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace Stackray.SpriteRenderer {
  public class LocalToWorldBufferGroup : BufferGroup<LocalToWorld, half4x4> {

    protected override JobHandle ExtractValues(ComponentSystemBase system, EntityQuery query, JobHandle inputDeps) {
      inputDeps = new ExtractValuesPerChunk {
        ChunkType = system.GetArchetypeChunkComponentType<LocalToWorld>(true),
        Values = m_values,
        LastSystemVersion = system.LastSystemVersion,
        ExtractAll = m_didChange
      }.Schedule(query, inputDeps);
      return inputDeps;
    }

    [BurstCompile]
    struct ExtractValuesPerChunk : IJobChunk {
      [ReadOnly]
      public ArchetypeChunkComponentType<LocalToWorld> ChunkType;
      [WriteOnly]
      [NativeDisableParallelForRestriction]
      public NativeList<half4x4> Values;
      public uint LastSystemVersion;
      public bool ExtractAll;

      public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
        if (!ExtractAll && !chunk.DidChange(ChunkType, LastSystemVersion))
          return;
        var components = chunk.GetNativeArray(ChunkType);
        for (var i = 0; i < components.Length; ++i)
          Values[firstEntityIndex + i] = new half4x4(components[i].Value);
      }
    }
  }
}
