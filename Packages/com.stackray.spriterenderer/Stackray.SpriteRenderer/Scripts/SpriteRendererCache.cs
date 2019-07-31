using UnityEngine;

namespace Stackray.SpriteRenderer {
  struct SpriteRendererCache {
    public Sprite Sprite;
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