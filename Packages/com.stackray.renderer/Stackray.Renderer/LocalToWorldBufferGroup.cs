using Stackray.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace Stackray.Renderer {
  public class LocalToWorldBufferGroup : BufferGroup<LocalToWorld, half4x4> {

    public override JobHandle Update(ComponentSystemBase system, EntityQuery query, JobHandle inputDeps) {
      inputDeps = new ExtractValuesPerChunk {
        ChunkType = system.GetComponentTypeHandle<LocalToWorld>(true),
        Values = m_values,
        LastSystemVersion = system.LastSystemVersion,
        ExtractAll = m_changed
      }.Schedule(query, inputDeps);
      return inputDeps;
    }

    [BurstCompile]
    struct ExtractValuesPerChunk : IJobChunk {
      [ReadOnly]
      public ComponentTypeHandle<LocalToWorld> ChunkType;
      [WriteOnly]
      [NativeDisableParallelForRestriction]
      public NativeArray<half4x4> Values;
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
