using Stackray.SpriteRenderer;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stackray.Text {
  [UpdateInGroup(typeof(PresentationSystemGroup))]
  [UpdateBefore(typeof(SpriteRendererSystem))]
  class MeshMerge : ComponentSystem {
    EntityQuery m_canvasRootQuery;
    Material m_DefaultMaterial;
    VertexAttributeDescriptor[] m_meshDescriptors;
    int m_lastOrderVersion;

    protected override void OnCreate() {
      m_canvasRootQuery = GetEntityQuery(
        ComponentType.ReadOnly<BatchVertex>(),
        ComponentType.ReadOnly<BatchVertexIndex>(),
        ComponentType.ReadOnly<SubMeshInfo>());

      m_meshDescriptors = new VertexAttributeDescriptor[] {
        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
        new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
        new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 0),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 0),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2, 0),
      };
    }

    protected override void OnUpdate() {
      //if (m_lastOrderVersion == m_canvasRootQuery.GetCombinedComponentOrderVersion())
      //  return;
      m_lastOrderVersion = m_canvasRootQuery.GetCombinedComponentOrderVersion();
      RebuildMesh();
    }

    private void RebuildMesh() {
      using (var chunkArray = m_canvasRootQuery.CreateArchetypeChunkArray(Allocator.TempJob)) {
        var meshType = GetArchetypeChunkSharedComponentType<SpriteRenderMesh>();

        var vertexBufferType = GetArchetypeChunkBufferType<BatchVertex>();
        var vertexIndexBufferType = GetArchetypeChunkBufferType<BatchVertexIndex>();
        var subMeshBufferType = GetArchetypeChunkBufferType<SubMeshInfo>();

        var entityType = GetArchetypeChunkEntityType();


        for (int i = 0; i < chunkArray.Length; i++) {
          var chunk = chunkArray[i];

          if (chunk.Count > 1) {
            Debug.LogError($"One archetype can contain only one canvas.");
            continue;
          }

          var entity = chunk.GetNativeArray(entityType)[0];
          var renderer = chunk.GetSharedComponentData(meshType, EntityManager);
          var vertices = chunk.GetBufferAccessor(vertexBufferType)[0];
          var indices = chunk.GetBufferAccessor(vertexIndexBufferType)[0];
          var subMeshes = chunk.GetBufferAccessor(subMeshBufferType)[0];

          BuildMesh(vertices, indices, subMeshes, renderer.Mesh);
        }
      }
    }

    SubMeshDescriptor m_lastSubMeshDescriptor;

    private void BuildMesh(DynamicBuffer<BatchVertex> vertexArray, DynamicBuffer<BatchVertexIndex> vertexIndexArray, DynamicBuffer<SubMeshInfo> subMeshArray, Mesh mesh) {
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
