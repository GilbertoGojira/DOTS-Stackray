using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;

namespace Stackray.Text {
  [StructLayout(LayoutKind.Sequential)]
  public struct BatchVertex : IBufferElementData {
    public float3 Position;
    public float3 Normal;
    public float4 Color;
    public float2 TexCoord0;
    public float2 TexCoord1;

    public static implicit operator BatchVertex(Vertex v) {
      return new BatchVertex {
        Position = v.Position,
        Normal = v.Normal,
        Color = v.Color,
        TexCoord0 = v.TexCoord0,
        TexCoord1 = v.TexCoord1
      };
    }
    public static implicit operator Vertex(BatchVertex v) {
      return new Vertex() {
        Position = v.Position,
        Normal = v.Normal,
        Color = v.Color,
        TexCoord0 = v.TexCoord0,
        TexCoord1 = v.TexCoord1
      };
    }
  }
}