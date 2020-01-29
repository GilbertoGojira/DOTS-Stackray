using Unity.Entities;
using UnityEngine.TextCore;

namespace Stackray.Text {
  public struct FontGlyph : IBufferElementData {
    public ushort Character;
    public float Scale;
    public GlyphRect Rect;
    public GlyphMetrics Metrics;
  }
}