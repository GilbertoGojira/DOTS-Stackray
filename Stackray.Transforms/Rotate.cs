using Unity.Entities;
using Unity.Mathematics;

namespace Stackray.Transforms {
  public struct Rotate : IComponentData {
    public quaternion Value;
  }

  public struct RotateAround : IComponentData {
    public quaternion Value;
    public Entity Target;
  }
}
