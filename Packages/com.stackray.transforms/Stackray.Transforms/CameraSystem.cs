using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Stackray.Transforms {
  [AlwaysUpdateSystem]
  public class CameraSystem : ComponentSystem {
    IDictionary<int, Tuple<Camera, Entity>> m_cameraMap = new Dictionary<int, Tuple<Camera, Entity>>();

    protected override void OnStartRunning() {
      base.OnStartRunning();
      RegisterCameras();
    }

    protected override void OnStopRunning() {
      base.OnStopRunning();
      UnregisterCameras();
    }

    private void RegisterCameras() {
      // Required to cache DPI for the first time
      // Also because DPI must be accessed from the main thread
      // This will allow 'CameraUtility.ScreenDpi' to be used in jobs
      var cameras = Object.FindObjectsOfType<Camera>();
      foreach (var camera in cameras)
        AddCamera(camera);
    }

    private void UnregisterCameras() {
      if(HasSingleton<MainCameraComponentData>())
        EntityManager.DestroyEntity(GetSingletonEntity<MainCameraComponentData>());
      var cameras = Object.FindObjectsOfType<Camera>();
      foreach (var camera in cameras)
        RemoveCamera(camera);
    }

    private void AddCamera(Camera camera) {
      var entity =
        (camera.gameObject.GetComponent<GameObjectEntity>() ?? camera.gameObject.AddComponent<GameObjectEntity>())
        .Entity;
      EntityManager.AddComponentData(entity,
        new LocalToWorld {
          Value = float4x4.identity
        });
      EntityManager.AddComponentData(entity, new CopyTransformFromGameObject());
      if (camera == Camera.main && EntityManager.Exists(entity)) {
        EntityManager.AddComponentData(entity, new MainCameraComponentData());
        SetSingleton(default(MainCameraComponentData));
      }

#if UNITY_EDITOR
      EntityManager.SetName(entity, camera.name);
#endif
    }

    private void RemoveCamera(Camera camera) {
      var goe = camera.gameObject.GetComponent<GameObjectEntity>();
      Object.Destroy(goe);
    }

    protected override void OnUpdate() { }
  }
}
