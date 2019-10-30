using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Stackray.Transforms {
  [Serializable]
  public struct LookAtEntity : IComponentData {
    public Entity Value;
  }

  [Serializable]
  public struct LookAtEntityPlane : IComponentData {
    public Entity Value;
  }

  [Serializable]
  public struct LookAtPosition : IComponentData {
    public float3 Value;
  }
}
