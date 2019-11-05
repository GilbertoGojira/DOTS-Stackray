using Stackray.Entities;
using Unity.Mathematics;

namespace Stackray.SpriteRenderer {

  // NOTE: Compute buffer stride must be a multiple of 4
  // half3 -> stride 6
  // half4 -> stride 8
  public struct ScaleProperty : IDynamicBufferProperty<half4> {
    // using half for faster GetHashCode
    public half3 Value;

    public string BufferName => "scaleBuffer";

    half4 IComponentValue<half4>.Value { get => new half4(Value.x, Value.y, Value.z, (half)0); set => Value = value.xyz; }

    public bool Equals(half4 other) {
      return Value.Equals(other);
    }

    public half4 Convert(UnityEngine.SpriteRenderer spriteRenderer) {
      return new half4(new half3(spriteRenderer.sprite.bounds.size), new half(0));
    }

    public override int GetHashCode() => Value.GetHashCode();
  }
}