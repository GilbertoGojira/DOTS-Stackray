using Unity.Entities;

namespace Stackray.SpriteRenderer {
  public struct SubMeshInfo : IBufferElementData {
    public int Offset;
    public int MaterialId;
  }
}