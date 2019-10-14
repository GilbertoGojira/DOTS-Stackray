﻿using Stackray.Collections;
using Stackray.Entities;
using Stackray.Jobs;
using Stackray.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Stackray.Text {

  [UpdateAfter(typeof(TextMeshBuildSystem))]
  public class TextMeshBatchSystem : JobComponentSystem {

    struct OffsetInfo {
      public int Vertex;
      public int VertexCount;
      public int Indices;
      public int SubMeshIndex;
      public int SubMesh;
      public int SubMeshMaterialId;
    }

    EntityQuery m_canvasdRootQuery;
    EntityQuery m_rendererQuery;
    EntityQuery m_vertexDataQuery;

    NativeList<Vertex> m_vertices;
    NativeList<VertexIndex> m_triangles;
    NativeCounter m_vertexCounter;
    NativeCounter m_vertexIndexCounter;
    NativeCounter m_subMeshCounter;

    NativeList<OffsetInfo> m_offsets;
    NativeList<int> m_sharedFontIndices;

    protected override void OnCreate() {
      m_vertices = new NativeList<Vertex>(10000, Allocator.Persistent);
      m_triangles = new NativeList<VertexIndex>(10000, Allocator.Persistent);
      m_vertexCounter = new NativeCounter(Allocator.Persistent);
      m_vertexIndexCounter = new NativeCounter(Allocator.Persistent);
      m_subMeshCounter = new NativeCounter(Allocator.Persistent);
      m_offsets = new NativeList<OffsetInfo>(0, Allocator.Persistent);
      m_sharedFontIndices = new NativeList<int>(0, Allocator.Persistent);

      m_canvasdRootQuery = GetEntityQuery(
        ComponentType.ReadWrite<Vertex>(),
        ComponentType.ReadWrite<VertexIndex>(),
        ComponentType.ReadWrite<SubMeshInfo>());
      m_vertexDataQuery = GetEntityQuery(
        ComponentType.ReadOnly<FontMaterial>(),
        ComponentType.ReadOnly<TextRenderer>(),
        ComponentType.ReadOnly<Vertex>(),
        ComponentType.ReadOnly<VertexIndex>(),
        ComponentType.ReadOnly<SortIndex>());
    }

    protected override void OnDestroy() {
      m_vertices.Dispose();
      m_triangles.Dispose();
      m_vertexCounter.Dispose();
      m_vertexIndexCounter.Dispose();
      m_subMeshCounter.Dispose();
      m_offsets.Dispose();
      m_sharedFontIndices.Dispose();
    }

    [BurstCompile]
    struct CreateOffsets : IJob {
      [ReadOnly]
      public NativeArray<Entity> Entities;
      [ReadOnly]
      public NativeArray<SortIndex> SortedIndices;
      [ReadOnly]
      public NativeArray<int> SharedComponentIndices;
      [ReadOnly]
      public BufferFromEntity<Vertex> Vertices;
      [ReadOnly]
      public BufferFromEntity<VertexIndex> VertexIndices;
      [WriteOnly]
      public NativeArray<OffsetInfo> Offsets;
      public NativeCounter VertexCounter;
      public NativeCounter VertexIndexCounter;
      public NativeCounter SubMeshCounter;
      public void Execute() {
        for (var i = 0; i < Entities.Length; ++i) {
          var prevIndex = i > 0 ? SortedIndices[SortedIndices.Length - i].Value : -1;
          var index = SortedIndices[SortedIndices.Length - i - 1].Value;
          var entity = Entities[index];
          var vertexData = Vertices[entity];
          var vertexIndexData = VertexIndices[entity];
          var sharedComponentIndex = SharedComponentIndices[index];
          var prevSharedComponentIndex = prevIndex >= 0 ? SharedComponentIndices[prevIndex] : -1;
          Offsets[index] = new OffsetInfo {
            Vertex = VertexCounter.Value,
            VertexCount = vertexData.Length,
            Indices = VertexIndexCounter.Value,
            SubMeshIndex = sharedComponentIndex != prevSharedComponentIndex ? SubMeshCounter.Value : -1,
            SubMesh = sharedComponentIndex != prevSharedComponentIndex ? VertexIndexCounter.Value : -1,
            SubMeshMaterialId = sharedComponentIndex != prevSharedComponentIndex ? sharedComponentIndex : -1
          };
          if (sharedComponentIndex != prevSharedComponentIndex)
            SubMeshCounter.Increment(1);
          VertexCounter.Increment(vertexData.Length);
          VertexIndexCounter.Increment(vertexIndexData.Length);
        }
      }
    }

    [BurstCompile]
    struct GatherVertexOffsets : IJobForEachWithEntity_EBBC<Vertex, VertexIndex, SortIndex> {
      public int SubMeshIndex;
      public int FontMaterialIndex;
      [NativeDisableParallelForRestriction]
      public NativeArray<OffsetInfo> Offsets;
      public NativeCounter VertexCounter;
      public NativeCounter VertexIndexCounter;

      public void Execute(
        Entity entity,
        int index,
        [ReadOnly] DynamicBuffer<Vertex> vertexData,
        [ReadOnly] DynamicBuffer<VertexIndex> vertexIndex,
        [ReadOnly] ref SortIndex sortIndex) {

        var subMeshOffset = VertexIndexCounter.Value;
        Offsets[Offsets.Length - sortIndex.Value - 1] = new OffsetInfo {
          Vertex = VertexCounter.Value,
          VertexCount = vertexData.Length,
          Indices = VertexIndexCounter.Value,
          SubMeshIndex = index == 0 ? SubMeshIndex : -1,
          SubMesh = index == 0 ? subMeshOffset : -1,
          SubMeshMaterialId = index == 0 ? FontMaterialIndex : -1
        };
        VertexCounter.Increment(vertexData.Length);
        VertexIndexCounter.Increment(vertexIndex.Length);
      }
    }

    [BurstCompile]
    private struct MeshBatching : IJobForEachWithEntity<TextRenderer> {
      [ReadOnly]
      public NativeArray<OffsetInfo> Offsets;
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
          var vertices = MeshVertexFromEntity[canvasEntity];
          var vertexIndices = MeshVertexIndexFromEntity[canvasEntity];
          var subMeshes = SubMeshInfoFromEntity[canvasEntity];

          var currOffset = Offsets[index];
          for (var i = 0; i < vertexData.Length; ++i)
            vertices[i + currOffset.Vertex] = vertexData[i];
          for (var i = 0; i < vertexIndex.Length; ++i) {
            var value = vertexIndex[i].Value + currOffset.Vertex;
            vertexIndices[i + currOffset.Indices] = value;
          }
          if (currOffset.SubMeshIndex != -1)
            subMeshes[currOffset.SubMeshIndex] = new SubMeshInfo() {
              Offset = currOffset.SubMesh,
              VertexCount = currOffset.VertexCount,
              MaterialId = currOffset.SubMeshMaterialId
            };
        }
      }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {

      if (m_canvasdRootQuery.CalculateEntityCount() == 0)
        TextUtility.CreateCanvas(EntityManager);

      m_vertexCounter.Value = 0;
      m_vertexIndexCounter.Value = 0;
      m_subMeshCounter.Value = 0;
      var changedVerticesCount = m_vertexDataQuery.CalculateEntityCount();
      if (changedVerticesCount == 0)
        return inputDeps;
      m_vertexDataQuery.ResetFilter();

      var length = m_vertexDataQuery.CalculateEntityCount();
      var canvasRootEntities = m_canvasdRootQuery.ToEntityArray(Allocator.TempJob, out var toCanvasRootEntities);
      var entities = m_vertexDataQuery.ToEntityArray(Allocator.TempJob, out var toEntitiesHandle);
      var sortedIndices = m_vertexDataQuery.ToComponentDataArray<SortIndex>(Allocator.TempJob, out var toSortIndicesHandle);
      inputDeps = JobUtility.CombineDependencies(
        toCanvasRootEntities,
        toEntitiesHandle,
        toSortIndicesHandle,
        new ResizeNativeList<OffsetInfo> {
          Source = m_offsets,
          Length = length
        }.Schedule(inputDeps),
        new ResizeNativeList<int> {
          Source = m_sharedFontIndices,
          Length = length
        }.Schedule(inputDeps));

      inputDeps = new GatherSharedComponentIndices<FontMaterial> {
        ChunkSharedComponentType = GetArchetypeChunkSharedComponentType<FontMaterial>(),
        Indices = m_sharedFontIndices.AsDeferredJobArray()
      }.Schedule(m_vertexDataQuery, inputDeps);

      inputDeps = new CreateOffsets {
        Entities = entities,
        SortedIndices = sortedIndices,
        Offsets = m_offsets.AsDeferredJobArray(),
        SharedComponentIndices = m_sharedFontIndices.AsDeferredJobArray(),
        Vertices = GetBufferFromEntity<Vertex>(true),
        VertexIndices = GetBufferFromEntity<VertexIndex>(true),
        VertexCounter = m_vertexCounter,
        VertexIndexCounter = m_vertexIndexCounter,
        SubMeshCounter = m_subMeshCounter
      }.Schedule(inputDeps);

      inputDeps = JobHandle.CombineDependencies(
        new ResizeBufferDeferred<Vertex> {
          Length = m_vertexCounter
        }.Schedule(m_canvasdRootQuery, inputDeps),
        new ResizeBufferDeferred<VertexIndex> {
          Length = m_vertexIndexCounter
        }.Schedule(m_canvasdRootQuery, inputDeps),
        new ResizeBufferDeferred<SubMeshInfo> {
          Length = m_subMeshCounter
        }.Schedule(m_canvasdRootQuery, inputDeps));

      inputDeps = new MeshBatching {
        CanvasEntities = canvasRootEntities,
        MeshVertexFromEntity = GetBufferFromEntity<Vertex>(false),
        MeshVertexIndexFromEntity = GetBufferFromEntity<VertexIndex>(false),
        SubMeshInfoFromEntity = GetBufferFromEntity<SubMeshInfo>(false),
        Offsets = m_offsets.AsDeferredJobArray(),
      }.ScheduleSingle(m_vertexDataQuery, inputDeps);

      inputDeps = JobHandle.CombineDependencies(
        canvasRootEntities.Dispose(inputDeps),
        entities.Dispose(inputDeps),
        sortedIndices.Dispose(inputDeps));
      
      m_vertexDataQuery.SetFilterChanged(new ComponentType[] { ComponentType.ReadOnly<Vertex>(), ComponentType.ReadOnly<VertexIndex>() });
      return inputDeps;
    }
  }
}
