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

    EntityQuery m_vertexDataQuery;
    EntityQuery m_canvasdRootQuery;
    NativeList<int> m_vertexOffset;
    NativeList<int> m_vertexIndexOffset;
    NativeCounter m_vertexCounter;
    NativeCounter m_vertexIndexCounter;

    int m_lastOrderInfo;
    List<FontMaterial> m_fontMaterials = new List<FontMaterial>();
    List<int> m_fontMaterialsIDs = new List<int>();

    protected override void OnCreate() {
      base.OnCreate();
      m_vertexDataQuery = GetEntityQuery(
        ComponentType.ReadOnly<FontMaterial>(),
        ComponentType.ReadOnly<TextRenderer>(),
        ComponentType.ReadOnly<Vertex>());
      m_canvasdRootQuery = GetEntityQuery(
        ComponentType.ReadWrite<Vertex>(),
        ComponentType.ReadWrite<VertexIndex>(),
        ComponentType.ReadWrite<SubMeshInfo>());
      m_vertexOffset = new NativeList<int>(Allocator.Persistent);
      m_vertexIndexOffset = new NativeList<int>(Allocator.Persistent);
      m_vertexCounter = new NativeCounter(Allocator.Persistent);
      m_vertexIndexCounter = new NativeCounter(Allocator.Persistent);
    }

    protected override void OnDestroy() {
      base.OnDestroy();
      m_vertexOffset.Dispose();
      m_vertexIndexOffset.Dispose();
      m_vertexCounter.Dispose();
      m_vertexIndexCounter.Dispose();
    }

    [BurstCompile]
    struct AddSubmesh : IJobForEach_B<SubMeshInfo> {
      public int MaterialId;
      [ReadOnly]
      public NativeCounter VertexCounter;
      public void Execute(DynamicBuffer<SubMeshInfo> b0) {
        b0.Add(new SubMeshInfo {
          MaterialId = MaterialId,
          Offset = VertexCounter.Value / 4 * 6
        });
      }
    }

    [BurstCompile]
    struct CalcOffset : IJobForEachWithEntity_EB<Vertex> {
      [WriteOnly]
      [NativeDisableParallelForRestriction]
      public NativeList<int> VertexOffset;
      [WriteOnly]
      [NativeDisableParallelForRestriction]
      public NativeList<int> VertexIndexOffset;
      public NativeCounter VertexCounter;
      public NativeCounter VertexIndexCounter;
      public void Execute(Entity entity, int index, [ReadOnly] DynamicBuffer<Vertex> vertexBuffer) {
        if (index == 0) {
          VertexOffset[0] = 0;
          VertexIndexOffset[0] = 0;
        }
        var vertexCount = vertexBuffer.Length;
        var vertexIndexCount = vertexCount / 4 * 6;
        VertexCounter.Increment(vertexCount);
        VertexIndexCounter.Increment(vertexIndexCount);
        VertexOffset[index + 1] = VertexCounter.Value;
        VertexIndexOffset[index + 1] = VertexIndexCounter.Value;
      }
    }

    [BurstCompile]
    struct BatchVertices : IJobForEachWithEntity<TextRenderer> {
      [ReadOnly]
      public NativeList<int> VertexOffset;
      [ReadOnly]
      public NativeList<int> VertexIndexOffset;

      [DeallocateOnJobCompletion]
      [ReadOnly]
      public NativeArray<Entity> CanvasEntities;
      [NativeDisableParallelForRestriction]
      public BufferFromEntity<Vertex> VertexFromEntity;
      [NativeDisableParallelForRestriction]
      public BufferFromEntity<VertexIndex> VertexIndexFromEntity;

      public void Execute(Entity entity, int index, [ReadOnly]ref TextRenderer _) {
        var vertexBuffer = VertexFromEntity[entity];
        var canvasEntity = CanvasEntities[0];
        var canvasVertices = VertexFromEntity[canvasEntity];
        var canvasVertexIndices = VertexIndexFromEntity[canvasEntity];
        var startVertex = VertexOffset[index];
        var startVertexIndex = VertexIndexOffset[index];
        var vertex = startVertex;
        for (var i = 0; i < vertexBuffer.Length; ++i) {
          canvasVertices[startVertex + i] = vertexBuffer[i];
          if ((i + 1) % 4 == 0) {
            canvasVertexIndices[startVertexIndex] = new VertexIndex() { Value = vertex + 2 };
            canvasVertexIndices[startVertexIndex + 1] = new VertexIndex() { Value = vertex + 1 };
            canvasVertexIndices[startVertexIndex + 2] = new VertexIndex() { Value = vertex };

            canvasVertexIndices[startVertexIndex + 3] = new VertexIndex() { Value = vertex + 3 };
            canvasVertexIndices[startVertexIndex + 4] = new VertexIndex() { Value = vertex + 2 };
            canvasVertexIndices[startVertexIndex + 5] = new VertexIndex() { Value = vertex };
            vertex += 4;
            startVertexIndex += 6;
          }
        }
      }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {
      if (m_canvasdRootQuery.CalculateEntityCount() == 0)
        TextUtility.CreateCanvas(EntityManager);

      var changedVerticesCount = m_vertexDataQuery.CalculateEntityCount();
      if (changedVerticesCount == 0)
        return inputDeps;

      var orderInfo = m_vertexDataQuery.GetCombinedComponentOrderVersion();
      if (m_lastOrderInfo != orderInfo) {
        m_lastOrderInfo = orderInfo;
        m_fontMaterials.Clear();
        m_fontMaterialsIDs.Clear();
        EntityManager.GetAllUniqueSharedComponentData(m_fontMaterials, m_fontMaterialsIDs);
        m_fontMaterials.Remove(default);
        m_fontMaterialsIDs.Remove(default);
      }

      inputDeps = JobHandle.CombineDependencies(
        new ResizeBuffer<SubMeshInfo> {
          Length = 0
        }.Schedule(m_canvasdRootQuery, inputDeps),
        new MemsetCounter {
          Counter = m_vertexCounter,
          Value = 0
        }.Schedule(inputDeps));

      for (var i = 0; i < m_fontMaterials.Count; ++i) {
        var fontMaterial = m_fontMaterials[i];
        var materialId = m_fontMaterialsIDs[i];
        m_vertexDataQuery.SetFilter(fontMaterial);

        inputDeps = new AddSubmesh {
          MaterialId = materialId,
          VertexCounter = m_vertexCounter
        }.ScheduleSingle(m_canvasdRootQuery, inputDeps);

        inputDeps = new MemsetCounter {
          Counter = m_vertexCounter,
          Value = 0
        }.Schedule(inputDeps);

        inputDeps = new CountBufferElements<Vertex> {
          Counter = m_vertexCounter
        }.Schedule(m_vertexDataQuery, inputDeps);
      }
      m_vertexDataQuery.ResetFilter();

      inputDeps = JobHandle.CombineDependencies(
        new ResizeNativeList<int> {
          Source = m_vertexOffset,
          Length = m_vertexDataQuery.CalculateEntityCount() + 1
        }.Schedule(inputDeps),
        new ResizeNativeList<int> {
          Source = m_vertexIndexOffset,
          Length = m_vertexDataQuery.CalculateEntityCount() + 1
        }.Schedule(inputDeps));

      inputDeps = JobHandle.CombineDependencies(
        new MemsetCounter {
          Counter = m_vertexCounter,
          Value = 0
        }.Schedule(inputDeps),
        new MemsetCounter {
          Counter = m_vertexIndexCounter,
          Value = 0
        }.Schedule(inputDeps));

      inputDeps = new CalcOffset {
        VertexOffset = m_vertexOffset,
        VertexIndexOffset = m_vertexIndexOffset,
        VertexCounter = m_vertexCounter,
        VertexIndexCounter = m_vertexIndexCounter
      }.ScheduleSingle(m_vertexDataQuery, inputDeps);

      inputDeps = new ResizeBuferDeferred<Vertex> {
        Length = m_vertexCounter
      }.Schedule(m_canvasdRootQuery, inputDeps);
      inputDeps = new ResizeBuferDeferred<VertexIndex> {
        Length = m_vertexIndexCounter
      }.Schedule(m_canvasdRootQuery, inputDeps);

      inputDeps = new BatchVertices {
        VertexOffset = m_vertexOffset,
        VertexIndexOffset = m_vertexIndexOffset,
        CanvasEntities = m_canvasdRootQuery.ToEntityArray(Allocator.TempJob),
        VertexFromEntity = GetBufferFromEntity<Vertex>(false),
        VertexIndexFromEntity = GetBufferFromEntity<VertexIndex>(false)
      }.Schedule(m_vertexDataQuery, inputDeps);

      m_vertexDataQuery.SetFilterChanged(new ComponentType[] { ComponentType.ReadOnly<Vertex>() });

      return inputDeps;
    }
  }
}
