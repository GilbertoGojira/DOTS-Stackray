using Unity.Entities;
using UnityEngine;

namespace Stackray.Transforms {
  public class LookAtMainCameraProxy : MonoBehaviour, IConvertGameObjectToEntity {
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
      dstManager.AddComponentData(entity, new LookAtEntityPlane {
        Value = CameraConversionSystem.GetCameraPrimaryEntity(Camera.main)
      });
    }
  }
}