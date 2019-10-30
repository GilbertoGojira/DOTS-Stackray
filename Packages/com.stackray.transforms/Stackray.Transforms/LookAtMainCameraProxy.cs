using Unity.Entities;
using UnityEngine;

namespace Stackray.Transforms {
  [RequiresEntityConversion]
  public class LookAtMainCameraProxy : MonoBehaviour, IConvertGameObjectToEntity {
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
      var gameObjectEntity = 
        Camera.main.GetComponent<GameObjectEntity>() ?? 
        Camera.main.gameObject.AddComponent<GameObjectEntity>();

      dstManager.AddComponentData(entity, new LookAtEntityPlane {
        Value = gameObjectEntity.Entity
      });
    }
  }
}