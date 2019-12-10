using Stackray.Entities.Editor;
using Unity.Mathematics;
using UnityEditor;

namespace Stackray.Renderer {
  public class TileOffsetSpriteAnimationClipEditor : BufferElementDataEditor<SpriteAnimationClipBufferElement<TileOffsetProperty, half4>> {
    public override void OnInspectorGUI(SpriteAnimationClipBufferElement<TileOffsetProperty, half4> target, string index) {
      base.OnInspectorGUI(target, $"{nameof(TileOffsetProperty)}-{target.ClipName.ToString()}-{index}");
      if (!target.Value.IsCreated)
        return;
      EditorGUI.indentLevel++;
      ref var clipSet = ref target.Value.Value;
      for (var frame = 0; frame < clipSet.Value.Length; ++frame)
        EditorGUILayout.Vector4Field($"Frame[{frame}]", (float4)clipSet.Value[frame].Value);
      EditorGUI.indentLevel--;
    }
  }

  public class ScaleAnimationClipBufferEditor : BufferElementDataEditor<SpriteAnimationClipBufferElement<ScaleProperty, half4>> {
    public override void OnInspectorGUI(SpriteAnimationClipBufferElement<ScaleProperty, half4> target, string index) {
      base.OnInspectorGUI(target, $"{nameof(ScaleProperty)}-{target.ClipName.ToString()}-{index}");
      if (!target.Value.IsCreated)
        return;
      EditorGUI.indentLevel++;
      ref var clipSet = ref target.Value.Value;
      for (var frame = 0; frame < clipSet.Value.Length; ++frame)
        EditorGUILayout.Vector4Field($"Frame[{frame}]", (float4)clipSet.Value[frame].Value);
      EditorGUI.indentLevel--;
    }
  }

  public class PivotAnimationClipBufferEditor : BufferElementDataEditor<SpriteAnimationClipBufferElement<PivotProperty, half2>> {
    public override void OnInspectorGUI(SpriteAnimationClipBufferElement<PivotProperty, half2> target, string index) {
      base.OnInspectorGUI(target, $"{nameof(PivotProperty)}-{target.ClipName.ToString()}-{index}");
      if (!target.Value.IsCreated)
        return;
      EditorGUI.indentLevel++;
      ref var clipSet = ref target.Value.Value;
      for (var frame = 0; frame < clipSet.Value.Length; ++frame)
        EditorGUILayout.Vector2Field($"Frame[{frame}]", (float2)clipSet.Value[frame].Value);
      EditorGUI.indentLevel--;
    }
  }

    public class ColorAnimationClipBufferEditor : BufferElementDataEditor<SpriteAnimationClipBufferElement<ColorProperty, half4>> {
    public override void OnInspectorGUI(SpriteAnimationClipBufferElement<ColorProperty, half4> target, string index) {
      base.OnInspectorGUI(target, $"{nameof(ColorProperty)}-{target.ClipName.ToString()}-{index}");
      if (!target.Value.IsCreated)
        return;
      EditorGUI.indentLevel++;
      ref var clipSet = ref target.Value.Value;
      for (var frame = 0; frame < clipSet.Value.Length; ++frame)
        EditorGUILayout.Vector4Field($"Frame[{frame}]", (float4)clipSet.Value[frame].Value);
      EditorGUI.indentLevel--;
    }
  }
}
