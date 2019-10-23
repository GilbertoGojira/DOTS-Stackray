using Unity.Entities;

namespace Stackray.Transforms {
  public struct SortedEntities : IComponentData {
    public SortStatus Status;
  }

  public struct SortedEntity : IBufferElementData {
    public Entity Value;
  }
}