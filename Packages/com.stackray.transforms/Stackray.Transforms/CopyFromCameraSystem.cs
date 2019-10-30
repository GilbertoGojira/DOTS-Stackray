using Stackray.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Jobs;

namespace Stackray.Transforms {
  /// <summary>
  /// This system will copy camera matrices from a designated camera
  /// </summary>
  [UpdateAfter(typeof(TransformSystemGroup))]
  public class CopyFromCameraSystem : JobComponentSystem {

    EntityQuery m_usedCameraQuery;
    EntityQuery m_freeCameraQuery;
    ComponentAccessState<Camera> m_cameraAccessState;

    protected override void OnCreate() {
      base.OnCreate();
      m_freeCameraQuery = GetEntityQuery(ComponentType.Exclude<CameraComponentData>(), typeof(Camera));
      m_usedCameraQuery = GetEntityQuery(typeof(CameraComponentData), typeof(Camera), typeof(Transform));
    }

    protected override void OnDestroy() {
      base.OnDestroy();
      m_cameraAccessState.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {
      if (m_freeCameraQuery.CalculateEntityCount() > 0)
        EntityManager.AddComponent(m_freeCameraQuery, typeof(CameraComponentData));
      var entities = m_usedCameraQuery.ToEntityArray(Allocator.TempJob, out var toEntityHandle);
      inputDeps = JobHandle.CombineDependencies(inputDeps, toEntityHandle);
      inputDeps = new CopyTransforms {
        Positions = GetComponentDataFromEntity<Translation>(true),
        Rotations = GetComponentDataFromEntity<Rotation>(true),
        Scales = GetComponentDataFromEntity<NonUniformScale>(true),
        entities = entities
      }.Schedule(m_usedCameraQuery.GetTransformAccessArray(), inputDeps);
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
      inputDeps = new CalcCameraCache().Schedule(this, inputDeps);
      inputDeps = cameraData.Dispose(inputDeps);
      return inputDeps;
    }

    [BurstCompile]
    struct CopyTransforms : IJobParallelForTransform {
      [ReadOnly] public ComponentDataFromEntity<Translation> Positions;
      [ReadOnly] public ComponentDataFromEntity<Rotation> Rotations;
      [ReadOnly] public ComponentDataFromEntity<NonUniformScale> Scales;

      [ReadOnly]
      [DeallocateOnJobCompletion]
      public NativeArray<Entity> entities;

      public void Execute(int index, TransformAccess transform) {
        var entity = entities[index];

        if (Positions.Exists(entity))
          transform.localPosition = Positions[entity].Value;

        if (Rotations.Exists(entity))
          transform.localRotation = Rotations[entity].Value;

        if (Scales.Exists(entity))
          transform.localScale = Scales[entity].Value;
      }
    }

    [BurstCompile]
    struct CalcCameraCache : IJobForEachWithEntity<LocalToWorld, CameraComponentData> {
      public void Execute(Entity entity, int index, [ReadOnly]ref LocalToWorld localToWorld, [WriteOnly]ref CameraComponentData cameraData) {
        cameraData.UpdatePlane(localToWorld.Value);
        cameraData.CalcCachedPointsPerPixel(localToWorld.Value);
      }
    }
  }
}
