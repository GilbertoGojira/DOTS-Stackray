using Unity.Entities;
using Unity.Mathematics;

namespace Stackray.SpriteRenderer {
  public struct SpriteBounds : IComponentData {
    public AABB Value;
  }
}

