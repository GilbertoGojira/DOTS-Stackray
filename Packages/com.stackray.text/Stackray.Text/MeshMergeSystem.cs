using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stackray.Text {
  [UpdateAfter(typeof(MeshBatchSystem))]
  class MeshMerge : ComponentSystem {
    EntityQuery m_canvasRootQuery;
    EntityQuery m_vertexDataQuery;
    VertexAttributeDescriptor[] m_meshDescriptors;

    protected override void OnCreate() {
      m_canvasRootQuery = GetEntityQuery(
        ComponentType.ReadOnly<Vertex>(),
        ComponentType.ReadOnly<VertexIndex>(),
        ComponentType.ReadOnly<SubMeshInfo>());

      m_vertexDataQuery = GetEntityQuery(
        ComponentType.ReadOnly<TextRenderer>(),
        ComponentType.ReadOnly<Vertex>(),
        ComponentType.ReadOnly<VertexIndex>());
      m_vertexDataQuery.SetFilterChanged(new ComponentType[] { ComponentType.ReadOnly<Vertex>(), ComponentType.ReadOnly<VertexIndex>() });

      m_meshDescriptors = new VertexAttributeDescriptor[] {
        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
        new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
        new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 0),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 0),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2, 0),
      };
    }

    protected override void OnUpdate() {
      if(m_vertexDataQuery.CalculateEntityCount() > 0)
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

    SubMeshDescriptor m_lastSubMeshDescriptor;

    private void BuildMesh(DynamicBuffer<Vertex> vertexArray, DynamicBuffer<VertexIndex> vertexIndexArray, DynamicBuffer<SubMeshInfo> subMeshArray, Mesh mesh) {
      mesh.SetVertexBufferParams(vertexArray.Length, m_meshDescriptors[0], m_meshDescriptors[1], m_meshDescriptors[2], m_meshDescriptors[3], m_meshDescriptors[4]);
      var vertexNativeArray = vertexArray.AsNativeArray();
      mesh.SetVertexBufferData(vertexNativeArray, 0, 0, vertexArray.Length, 0);
      mesh.SetIndexBufferParams(vertexIndexArray.Length, IndexFormat.UInt32);
      mesh.SetIndexBufferData(vertexIndexArray.AsNativeArray(), 0, 0, vertexIndexArray.Length);
      mesh.subMeshCount = subMeshArray.Length;
      for (int i = 0; i < subMeshArray.Length; i++) {
        var subMesh = subMeshArray[i];
        var descr = new SubMeshDescriptor() {
          baseVertex = 0,
          bounds = default,
          firstVertex = 0,
          indexCount = i < subMeshArray.Length - 1
                ? subMeshArray[i + 1].Offset - subMesh.Offset
                : vertexIndexArray.Length - subMesh.Offset,
          indexStart = subMesh.Offset,
          topology = MeshTopology.Triangles,
          vertexCount = vertexArray.Length
        };
        if (!m_lastSubMeshDescriptor.Equals(descr)) {
          m_lastSubMeshDescriptor = descr;
          mesh.SetSubMesh(i, descr);
        }
      }
      mesh.UploadMeshData(false);
    }
  }
}
