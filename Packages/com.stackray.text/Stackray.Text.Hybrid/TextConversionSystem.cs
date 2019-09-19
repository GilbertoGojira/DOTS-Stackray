using Stackray.Mathematics;
using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace Stackray.Text {
  [UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
  public class TextConversionSystem : GameObjectConversionSystem {

    static Dictionary<TMP_FontAsset, Entity> m_textFontAssets = new Dictionary<TMP_FontAsset, Entity>(); 

    protected override void OnUpdate() {
      Entities.ForEach((TextMeshPro textMesh) => {
        var font = textMesh.font;
        var entity = GetPrimaryEntity(textMesh);
        if (!m_textFontAssets.TryGetValue(font, out var fontEntity)) {
          fontEntity = TextUtility.CreateTextFontAsset(DstEntityManager, font);
          m_textFontAssets.Add(font, fontEntity);
        }

        DstEntityManager.AddSharedComponentData(entity, new FontMaterial {
          Value = font.material
        });
        var materialId = DstEntityManager.GetSharedComponentDataIndex<FontMaterial>(entity);

        DstEntityManager.AddComponentData(entity, new TextRenderer() {
          Font = fontEntity,
          MaterialId = materialId,
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
        if(!DstEntityManager.HasComponent<RenderBounds>(entity))
          // RenderBounds will be calculated on TextMeshBuildSystem
          DstEntityManager.AddComponentData(entity, default(RenderBounds));
      });
    }
  }
}
