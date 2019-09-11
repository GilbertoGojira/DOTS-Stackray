using Stackray.Mathematics;
using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace Stackray.Text {
  [UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
  public class TextConversionSystem : GameObjectConversionSystem {

    static Dictionary<TMP_FontAsset, (Entity, int)> m_textFontAssets = new Dictionary<TMP_FontAsset, (Entity, int)>();
    static Entity m_canvasEntity;   

    protected override void OnUpdate() {
      Entities.ForEach((TextMeshPro textMesh) => {
        var font = textMesh.font;
        var entity = GetPrimaryEntity(textMesh);
        if (!m_textFontAssets.TryGetValue(font, out var fontEntityId)) {
          fontEntityId = TextUtility.CreateTextFontAsset(DstEntityManager, font);
          m_textFontAssets.Add(font, fontEntityId);
        }
        if (m_canvasEntity == Entity.Null)
          m_canvasEntity = TextUtility.CreateCanvas(DstEntityManager);

        DstEntityManager.AddComponentData(entity, new TextRenderer() {
          CanvasEntity = m_canvasEntity,
          Font = fontEntityId.Item1,
          MaterialId = fontEntityId.Item2,
          Size = textMesh.fontSize,
          Alignment = textMesh.alignment,
          Bold = (textMesh.fontStyle & FontStyles.Bold) == FontStyles.Bold,
          Italic = (textMesh.fontStyle & FontStyles.Italic) == FontStyles.Italic
        });
        DstEntityManager.AddComponentData(entity, new TextData {
          Value = new NativeString64(textMesh.text)
        });
        DstEntityManager.AddComponentData(entity, new VertexColor() {
          Value = textMesh.color.ToFloat4()
        });
        DstEntityManager.AddComponentData(entity, new VertexColorMultiplier() {
          Value = new float4(1.0f, 1.0f, 1.0f, 1.0f)
        });
        DstEntityManager.AddBuffer<Vertex>(entity);
        DstEntityManager.AddBuffer<VertexIndex>(entity);
        if(!DstEntityManager.HasComponent<RenderBounds>(entity))
          // RenderBounds will be calculated on TextMeshBuildSystem
          DstEntityManager.AddComponentData(entity, default(RenderBounds));
      });
    }
  }
}
