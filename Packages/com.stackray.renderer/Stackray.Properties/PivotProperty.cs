using Stackray.Entities;
using Unity.Mathematics;

namespace Stackray.Renderer {

  public struct PivotProperty : IDynamicBufferProperty<half2> {
    // using half for faster GetHashCode
    public half2 Value;

    public string BufferName => "pivotBuffer";

    half2 IComponentValue<half2>.Value { get => Value; set => Value = value; }

    public bool Equals(half2 other) {
      return Value.Equals(other);
    }

    public half2 Convert(UnityEngine.SpriteRenderer spriteRenderer) {
      var sprite = spriteRenderer.sprite;
      return (half2)new float2(-sprite.pivot.x / sprite.rect.width, -sprite.pivot.y / sprite.rect.height);
    }

    public override int GetHashCode() => Value.GetHashCode();
  }
}