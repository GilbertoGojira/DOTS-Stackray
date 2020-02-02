using UnityEngine;

namespace Stackray.Sprite {
  struct SpriteRendererCache {
    public UnityEngine.Sprite Sprite;
    public Color Color;

    public SpriteRendererCache(UnityEngine.SpriteRenderer renderer) {
      Sprite = renderer.sprite;
      Color = renderer.color;
    }

    public void Restore(UnityEngine.SpriteRenderer renderer) {
      renderer.sprite = Sprite;
      renderer.color = Color;
    }
  }
}