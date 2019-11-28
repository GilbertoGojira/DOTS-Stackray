using Stackray.Collections;
using Stackray.Entities;
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
    ComponentType[] m_filterChanged;

    NativeList<Vertex> m_vertices;
    NativeList<VertexIndex> m_triangles;
    NativeCounter m_vertexCounter;
    NativeCounter m_vertexIndexCounter;
    NativeCounter m_subMeshCounter;

    NativeList<OffsetInfo> m_offsets;
    NativeHashMap<Entity, int> m_entityIndexMap;
    NativeList<int> m_sharedFontIndices;

    protected override void OnCreate() {
      m_vertices = new NativeList<Vertex>(10000, Allocator.Persistent);
      m_triangles = new NativeList<VertexIndex>(10000, Allocator.Persistent);
      m_vertexCounter = new NativeCounter(Allocator.Persistent);
      m_vertexIndexCounter = new NativeCounter(Allocator.Persistent);
      m_subMeshCounter = new NativeCounter(Allocator.Persistent);
      m_offsets = new NativeList<OffsetInfo>(0, Allocator.Persistent);
      m_entityIndexMap = new NativeHashMap<Entity, int>(1000, Allocator.Persistent);
      m_sharedFontIndices = new NativeList<int>(0, Allocator.Persistent);

      m_canvasdRootQuery = GetEntityQuery(
        ComponentType.ReadOnly<CanvasRoot>(),
        ComponentType.ReadWrite<Vertex>(),
        ComponentType.ReadWrite<VertexIndex>(),
        ComponentType.ReadWrite<SubMeshInfo>());
      m_vertexDataQuery = GetEntityQuery(
        ComponentType.ReadOnly<FontMaterial>(),
        ComponentType.ReadOnly<TextRenderer>(),
        ComponentType.ReadOnly<Vertex>(),
        ComponentType.ReadOnly<VertexIndex>());
      m_filterChanged = new ComponentType[] {
        ComponentType.ReadOnly<Vertex>(),
        ComponentType.ReadOnly<VertexIndex>() };
    }

    protected override void OnDestroy() {
      m_vertices.Dispose();
      m_triangles.Dispose();
      m_vertexCounter.Dispose();
      m_vertexIndexCounter.Dispose();
      m_subMeshCounter.Dispose();
      m_offsets.Dispose();
      m_sharedFontIndices.Dispose();
      m_entityIndexMap.Dispose();
    }

    [BurstCompile]
    struct CreateOffsets : IJob {
      [ReadOnly]
      public NativeHashMap<Entity, int> EntitiesIndexMap;
      [ReadOnly]
      public NativeArray<SortedEntity> SortedEntities;
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
        var prevIndex = -1;
        for (var i = SortedEntities.Length - 1; i >= 0; --i) {
          var entity = SortedEntities[i].Value;
          if (!EntitiesIndexMap.TryGetValue(entity, out var index))
            continue;
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
          prevIndex = index;
        }
      }
    }

    [BurstCompile]
    private struct MeshBatching : IJobForEachWithEntity<TextRenderer> {
      public Entity CanvasRootEntity;
      [ReadOnly]
      public NativeArray<OffsetInfo> Offsets;
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
        var vertexIndexData = MeshVertexIndexFromEntity[entity];

        var canvasVertexData = MeshVertexFromEntity[CanvasRootEntity];
        var canvasVertexIndexData = MeshVertexIndexFromEntity[CanvasRootEntity];
        var canvasSubMeshData = SubMeshInfoFromEntity[CanvasRootEntity];

        var currOffset = Offsets[index];
        for (var i = 0; i < vertexData.Length; ++i)
          canvasVertexData[i + currOffset.Vertex] = vertexData[i];
        for (var i = 0; i < vertexIndexData.Length; ++i) {
          var value = vertexIndexData[i].Value + currOffset.Vertex;
          canvasVertexIndexData[i + currOffset.Indices] = value;
        }
        if (currOffset.SubMeshIndex != -1)
          canvasSubMeshData[currOffset.SubMeshIndex] = new SubMeshInfo() {
            Offset = currOffset.SubMesh,
            VertexCount = currOffset.VertexCount,
            MaterialId = currOffset.SubMeshMaterialId
          };
      }
    }
    
    [BurstCompile]
    struct ToEntityHashMap : IJobParallelFor {
      [ReadOnly]
      public NativeArray<Entity> Entities;
      [WriteOnly]
      public NativeHashMap<Entity, int>.ParallelWriter EntityIndexMap;
      public void Execute(int index) {
        EntityIndexMap.TryAdd(Entities[index], index);
      }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {

      if (!HasSingleton<CanvasRoot>()) {
        TextUtility.CreateCanvas(EntityManager);
        SetSingleton(default(CanvasRoot));
      }

      m_vertexCounter.Value = 0;
      m_vertexIndexCounter.Value = 0;
      m_subMeshCounter.Value = 0;
      var changedVerticesCount = m_vertexDataQuery.CalculateEntityCount();
      if (changedVerticesCount == 0)
        return inputDeps;
      m_vertexDataQuery.ResetFilter();

      var length = m_vertexDataQuery.CalculateEntityCount();
      var canvasRootEntity = GetSingletonEntity<CanvasRoot>();
      var sortedEntities = EntityManager.GetAllSortedEntities(this, Allocator.TempJob);
      inputDeps = m_vertexDataQuery.ToEntityIndexMap(EntityManager, ref m_entityIndexMap, inputDeps);

      inputDeps = JobHandle.CombineDependencies(
        m_offsets.Resize(length, inputDeps),
        m_sharedFontIndices.Resize(length, inputDeps));

      inputDeps = new GatherSharedComponentIndices<FontMaterial> {
        ChunkSharedComponentType = GetArchetypeChunkSharedComponentType<FontMaterial>(),
        Indices = m_sharedFontIndices.AsDeferredJobArray()
      }.Schedule(m_vertexDataQuery, inputDeps);

      inputDeps = new CreateOffsets {
        EntitiesIndexMap = m_entityIndexMap,
        SortedEntities = sortedEntities,
        Offsets = m_offsets.AsDeferredJobArray(),
        SharedComponentIndices = m_sharedFontIndices.AsDeferredJobArray(),
        Vertices = GetBufferFromEntity<Vertex>(true),
        VertexIndices = GetBufferFromEntity<VertexIndex>(true),
        VertexCounter = m_vertexCounter,
        VertexIndexCounter = m_vertexIndexCounter,
        SubMeshCounter = m_subMeshCounter
      }.Schedule(inputDeps);

      inputDeps = JobHandle.CombineDependencies(
        m_canvasdRootQuery.ResizeBufferDeferred<Vertex>(this, m_vertexCounter, inputDeps),
        m_canvasdRootQuery.ResizeBufferDeferred<VertexIndex>(this, m_vertexIndexCounter, inputDeps),
        m_canvasdRootQuery.ResizeBufferDeferred<SubMeshInfo>(this, m_subMeshCounter, inputDeps));

      inputDeps = new MeshBatching {
        CanvasRootEntity = canvasRootEntity,
        MeshVertexFromEntity = GetBufferFromEntity<Vertex>(false),
        MeshVertexIndexFromEntity = GetBufferFromEntity<VertexIndex>(false),
        SubMeshInfoFromEntity = GetBufferFromEntity<SubMeshInfo>(false),
        Offsets = m_offsets.AsDeferredJobArray(),
      }.Schedule(m_vertexDataQuery, inputDeps);

      inputDeps = sortedEntities.Dispose(inputDeps);
      m_vertexDataQuery.SetChangedVersionFilter(m_filterChanged);
      return inputDeps;
    }
  }
}
