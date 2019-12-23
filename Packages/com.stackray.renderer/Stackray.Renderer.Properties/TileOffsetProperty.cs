using Stackray.Entities;
using Unity.Mathematics;

namespace Stackray.Renderer {

  public struct TileOffsetProperty : IDynamicBufferProperty<half4> {
    // using half for faster GetHashCode
    public half4 Value;

    public string BufferName => "tileOffsetBuffer";

    half4 IComponentValue<half4>.Value { get => Value; set => Value = value; }

    public bool Equals(half4 other) {
      return Value.Equals(other);
    }

    public half4 Convert(UnityEngine.SpriteRenderer spriteRenderer) {
      var uv = spriteRenderer.sprite.uv;
      var offset = new float2(uv[2].x, uv[2].y);
      var tile = new float2(uv[1].x, uv[1].y) - offset;
      return (half4)new float4(tile, offset);
    }

    public override int GetHashCode() => Value.GetHashCode();
  }
}