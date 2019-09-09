using Unity.Entities;
using Unity.Mathematics;

namespace Stackray.Text {
  public struct TextFontAsset : IComponentData {
    public float LineHeight;
    public float NormalSpace;
    public float BoldSpace;
    public float AscentLine;
    public float CapLine;
    public float MeanLine;
    public float Baseline;
    public float DescentLine;
    public float PointSize;
    public float BoldStyle;
    public float NormalStyle;

    public float2 AtlasSize;
    public int NativeMaterialId;    // TODO: Temporary hack

  }
}