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
  [UpdateInGroup(typeof(TransformSystemGroup))]
  public class CopyFromCameraSystem : JobComponentSystem {

    EntityQuery m_usedCameraQuery;
    EntityQuery m_freeCameraQuery;
    ComponentAccessState<Camera> m_cameraAccessState;
    NativeHashMap<Entity, LocalToWorld> m_changedTransforms;

    protected override void OnCreate() {
      base.OnCreate();
      m_freeCameraQuery = GetEntityQuery(ComponentType.Exclude<CameraComponentData>(), typeof(Camera));
      m_usedCameraQuery = GetEntityQuery(typeof(CameraComponentData), typeof(Camera), typeof(Transform), typeof(LocalToWorld));
      m_changedTransforms = new NativeHashMap<Entity, LocalToWorld>(0, Allocator.Persistent);
    }

    protected override void OnDestroy() {
      base.OnDestroy();
      m_cameraAccessState.Dispose();
      m_changedTransforms.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {
      if (m_freeCameraQuery.CalculateEntityCount() > 0)
        EntityManager.AddComponent(m_freeCameraQuery, typeof(CameraComponentData));
      inputDeps = CopyToCamera(m_usedCameraQuery, inputDeps);
      var cameras = m_usedCameraQuery.GetComponentAccess(EntityManager, ref m_cameraAccessState);
      var cameraData = m_usedCameraQuery.ToComponentDataArray<CameraComponentData>(Allocator.TempJob);
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
      m_usedCameraQuery.CopyFromComponentDataArray(cameraData, out var copyHandle);
      inputDeps = JobHandle.CombineDependencies(inputDeps, copyHandle);
      inputDeps = new CalcCameraCache {
        ScreenSize = new float2(Screen.width, Screen.height)
      }.Schedule(this, inputDeps);
      inputDeps = cameraData.Dispose(inputDeps);
      return inputDeps;
    }

    JobHandle CopyToCamera(EntityQuery query, JobHandle inputDeps) {
      query.SetFilterChanged(typeof(LocalToWorld));
      var changedEntities = query.CalculateEntityCount();
      query.ResetFilter();
      if (changedEntities == 0) {
        query.GetChangedTransformFromEntity(this, m_changedTransforms, inputDeps, out inputDeps);
        query.CopyFromChangedComponentData(m_changedTransforms, inputDeps, out inputDeps);
        return inputDeps;
      }
      var entities = query.ToEntityArray(Allocator.TempJob, out var toEntityHandle);
      inputDeps = JobHandle.CombineDependencies(inputDeps, toEntityHandle);
      inputDeps = new CopyTransforms {
        LocalToWorldFromEntity = GetComponentDataFromEntity<LocalToWorld>(true),
        entities = entities
      }.Schedule(query.GetTransformAccessArray(), inputDeps);
      return inputDeps;
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

        if (LocalToWorldFromEntity.Exists(entity)) {
          var localToWorld = LocalToWorldFromEntity[entity];
          transform.localPosition = localToWorld.Position;
          transform.localRotation = localToWorld.Value.Rotation();
          transform.localScale = localToWorld.Value.Scale();
        }
      }
    }

    [BurstCompile]
    struct CalcCameraCache : IJobForEachWithEntity<LocalToWorld, CameraComponentData> {
      public float2 ScreenSize;
      public void Execute(Entity entity, int index, [ReadOnly]ref LocalToWorld localToWorld, [WriteOnly]ref CameraComponentData cameraData) {
        cameraData.UpdatePlane(localToWorld.Value);
        cameraData.CalcCachedPointsPerPixel(localToWorld.Value, ScreenSize);
      }
    }
  }
}
