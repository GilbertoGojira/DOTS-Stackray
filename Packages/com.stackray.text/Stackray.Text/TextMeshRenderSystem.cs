using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Stackray.Text {

  [UpdateInGroup(typeof(PresentationSystemGroup))]
  class TextMeshRenderSystem : ComponentSystem {

    EntityQuery m_renderQuery;
    int m_lastOrderInfo;
    List<TextRenderMesh> m_textMeshes = new List<TextRenderMesh>();
    MaterialPropertyBlock m_TemporaryBlock = new MaterialPropertyBlock();

    protected override void OnCreate() {
      base.OnCreate();
      m_renderQuery = GetEntityQuery(
        ComponentType.ReadOnly<TextRenderMesh>(),
        ComponentType.ReadOnly<SubMeshInfo>());
    }

    protected override void OnUpdate() {
      if (m_lastOrderInfo != m_renderQuery.GetCombinedComponentOrderVersion()) {
        m_lastOrderInfo = m_renderQuery.GetCombinedComponentOrderVersion();
        m_textMeshes.Clear();
        EntityManager.GetAllUniqueSharedComponentData(m_textMeshes);
        m_textMeshes.Remove(default);
      }
      foreach (var textMesh in m_textMeshes) {
        var mesh = textMesh.Mesh;
        m_renderQuery.SetFilter(textMesh);
        using (var chunkArray = m_renderQuery.CreateArchetypeChunkArray(Allocator.TempJob)) {
          var subMeshBufferType = GetArchetypeChunkBufferType<SubMeshInfo>();
          foreach (var chunk in chunkArray) {
            var subMeshes = chunk.GetBufferAccessor(subMeshBufferType)[0];
            var submeshCount = math.min(subMeshes.Length, mesh.subMeshCount);
            for (var i = 0; i < submeshCount; ++i) {
              var subMesh = subMeshes[i];
              var material = GetMaterial(subMesh.MaterialId);
              m_TemporaryBlock.SetTexture("_MainTex", material.mainTexture);
              Graphics.DrawMesh(
                mesh: mesh, 
                matrix: float4x4.identity, 
                material: material,
                submeshIndex: i,
                layer: 0,
                properties: m_TemporaryBlock,
                camera: Camera.main);
            }
          }
        }
      }
    }

    Material GetMaterial(int id) {
      return EntityManager.GetSharedComponentData<FontMaterial>(id).Value;
    }
  }
}
