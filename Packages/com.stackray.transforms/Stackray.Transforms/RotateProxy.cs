using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Stackray.Transforms {
  public class RotateProxy : MonoBehaviour, IConvertGameObjectToEntity {
    public float3 Axis;
    /// <summary>
    /// Angle in degrees
    /// </summary>
    public float Angle;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
      dstManager.AddComponentData(entity,
        new Rotate { Value = quaternion.AxisAngle(Axis, math.radians(Angle)) });
    }
  }
}
