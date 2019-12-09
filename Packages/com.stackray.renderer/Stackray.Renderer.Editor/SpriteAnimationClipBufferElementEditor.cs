using Stackray.Entities.Editor;
using Unity.Mathematics;

namespace Stackray.Renderer {
  public class SpriteAnimationClipBufferElementEditor1 : BufferElementDataEditor<SpriteAnimationClipBufferElement<TileOffsetProperty, half4>> {
    public override void OnInspectorGUI(SpriteAnimationClipBufferElement<TileOffsetProperty, half4> target, string index) {
      base.OnInspectorGUI(target, $"{nameof(TileOffsetProperty)}-{target.ClipName.ToString()}-{index}");

    }
  }

  public class SpriteAnimationClipBufferElementEditor2 : BufferElementDataEditor<SpriteAnimationClipBufferElement<ScaleProperty, half4>> {
    public override void OnInspectorGUI(SpriteAnimationClipBufferElement<ScaleProperty, half4> target, string index) {
      base.OnInspectorGUI(target, $"{nameof(ScaleProperty)}-{target.ClipName.ToString()}-{index}");

    }
  }

  public class SpriteAnimationClipBufferElementEditor3 : BufferElementDataEditor<SpriteAnimationClipBufferElement<PivotProperty, half2>> {
    public override void OnInspectorGUI(SpriteAnimationClipBufferElement<PivotProperty, half2> target, string index) {
      base.OnInspectorGUI(target, $"{nameof(PivotProperty)}-{target.ClipName.ToString()}-{index}");

    }
  }

    public class SpriteAnimationClipBufferElementEditor4 : BufferElementDataEditor<SpriteAnimationClipBufferElement<ColorProperty, half4>> {
    public override void OnInspectorGUI(SpriteAnimationClipBufferElement<ColorProperty, half4> target, string index) {
      base.OnInspectorGUI(target, $"{nameof(ColorProperty)}-{target.ClipName.ToString()}-{index}");

    }
  }
}
