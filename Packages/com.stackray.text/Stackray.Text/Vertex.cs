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
    public float3 Normal;
    public float4 Color;
    public float2 TexCoord0;
    public float2 TexCoord1;
  }
}