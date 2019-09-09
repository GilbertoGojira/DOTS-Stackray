using TMPro;
using Unity.Entities;

namespace Stackray.Text {
  public struct TextRenderer : IComponentData {
    public float Size;
    public TextAlignmentOptions Alignment;
    public bool Bold;
    public bool Italic;
    public Entity CanvasEntity;
    public Entity Font;
    public int MaterialId;
  }
}