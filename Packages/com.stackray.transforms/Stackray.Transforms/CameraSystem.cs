#if UNITY_2019_3
using Stackray.Collections;
#endif
using Stackray.Entities;
using Stackray.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Jobs;

namespace Stackray.Transforms {
  /// <summary>
  /// This system will copy camera matrices from a designated camera
  /// </summary>
  [AlwaysUpdateSystem]
  [UpdateInGroup(typeof(TransformSystemGroup))]
  public class CameraSystem : SystemBase {

    EntityQuery m_cameraQuery;
    ComponentAccessState<Camera> m_cameraAccessState;
    NativeHashMap<Entity, LocalToWorld> m_changedTransforms;

    protected override void OnCreate() {
      base.OnCreate();
      m_cameraQuery = GetEntityQuery(typeof(CameraComponentData), typeof(Camera), typeof(Transform), ComponentType.ReadWrite<LocalToWorld>());
      m_changedTransforms = new NativeHashMap<Entity, LocalToWorld>(0, Allocator.Persistent);
    }

    protected override void OnDestroy() {
      base.OnDestroy();
      m_cameraAccessState.Dispose();
      m_changedTransforms.Dispose();
    }

    protected override void OnStartRunning() {
      base.OnStartRunning();
      // Force all cameras to be converted to entities
      var cameras = Resources.FindObjectsOfTypeAll<Camera>();
      foreach (var camera in cameras) {
        if (camera.gameObject.scene.isLoaded) {
          var converter = camera.GetComponent<ConvertToEntity>() ?? camera.gameObject.AddComponent<ConvertToEntity>();
          converter.ConversionMode = ConvertToEntity.Mode.ConvertAndInjectGameObject;
        }
      }
    }

    [BurstCompile]
    struct CopyTransforms : IJobParallelForTransform {
      [ReadOnly]
      public ComponentDataFromEntity<LocalToWorld> LocalToWorldFromEntity;
      [ReadOnly]
      [DeallocateOnJobCompletion]
      public NativeArray<Entity> entities;

      public void Execute(int index, TransformAccess transform) {
        var entity = entities[index];

        if (LocalToWorldFromEntity.HasComponent(entity)) {
          var localToWorld = LocalToWorldFromEntity[entity];
          transform.localPosition = localToWorld.Position;
          transform.localRotation = localToWorld.Value.Rotation();
          transform.localScale = localToWorld.Value.Scale();
        }
      }
    }

    void CopyToCamera(EntityQuery query) {
      query.SetChangedVersionFilter(typeof(LocalToWorld));
      var changedEntities = query.CalculateEntityCount();
      query.ResetFilter();
      if (changedEntities == 0) {
        Dependency = query.GetChangedTransformFromEntity(this, ref m_changedTransforms, Dependency);
        Dependency = query.CopyFromChangedComponentData(this, ref m_changedTransforms, Dependency);
        return;
      }
      var entities = query.ToEntityArrayAsync(Allocator.TempJob, out var toEntityHandle);
      Dependency = JobHandle.CombineDependencies(Dependency, toEntityHandle);
      Dependency = new CopyTransforms {
        LocalToWorldFromEntity = GetComponentDataFromEntity<LocalToWorld>(true),
        entities = entities
      }.Schedule(query.GetTransformAccessArray(), Dependency);
    }

    void CalcCameraCache(float2 screenSize) {
      Entities
          .ForEach((Entity entity, int entityInQueryIndex, ref CameraComponentData cameraData, in LocalToWorld localToWorld) => {
            cameraData.UpdatePlane(localToWorld.Value);
            cameraData.CalcCachedPointsPerPixel(localToWorld.Value, screenSize);
          }).ScheduleParallel();
    }


    protected override void OnUpdate() {
      CopyToCamera(m_cameraQuery);
      var cameras = m_cameraQuery.GetComponentAccess(EntityManager, ref m_cameraAccessState);
      var cameraData = m_cameraQuery.ToComponentDataArray<CameraComponentData>(Allocator.TempJob);
      for (var i = 0; i < cameraData.Length; ++i) {
        var camera = cameras[i];
        cameraData[i] = new CameraComponentData {
          ProjectionMatrix = camera.projectionMatrix,
          WorldToCameraMatrix = camera.worldToCameraMatrix,
          IsOrthographic = camera.orthographic,
          OrthographicSize = camera.orthographicSize,
          FieldOfView = camera.fieldOfView,
          NearClipPlane = camera.nearClipPlane,
          FarClipPlane = camera.farClipPlane,
          Aspect = camera.aspect
        };
      }
      m_cameraQuery.CopyFromComponentDataArrayAsync(cameraData, out var copyHandle);
      Dependency = JobHandle.CombineDependencies(Dependency, copyHandle);
      CalcCameraCache(new float2(Screen.width, Screen.height));
      Dependency = cameraData.Dispose(Dependency);
    }
  }
}
