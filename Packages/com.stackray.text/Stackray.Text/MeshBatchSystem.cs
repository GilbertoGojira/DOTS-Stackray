using Stackray.Collections;
using Stackray.Jobs;
using Stackray.SpriteRenderer;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Stackray.Text {
  public class MeshBatchSystem : JobComponentSystem {

    struct OffsetInfo {
      public int Vertex;
      public int Triangle;
    }

    EntityQuery m_canvasdRootQuery;
    EntityQuery m_rendererQuery;
    EntityQuery m_vertexDataQuery;

    NativeList<BatchVertex> m_vertices;
    NativeList<BatchVertexIndex> m_triangles;
    NativeCounter VertexCounter;
    NativeCounter VertexIndexCounter;

    protected override void OnCreate() {
      m_vertices = new NativeList<BatchVertex>(10000, Allocator.Persistent);
      m_triangles = new NativeList<BatchVertexIndex>(10000, Allocator.Persistent);
      VertexCounter = new NativeCounter(Allocator.Persistent);
      VertexIndexCounter = new NativeCounter(Allocator.Persistent);

      m_canvasdRootQuery = GetEntityQuery(
        ComponentType.ReadWrite<BatchVertex>(),
        ComponentType.ReadWrite<BatchVertexIndex>(),
        ComponentType.ReadWrite<SubMeshInfo>());
      m_vertexDataQuery = GetEntityQuery(
        ComponentType.ReadOnly<TextRenderer>(),
        ComponentType.ReadOnly<Vertex>(),
        ComponentType.ReadOnly<VertexIndex>());
    }

    protected override void OnDestroy() {
      m_vertices.Dispose();
      m_triangles.Dispose();
      VertexCounter.Dispose();
      VertexIndexCounter.Dispose();
    }

    [BurstCompile]
    struct GatherVertexOffsets : IJobForEachWithEntity_EBB<Vertex, VertexIndex> {
      [NativeDisableParallelForRestriction]
      public NativeArray<OffsetInfo> Offsets;
      [WriteOnly]
      public NativeCounter.Concurrent VertexCounter;
      [WriteOnly]
      public NativeCounter.Concurrent VertexIndexCounter;
      public void Execute(Entity entity, int index, [ReadOnly, ChangedFilter] DynamicBuffer<Vertex> vertexData, [ReadOnly, ChangedFilter] DynamicBuffer<VertexIndex> vertexIndex) {
        var previousOffset = index > 0 ? Offsets[index - 1] : default;
        Offsets[index] = new OffsetInfo {
          Vertex = previousOffset.Vertex + vertexData.Length,
          Triangle = previousOffset.Triangle + vertexIndex.Length
        };
        VertexCounter.Increment(vertexData.Length);
        VertexIndexCounter.Increment(vertexIndex.Length);
      }
    }

    [BurstCompile]
    private struct FastMeshBatching : IJobForEachWithEntity_EBBC<Vertex, VertexIndex, TextRenderer> {
      [ReadOnly]
      [DeallocateOnJobCompletion]
      public NativeArray<OffsetInfo> Offsets;

      [DeallocateOnJobCompletion]
      [ReadOnly]
      public NativeArray<Entity> CanvasEntities;
      [NativeDisableParallelForRestriction]
      public BufferFromEntity<BatchVertex> MeshVertexFromEntity;
      [NativeDisableParallelForRestriction]
      public BufferFromEntity<BatchVertexIndex> MeshVertexIndexFromEntity;
      [NativeDisableParallelForRestriction]
      public BufferFromEntity<SubMeshInfo> SubMeshInfoFromEntity;

      public void Execute(Entity entity, int index, [ReadOnly, ChangedFilter] DynamicBuffer<Vertex> vertexData, [ReadOnly, ChangedFilter] DynamicBuffer<VertexIndex> vertexIndex, [ReadOnly, ChangedFilter] ref TextRenderer textRenderer) {
        for (var batcherIndex = 0; batcherIndex < CanvasEntities.Length; ++batcherIndex) {
          var canvasEntity = CanvasEntities[batcherIndex];
          if (textRenderer.CanvasEntity != canvasEntity)
            continue;

          var vertices = MeshVertexFromEntity[canvasEntity];
          var vertexIndices = MeshVertexIndexFromEntity[canvasEntity];
          var subMeshes = SubMeshInfoFromEntity[canvasEntity];

          var currOffset = index > 0 ? Offsets[index - 1] : default;
          for (var i = 0; i < vertexData.Length; ++i)
            vertices[i + currOffset.Vertex] = vertexData[i];
          for (var i = 0; i < vertexIndex.Length; ++i) {
            var value = vertexIndex[i].Value + currOffset.Vertex;
            vertexIndices[i + currOffset.Triangle] = new BatchVertexIndex() {
              Value = value
            };
          }
          subMeshes.Add(new SubMeshInfo() {
            Offset = currOffset.Triangle,
            MaterialId = textRenderer.MaterialId
          });
        }
      }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {

      VertexCounter.Value = 0;
      VertexIndexCounter.Value = 0;
      var offsets = new NativeArray<OffsetInfo>(m_vertexDataQuery.CalculateEntityCount(), Allocator.TempJob);

      inputDeps = new GatherVertexOffsets {
        Offsets = offsets,
        VertexCounter = VertexCounter,
        VertexIndexCounter = VertexIndexCounter,
      }.ScheduleSingle(m_vertexDataQuery, inputDeps);

      inputDeps = JobHandle.CombineDependencies(
        new ResizeBuferDeferred<BatchVertex> {
          Length = VertexCounter
        }.Schedule(m_canvasdRootQuery, inputDeps),
        new ResizeBuferDeferred<BatchVertexIndex> {
          Length = VertexIndexCounter
        }.Schedule(m_canvasdRootQuery, inputDeps),
        new ResizeBuffer<SubMeshInfo> {
          Length = 0
        }.Schedule(m_canvasdRootQuery, inputDeps));

      inputDeps = new FastMeshBatching {
        CanvasEntities = m_canvasdRootQuery.ToEntityArray(Allocator.TempJob),
        MeshVertexFromEntity = GetBufferFromEntity<BatchVertex>(false),
        MeshVertexIndexFromEntity = GetBufferFromEntity<BatchVertexIndex>(false),
        SubMeshInfoFromEntity = GetBufferFromEntity<SubMeshInfo>(false),
        Offsets = offsets,
      }.Schedule(m_vertexDataQuery, inputDeps);

      return inputDeps;
    }
  }
}
