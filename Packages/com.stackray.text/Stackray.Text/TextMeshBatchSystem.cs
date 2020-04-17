using Stackray.Collections;
using Stackray.Entities;
using Stackray.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Stackray.Text {

  [UpdateAfter(typeof(TextMeshBuildSystem))]
  public class TextMeshBatchSystem : SystemBase {

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

    void CreateOffsets(
      NativeHashMap<Entity, int> entitiesIndexMap, 
      NativeArray<SortedEntity> sortedEntities,
      NativeArray<int> sharedComponentIndices,
      NativeArray<OffsetInfo> offsets,
      NativeCounter vertexCounter,
      NativeCounter vertexIndexCounter,
      NativeCounter subMeshCounter) {


      var vertices = GetBufferFromEntity<Vertex>(true);
      var vertexIndices = GetBufferFromEntity<VertexIndex>(true);
      
      Job
        .WithReadOnly(entitiesIndexMap)
        .WithReadOnly(sortedEntities)
        .WithReadOnly(sharedComponentIndices)
        .WithReadOnly(vertices)
        .WithReadOnly(vertexIndices)
        .WithCode(() => {
          var prevIndex = -1;
          for (var i = sortedEntities.Length - 1; i >= 0; --i) {
            var entity = sortedEntities[i].Value;
            if (!entitiesIndexMap.TryGetValue(entity, out var index))
              continue;
            var vertexData = vertices[entity];
            var vertexIndexData = vertexIndices[entity];
            var sharedComponentIndex = sharedComponentIndices[index];
            var prevSharedComponentIndex = prevIndex >= 0 ? sharedComponentIndices[prevIndex] : -1;
            offsets[index] = new OffsetInfo {
              Vertex = vertexCounter.Value,
              VertexCount = vertexData.Length,
              Indices = vertexIndexCounter.Value,
              SubMeshIndex = sharedComponentIndex != prevSharedComponentIndex ? subMeshCounter.Value : -1,
              SubMesh = sharedComponentIndex != prevSharedComponentIndex ? vertexIndexCounter.Value : -1,
              SubMeshMaterialId = sharedComponentIndex != prevSharedComponentIndex ? sharedComponentIndex : -1
            };
            if (sharedComponentIndex != prevSharedComponentIndex)
              subMeshCounter.Increment(1);
            vertexCounter.Increment(vertexData.Length);
            vertexIndexCounter.Increment(vertexIndexData.Length);
            prevIndex = index;
          }
        }).Schedule();
    }

    void MergeBatching(
      Entity canvasRootEntity,
      NativeArray<OffsetInfo> offsets) {

      var meshVertexFromEntity = GetBufferFromEntity<Vertex>(false);
      var meshVertexIndexFromEntity = GetBufferFromEntity<VertexIndex>(false);
      var subMeshInfoFromEntity = GetBufferFromEntity<SubMeshInfo>(false);
      Entities
      .WithAll<FontMaterial, Vertex, VertexIndex>()
      .WithStoreEntityQueryInField(ref m_vertexDataQuery)
      .WithReadOnly(offsets)
      .WithNativeDisableParallelForRestriction(meshVertexFromEntity)
      .WithNativeDisableParallelForRestriction(meshVertexIndexFromEntity)
      .WithNativeDisableParallelForRestriction(subMeshInfoFromEntity)
      .ForEach((Entity entity, int entityInQueryIndex, in TextRenderer textRenderer) => {
        var vertexData = meshVertexFromEntity[entity];
        var vertexIndexData = meshVertexIndexFromEntity[entity];

        var canvasVertexData = meshVertexFromEntity[canvasRootEntity];
        var canvasVertexIndexData = meshVertexIndexFromEntity[canvasRootEntity];
        var canvasSubMeshData = subMeshInfoFromEntity[canvasRootEntity];

        var currOffset = offsets[entityInQueryIndex];
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
      }).ScheduleParallel();
    }

    protected override void OnUpdate() {

      if (!HasSingleton<CanvasRoot>()) {
        TextUtility.CreateCanvas(EntityManager);
        SetSingleton(default(CanvasRoot));
      }

      m_vertexCounter.Value = 0;
      m_vertexIndexCounter.Value = 0;
      m_subMeshCounter.Value = 0;
      var changedVerticesCount = m_vertexDataQuery.CalculateEntityCount();
      if (changedVerticesCount == 0)
        return;
      m_vertexDataQuery.ResetFilter();

      var length = m_vertexDataQuery.CalculateEntityCount();
      var canvasRootEntity = GetSingletonEntity<CanvasRoot>();
      var sortedEntities = EntityManager.GetAllSortedEntities(this, Allocator.TempJob);
      Dependency = m_vertexDataQuery.ToEntityIndexMap(EntityManager, ref m_entityIndexMap, Dependency);

      Dependency = JobHandle.CombineDependencies(
        m_offsets.Resize(length, Dependency),
        m_sharedFontIndices.Resize(length, Dependency));

      Dependency = new GatherSharedComponentIndices<FontMaterial> {
        ChunkSharedComponentType = GetArchetypeChunkSharedComponentType<FontMaterial>(),
        Indices = m_sharedFontIndices.AsDeferredJobArray()
      }.Schedule(m_vertexDataQuery, Dependency);

      CreateOffsets(
        m_entityIndexMap,
        sortedEntities,
        m_sharedFontIndices.AsDeferredJobArray(),
        m_offsets.AsDeferredJobArray(),
        m_vertexCounter,
        m_vertexIndexCounter,
        m_subMeshCounter);

      Dependency = JobHandle.CombineDependencies(
        m_canvasdRootQuery.ResizeBufferDeferred<Vertex>(this, m_vertexCounter, Dependency),
        m_canvasdRootQuery.ResizeBufferDeferred<VertexIndex>(this, m_vertexIndexCounter, Dependency),
        m_canvasdRootQuery.ResizeBufferDeferred<SubMeshInfo>(this, m_subMeshCounter, Dependency));

      MergeBatching(canvasRootEntity, m_offsets.AsDeferredJobArray());
      Dependency = sortedEntities.Dispose(Dependency);
      m_vertexDataQuery.SetChangedVersionFilter(m_filterChanged);
    }
  }
}
