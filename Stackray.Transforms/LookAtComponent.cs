using System;
using Unity.Entities;
using Unity.Mathematics;
using Stackray.Entities;

namespace Stackray.Transforms {
  [Serializable]
  public struct LookAtEntity : IComponentData, IComponentValue<Entity> {
    public Entity Value;

    Entity IComponentValue<Entity>.Value { get => Value; set => Value = value; }
  }

  [Serializable]
  public struct LookAtEntityPlane : IComponentData, IComponentValue<Entity> {
    public Entity Value;

    Entity IComponentValue<Entity>.Value { get => Value; set => Value = value; }
  }

  [Serializable]
  public struct LookAtPosition : IComponentData, IComponentValue<float3> {
    public float3 Value;

    float3 IComponentValue<float3>.Value { get => Value; set => Value = value; }
  }
}
