using Unity.Entities;

namespace Stackray.Text {

  public struct TextData : IComponentData {
    public NativeString64 Value;
  }
}
