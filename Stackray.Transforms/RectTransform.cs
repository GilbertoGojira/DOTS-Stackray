using Unity.Entities;
using Unity.Mathematics;

namespace Stackray.Transforms {
  public struct LocalRectTransform : IComponentData {
    public AABB Value; 
  }

  public struct WorldRectTransform : IComponentData {
    public AABB Value;
  }

  public struct ChunkWorldRectTransform : IComponentData {
    public AABB Value;
  }
}
