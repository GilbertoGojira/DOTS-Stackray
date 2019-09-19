using System.Collections.Generic;
using Stackray.Collections;
using Stackray.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Stackray.Text {

  [UpdateAfter(typeof(TextMeshBuildSystem))]
  public class TextMeshBatchSystem : JobComponentSystem {

    struct OffsetInfo {
      public int Vertex;
      public int Triangle;
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

    NativeHashMap<Entity, int> m_entityToIndex;
    List<FontMaterial> m_fontMaterials = new List<FontMaterial>();
    List<int> m_fontMaterialIndices = new List<int>();
    int m_lastOrderInfo;

    protected override void OnCreate() {
      m_vertices = new NativeList<Vertex>(10000, Allocator.Persistent);
      m_triangles = new NativeList<VertexIndex>(10000, Allocator.Persistent);
      m_vertexCounter = new NativeCounter(Allocator.Persistent);
      m_vertexIndexCounter = new NativeCounter(Allocator.Persistent);
      m_entityToIndex = new NativeHashMap<Entity, int>(0, Allocator.Persistent);

      m_canvasdRootQuery = GetEntityQuery(
        ComponentType.ReadWrite<Vertex>(),
        ComponentType.ReadWrite<VertexIndex>(),
        ComponentType.ReadWrite<SubMeshInfo>());
      m_vertexDataQuery = GetEntityQuery(
        ComponentType.ReadOnly<FontMaterial>(),
        ComponentType.ReadOnly<TextRenderer>(),
        ComponentType.ReadOnly<Vertex>(),
        ComponentType.ReadOnly<VertexIndex>());
    }

    protected override void OnDestroy() {
      m_vertices.Dispose();
      m_triangles.Dispose();
      m_vertexCounter.Dispose();
      m_vertexIndexCounter.Dispose();
      m_entityToIndex.Dispose();
    }

    [BurstCompile]
    struct EntityToIndex : IJobChunk {
      [ReadOnly]
      public ArchetypeChunkEntityType EntityType;
      [WriteOnly]
      public NativeHashMap<Entity, int>.ParallelWriter GlobalIndices;
      public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
        var entities = chunk.GetNativeArray(EntityType);
        for (var i = 0; i < entities.Length; ++i)
          GlobalIndices.TryAdd(entities[i], firstEntityIndex + i);
      }
    }

    [BurstCompile]
    struct GatherVertexOffsets : IJobForEachWithEntity_EBB<Vertex, VertexIndex> {
      public int SubMeshIndex;
      public int FontMaterialIndex;
      [NativeDisableParallelForRestriction]
      public NativeArray<OffsetInfo> Offsets;
      public NativeCounter VertexCounter;
      public NativeCounter VertexIndexCounter;
      [ReadOnly]
      public NativeHashMap<Entity, int> EntityToIndex;

      public void Execute(
        Entity entity,
        int index,
        [ReadOnly] DynamicBuffer<Vertex> vertexData,
        [ReadOnly] DynamicBuffer<VertexIndex> vertexIndex) {

        var subMeshOffset = VertexIndexCounter.Value;
        var globalIndex = EntityToIndex[entity];
        Offsets[globalIndex] = new OffsetInfo {
          Vertex = VertexCounter.Value,
          Triangle = VertexIndexCounter.Value,
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
          var vertices = MeshVertexFromEntity[canvasEntity];
          var vertexIndices = MeshVertexIndexFromEntity[canvasEntity];
          var subMeshes = SubMeshInfoFromEntity[canvasEntity];

          var currOffset = Offsets[index];
          for (var i = 0; i < vertexData.Length; ++i)
            vertices[i + currOffset.Vertex] = vertexData[i];
          for (var i = 0; i < vertexIndex.Length; ++i) {
            var value = vertexIndex[i].Value + currOffset.Vertex;
            vertexIndices[i + currOffset.Triangle] = value;
          }
          if (currOffset.SubMeshIndex != -1)
            subMeshes[currOffset.SubMeshIndex] = new SubMeshInfo() {
              Offset = currOffset.SubMesh,
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
      var changedVerticesCount = m_vertexDataQuery.CalculateEntityCount();
      if (changedVerticesCount == 0)
        return inputDeps;

      if(m_lastOrderInfo != m_vertexDataQuery.GetCombinedComponentOrderVersion()) {
        m_lastOrderInfo = m_vertexDataQuery.GetCombinedComponentOrderVersion();
        m_fontMaterials.Clear();
        m_fontMaterialIndices.Clear();
        EntityManager.GetAllUniqueSharedComponentData(m_fontMaterials, m_fontMaterialIndices);
        m_fontMaterials.Remove(default);
        m_fontMaterialIndices.Remove(default);
      }

      m_vertexDataQuery.ResetFilter();
      var globalEntityCount = m_vertexDataQuery.CalculateEntityCount();
      inputDeps = new ClearNativeHashMap<Entity, int> {
        Capacity = globalEntityCount,
        Source = m_entityToIndex
      }.Schedule(inputDeps);

      inputDeps = new EntityToIndex {
        EntityType = GetArchetypeChunkEntityType(),
        GlobalIndices = m_entityToIndex.AsParallelWriter()
      }.Schedule(m_vertexDataQuery, inputDeps);

      var offsets = new NativeArray<OffsetInfo>(globalEntityCount, Allocator.TempJob);
      var subMeshcount = 0;
      for (var i = 0; i < m_fontMaterials.Count; ++i) {
        m_vertexDataQuery.SetFilter(m_fontMaterials[i]);
        inputDeps = new GatherVertexOffsets {
          EntityToIndex = m_entityToIndex,
          FontMaterialIndex = m_fontMaterialIndices[i],
          SubMeshIndex = subMeshcount,
          Offsets = offsets,
          VertexCounter = m_vertexCounter,
          VertexIndexCounter = m_vertexIndexCounter,
        }.ScheduleSingle(m_vertexDataQuery, inputDeps);
        var entityCount = m_vertexDataQuery.CalculateEntityCount();
        subMeshcount += entityCount > 0 ? 1 : 0;
      }

      m_vertexDataQuery.ResetFilter();
      inputDeps = JobHandle.CombineDependencies(
        new ResizeBuferDeferred<Vertex> {
          Length = m_vertexCounter
        }.Schedule(m_canvasdRootQuery, inputDeps),
        new ResizeBuferDeferred<VertexIndex> {
          Length = m_vertexIndexCounter
        }.Schedule(m_canvasdRootQuery, inputDeps),
        new ResizeBuffer<SubMeshInfo> {
          Length = subMeshcount
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
