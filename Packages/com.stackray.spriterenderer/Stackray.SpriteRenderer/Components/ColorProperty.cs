using Unity.Mathematics;

namespace Stackray.SpriteRenderer {

  public struct ColorProperty : IDynamicBufferProperty<half4> {
    // using half for faster GetHashCode
    public half4 Value;

    public string BufferName => "colorBuffer";

    half4 IComponentValue<half4>.Value { get => Value; set => Value = value; }

    public bool Equals(half4 other) {
      return Value.Equals(other);
    }

    public half4 Convert(UnityEngine.SpriteRenderer spriteRenderer) {
      var color = spriteRenderer.color;
      return (half4)new float4(color.r, color.g, color.b, color.a);
    }

    public override int GetHashCode() => Value.GetHashCode();
  }
}