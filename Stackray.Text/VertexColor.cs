using Unity.Entities;
using Unity.Mathematics;

namespace Stackray.Text {
  public struct VertexColorMultiplier : IComponentData {
    public float4 Value;
  }

  public struct VertexColor : IComponentData {
    public float4 Value;
  }
}