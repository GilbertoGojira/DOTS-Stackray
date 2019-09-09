using Unity.Entities;

namespace Stackray.Text {
  public struct BatchVertexIndex : IBufferElementData {
    public int Value;
    public static implicit operator BatchVertexIndex(int v) { return new BatchVertexIndex { Value = v }; }
    public static implicit operator int(BatchVertexIndex v) { return v.Value; }
  }
}