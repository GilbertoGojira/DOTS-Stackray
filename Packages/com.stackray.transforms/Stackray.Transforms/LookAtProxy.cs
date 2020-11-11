using Unity.Entities;
using UnityEngine;

namespace Stackray.Transforms {
  public class LookAtProxy : MonoBehaviour, IConvertGameObjectToEntity {
    public GameObject Target;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
      dstManager.AddComponentData(entity, new LookAtEntityPlane {
        Value = conversionSystem.GetPrimaryEntity(Target)
      });
    }
  }
}