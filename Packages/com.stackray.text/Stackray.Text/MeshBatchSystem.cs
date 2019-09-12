using Stackray.Collections;
using Stackray.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Stackray.Text {

  [UpdateAfter(typeof(TextMeshBuildSystem))]
  public class MeshBatchSystem : JobComponentSystem {

    struct OffsetInfo {
      public int Vertex;
      public int Triangle;
    }

    EntityQuery m_canvasdRootQuery;
    EntityQuery m_rendererQuery;
    EntityQuery m_vertexDataQuery;

    NativeList<Vertex> m_vertices;
    NativeList<VertexIndex> m_triangles;
    NativeCounter VertexCounter;
    NativeCounter VertexIndexCounter;

    protected override void OnCreate() {
      m_vertices = new NativeList<Vertex>(10000, Allocator.Persistent);
      m_triangles = new NativeList<VertexIndex>(10000, Allocator.Persistent);
      VertexCounter = new NativeCounter(Allocator.Persistent);
      VertexIndexCounter = new NativeCounter(Allocator.Persistent);

      m_canvasdRootQuery = GetEntityQuery(
        ComponentType.ReadWrite<Vertex>(),
        ComponentType.ReadWrite<VertexIndex>(),
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
      public void Execute(
        Entity entity,
        int index,
        [ReadOnly] DynamicBuffer<Vertex> vertexData,
        [ReadOnly] DynamicBuffer<VertexIndex> vertexIndex) {

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
    private struct MeshBatching : IJobForEachWithEntity<TextRenderer> {
      [ReadOnly]
      [DeallocateOnJobCompletion]
      public NativeArray<OffsetInfo> Offsets;

      [DeallocateOnJobCompletion]
      [ReadOnly]
      public NativeArray<Entity> CanvasEntities;
      [NativeDisableParallelForRestriction]
      public BufferFromEntity<Vertex> MeshVertexFromEntity;
      [NativeDisableParallelForRestriction]
      public BufferFromEntity<VertexIndex> MeshVertexIndexFromEntity;
      [NativeDisableParallelForRestriction]
      public BufferFromEntity<SubMeshInfo> SubMeshInfoFromEntity;

      public void Execute(
        Entity entity,
        int index,
        [ReadOnly]ref TextRenderer textRenderer) {

        var vertexData = MeshVertexFromEntity[entity];
        var vertexIndex = MeshVertexIndexFromEntity[entity];

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
            vertexIndices[i + currOffset.Triangle] = new VertexIndex() {
              Value = value
            };
          }
          subMeshes[index] = new SubMeshInfo() {
            Offset = currOffset.Triangle,
            MaterialId = textRenderer.MaterialId
          };
        }
      }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {

      VertexCounter.Value = 0;
      VertexIndexCounter.Value = 0;
      var changedVerticesCount = m_vertexDataQuery.CalculateEntityCount();
      if (changedVerticesCount == 0)
        return inputDeps;

      m_vertexDataQuery.ResetFilter();
      var offsets = new NativeArray<OffsetInfo>(m_vertexDataQuery.CalculateEntityCount(), Allocator.TempJob);

      inputDeps = new GatherVertexOffsets {
        Offsets = offsets,
        VertexCounter = VertexCounter,
        VertexIndexCounter = VertexIndexCounter,
      }.ScheduleSingle(m_vertexDataQuery, inputDeps);

      inputDeps = JobHandle.CombineDependencies(
        new ResizeBuferDeferred<Vertex> {
          Length = VertexCounter
        }.Schedule(m_canvasdRootQuery, inputDeps),
        new ResizeBuferDeferred<VertexIndex> {
          Length = VertexIndexCounter
        }.Schedule(m_canvasdRootQuery, inputDeps),
        new ResizeBuffer<SubMeshInfo> {
          Length = offsets.Length
        }.Schedule(m_canvasdRootQuery, inputDeps));

      inputDeps = new MeshBatching {
        CanvasEntities = m_canvasdRootQuery.ToEntityArray(Allocator.TempJob),
        MeshVertexFromEntity = GetBufferFromEntity<Vertex>(false),
        MeshVertexIndexFromEntity = GetBufferFromEntity<VertexIndex>(false),
        SubMeshInfoFromEntity = GetBufferFromEntity<SubMeshInfo>(false),
        Offsets = offsets,
      }.Schedule(m_vertexDataQuery, inputDeps);

      m_vertexDataQuery.SetFilterChanged(new ComponentType[] { ComponentType.ReadOnly<Vertex>(), ComponentType.ReadOnly<VertexIndex>() });
      return inputDeps;
    }
  }
}
