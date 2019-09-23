using Unity.Entities;

namespace Stackray.Text {
  [InternalBufferCapacity(54)]    // 54 is the worst case scenario for 9-slice sprite
  public struct VertexIndex : IBufferElementData {
    public int Value;
    public static implicit operator VertexIndex(int v) { return new VertexIndex { Value = v }; }
    public static implicit operator int(VertexIndex v) { return v.Value; }
  }
}