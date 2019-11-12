using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Stackray.Transforms {
  [UpdateInGroup(typeof(GameObjectBeforeConversionGroup))]
  class CameraConversionSystem : GameObjectConversionSystem {
    static Dictionary<Camera, Entity> m_cameraEntities = new Dictionary<Camera, Entity>();

    public static Entity GetCameraPrimaryEntity(Camera camera) {
      if (!m_cameraEntities.TryGetValue(camera, out var entity))
        Debug.LogWarning($"GetCameraPrimaryEntity({camera}) was not included in the conversion and will be ignored.");
      return entity;
    }

    protected override void OnUpdate() {
      Entities.ForEach((Camera camera, ConvertToEntity converter) => {
        if (converter.ConversionMode == ConvertToEntity.Mode.ConvertAndDestroy)
          throw new ArgumentException($"Camera ({camera}) can only be converted using mode '{ConvertToEntity.Mode.ConvertAndInjectGameObject}'");
        var entity = GetPrimaryEntity(camera);
        DstEntityManager.AddComponentData(entity, new CameraComponentData());
        if (camera == Camera.main) {
          DstEntityManager.AddComponentData(entity, new MainCameraComponentData());
          DstWorld.GetOrCreateSystem<CameraSystem>().SetSingleton(new MainCameraComponentData());
        }
        m_cameraEntities.Add(camera, entity);
      });
    }
  }
}
