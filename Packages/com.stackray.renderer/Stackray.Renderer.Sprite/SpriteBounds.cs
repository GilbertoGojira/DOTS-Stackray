using Unity.Entities;
using Unity.Mathematics;

namespace Stackray.Renderer {
  public struct SpriteBounds : IComponentData {
    public AABB Value;
  }
}

