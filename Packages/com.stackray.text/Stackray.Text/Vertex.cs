using Unity.Entities;
using Unity.Mathematics;

namespace Stackray.Text {
  /// <summary>
  /// 16 seems like a quite big buffer, but it's enough to store 9-slice sprite vertices.
  /// It is the most common sprite type used in UI. I noticed performance increase compared to smaller buffers
  /// </summary>
  [InternalBufferCapacity(16)]
  public struct Vertex : IBufferElementData {
    public float3 Position;
    public half4 Normal;
    public half4 Color;
    public half2 TexCoord0;
    public half2 TexCoord1;
  }
}