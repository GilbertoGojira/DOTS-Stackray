using Unity.Transforms;

namespace Stackray.SpriteRenderer {
  public struct LocalToWorldProperty : IFixedBufferProperty<LocalToWorld> {
    public string BufferName => "localToWorldBuffer";
  }
}