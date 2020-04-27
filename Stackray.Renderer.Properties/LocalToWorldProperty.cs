using Unity.Transforms;

namespace Stackray.Renderer {
  public struct LocalToWorldProperty : IFixedBufferProperty<LocalToWorld> {
    public string BufferName => "localToWorldBuffer";
  }
}