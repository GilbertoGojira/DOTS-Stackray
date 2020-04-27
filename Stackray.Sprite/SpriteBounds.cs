using Unity.Entities;
using Unity.Mathematics;

namespace Stackray.Sprite {
  public struct SpriteBounds : IComponentData {
    public AABB Value;
  }
}

