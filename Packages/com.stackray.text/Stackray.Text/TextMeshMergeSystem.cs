using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Stackray.Text {
  [UpdateAfter(typeof(TextMeshBatchSystem))]
  class TextMeshMergeSystem : ComponentSystem {
    EntityQuery m_canvasRootQuery;
    EntityQuery m_vertexDataQuery;
    SubMeshDescriptor m_lastSubMeshDescriptor;
    VertexAttributeDescriptor[] m_meshDescriptors;
    int m_cachedVertexCount;

    protected override void OnCreate() {
      m_canvasRootQuery = GetEntityQuery(
        ComponentType.ReadOnly<Vertex>(),
        ComponentType.ReadOnly<VertexIndex>(),
        ComponentType.ReadOnly<SubMeshInfo>());

      m_vertexDataQuery = GetEntityQuery(
        ComponentType.ReadOnly<TextRenderer>(),
        ComponentType.ReadOnly<Vertex>(),
        ComponentType.ReadOnly<VertexIndex>());
      m_vertexDataQuery.SetChangedVersionFilter(new ComponentType[] { ComponentType.ReadOnly<Vertex>(), ComponentType.ReadOnly<VertexIndex>() });

      m_meshDescriptors = new VertexAttributeDescriptor[] {
        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
        new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float16, 4, 0),
        new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float16, 4, 0),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2, 0),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float16, 2, 0),
      };
    }

    protected override void OnUpdate() {
      if (m_vertexDataQuery.CalculateEntityCount() > 0)
        RebuildMesh();
    }

    private void RebuildMesh() {
      using (var chunkArray = m_canvasRootQuery.CreateArchetypeChunkArray(Allocator.TempJob)) {
        var meshType = GetArchetypeChunkSharedComponentType<TextRenderMesh>();

        var vertexBufferType = GetArchetypeChunkBufferType<Vertex>();
        var vertexIndexBufferType = GetArchetypeChunkBufferType<VertexIndex>();
        var subMeshBufferType = GetArchetypeChunkBufferType<SubMeshInfo>();

        for (int i = 0; i < chunkArray.Length; i++) {
          var chunk = chunkArray[i];
          if (chunk.Count > 1) {
            Debug.LogError($"One archetype can contain only one canvas.");
            continue;
          }
          var renderer = chunk.GetSharedComponentData(meshType, EntityManager);
          var vertices = chunk.GetBufferAccessor(vertexBufferType)[0];
          var indices = chunk.GetBufferAccessor(vertexIndexBufferType)[0];
          var subMeshes = chunk.GetBufferAccessor(subMeshBufferType)[0];

          BuildMesh(vertices, indices, subMeshes, renderer.Mesh);
        }
      }
    }

    private void BuildMesh(DynamicBuffer<Vertex> vertexArray, DynamicBuffer<VertexIndex> vertexIndexArray, DynamicBuffer<SubMeshInfo> subMeshArray, Mesh mesh) {
      var vertexCount = vertexArray.Length;
      Profiler.BeginSample("SetVertexBufferParams");
      if (m_cachedVertexCount != vertexCount) {
        m_cachedVertexCount = vertexCount;
        mesh.SetVertexBufferParams(
          vertexCount,
          m_meshDescriptors[0],
          m_meshDescriptors[1],
          m_meshDescriptors[2],
          m_meshDescriptors[3],
          m_meshDescriptors[4]);
      }
      Profiler.EndSample();
      Profiler.BeginSample("SetVertexBufferData");
      mesh.SetVertexBufferData(vertexArray.AsNativeArray(), 0, 0, vertexCount, 0);
      Profiler.EndSample();
      Profiler.BeginSample("SetIndexBufferData");
      mesh.SetIndexBufferParams(vertexIndexArray.Length, IndexFormat.UInt32);
      mesh.SetIndexBufferData(vertexIndexArray.AsNativeArray(), 0, 0, vertexIndexArray.Length);
      Profiler.EndSample();
      mesh.subMeshCount = subMeshArray.Length;
      for (int i = 0; i < subMeshArray.Length; i++) {
        var subMesh = subMeshArray[i];
        var descr = new SubMeshDescriptor() {
          baseVertex = 0,
          bounds = default,
          indexCount = i < subMeshArray.Length - 1
                ? subMeshArray[i + 1].Offset - subMesh.Offset
                : vertexIndexArray.Length - subMesh.Offset,
          indexStart = subMesh.Offset,
          topology = MeshTopology.Triangles
        };
        Profiler.BeginSample("Set SubMesh");
        if (!CompareSubMeshDescriptor(m_lastSubMeshDescriptor, descr)) {
          m_lastSubMeshDescriptor = descr;
          mesh.SetSubMesh(i, descr);
        }
        Profiler.EndSample();
      }

      Profiler.BeginSample("Mesh RecalculateBounds");
      mesh.RecalculateBounds();
      Profiler.EndSample();
      mesh.UploadMeshData(false);
    }

    static bool CompareSubMeshDescriptor(SubMeshDescriptor thisDescriptor, SubMeshDescriptor otherDescriptor) {
      return
        thisDescriptor.baseVertex == otherDescriptor.baseVertex &&
        thisDescriptor.bounds == otherDescriptor.bounds &&
        thisDescriptor.firstVertex == otherDescriptor.firstVertex &&
        thisDescriptor.indexCount == otherDescriptor.indexCount &&
        thisDescriptor.indexStart == otherDescriptor.indexStart &&
        thisDescriptor.topology == otherDescriptor.topology &&
        thisDescriptor.vertexCount == otherDescriptor.vertexCount;
    }
  }
}
