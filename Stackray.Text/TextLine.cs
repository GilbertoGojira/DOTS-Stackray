using Unity.Entities;

namespace Stackray.Text {
  public struct TextLine : IBufferElementData {
    public int CharacterOffset;
    public float LineWidth;
  }
}