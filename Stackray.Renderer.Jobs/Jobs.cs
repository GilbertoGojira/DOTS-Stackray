using Stackray.Collections;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;

namespace Stackray.Renderer {
  [BurstCompile]
  struct FilterChunkWorldRenderBounds<T> : IJob where T : struct, ISharedComponentData {
    [ReadOnly]
    [DeallocateOnJobCompletion]
    public NativeArray<ArchetypeChunk> Chunks;
    [ReadOnly]
    public ArchetypeChunkComponentType<ChunkWorldRenderBounds> ChunkWorldRenderBoundsType;
    [ReadOnly]
    public ArchetypeChunkSharedComponentType<T> FilterType;
    public int SharedComponentIndex;
    public NativeUnit<AABB> ChunkWorldBounds;

    public void Execute() {
      for (var i = 0; i < Chunks.Length; ++i) {
        if (SharedComponentIndex == Chunks[i].GetSharedComponentIndex(FilterType)) {
          MinMaxAABB bounds = ChunkWorldBounds.Value;
          bounds.Encapsulate(Chunks[i].GetChunkComponentData(ChunkWorldRenderBoundsType).Value);
          ChunkWorldBounds.Value = bounds;
        }
      }
    }
  }
}
