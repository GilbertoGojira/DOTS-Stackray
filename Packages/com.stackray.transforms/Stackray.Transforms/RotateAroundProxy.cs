using Stackray.Entities;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Stackray.Transforms {

  public class RotateAroundProxy : ConvertReferences, IConvertGameObjectToEntity {
    public float3 Axis;
    /// <summary>
    /// Angle in degrees
    /// </summary>
    public float Angle;
    public GameObject Target;

    private void Awake() {
      AddReference(Target);
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
      
      dstManager.AddComponentData(entity,
        new RotateAround {
          Value = quaternion.AxisAngle(Axis, math.radians(Angle)),
          Target = GetPrimaryEntity(Target)
        });
    }
  }
}
