using Stackray.Entities;
using Unity.Mathematics;

namespace Stackray.Renderer {

  public struct FlipProperty : IDynamicBufferProperty<int2> {

    public int2 Value;

    public string BufferName => "flipBuffer";

    int2 IComponentValue<int2>.Value { get => Value; set => Value = value; }

    public bool Equals(int2 other) {
      return Value.Equals(other);
    }

    public int2 Convert(UnityEngine.SpriteRenderer spriteRenderer) {
      return new int2(spriteRenderer.flipX ? 1 : 0, spriteRenderer.flipY ? 1 : 0);
    }

    public override int GetHashCode() => Value.GetHashCode();
  }
}