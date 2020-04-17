using Stackray.Collections;
using Stackray.Entities;
using Stackray.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Profiling;

namespace Stackray.Transforms {

  public enum SortStatus {
    PREPARE,
    START,
    CONTINUE,
    FINALIZE
  }

  [UpdateAfter(typeof(TransformSystemGroup))]
  public class SortSystem : SystemBase {

    private const int STEP_SIZE = 65_536;

    struct State {
      SortStatus m_status;
      public SortStatus Status {
        get => m_status;
        set {
          if (m_status != value) {
            Offset = 0;
            Length = 0;
            m_status = value;
          }
        }
      }
      public int Offset;
      public int Length;
    }
    State m_state;

    EntityQuery m_query;
    int m_cachedOrderVersion;
    NativeList<DataWithEntity<float>> m_distancesToCamera;
    TimeSpanParallelSort<DataWithEntity<float>> m_parallelSort;
    Entity m_cameraEntity;

    protected override void OnCreate() {
      base.OnCreate();
      m_query = GetEntityQuery(
        new EntityQueryDesc {
          All = new ComponentType[] { ComponentType.ReadOnly<LocalToWorld>() },
          Options = EntityQueryOptions.IncludeDisabled
        });
      m_distancesToCamera = new NativeList<DataWithEntity<float>>(Allocator.Persistent);
      m_parallelSort = new TimeSpanParallelSort<DataWithEntity<float>>();
      var sortedEntities = EntityManager.CreateEntity(typeof(SortedEntities));
      EntityManager.AddBuffer<SortedEntity>(sortedEntities);
#if UNITY_EDITOR
      EntityManager.SetName(sortedEntities, $"{nameof(SortSystem)} Sorted Entities");
      CreateStats();
#endif
      SetSingleton<SortedEntities>(default);      
    }

    protected override void OnDestroy() {
      m_distancesToCamera.Dispose();
      m_parallelSort.Dispose();
    }

    void CalcDistance(
      NativeArray<DataWithEntity<float>> distancesToCamera,
      float4x4 cameraLocalToWorld,
      int offset,
      int length) {

      Entities
        .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabled)
        .ForEach((Entity entity, int entityInQueryIndex, in LocalToWorld localToWorld) => {
          if (entityInQueryIndex >= offset && entityInQueryIndex < offset + length) {
            // TODO: So far we only detect if the entity is behind the camera or not
            var isVisible = new Plane(cameraLocalToWorld.Forward(), cameraLocalToWorld.Position())
              .GetDistanceToPoint(localToWorld.Position) > 0;
            distancesToCamera[entityInQueryIndex] = new DataWithEntity<float> {
              Entity = entity,
              Index = entityInQueryIndex,
              Value = isVisible ? math.distancesq(localToWorld.Position, cameraLocalToWorld.Position()) : float.MaxValue
            };
          }
        }).ScheduleParallel();
    }

    [BurstCompile]
    struct UpdateSortedEntities : IJobParallelFor {
      [ReadOnly]
      public NativeArray<DataWithEntity<float>> SortedIndices;
      [NativeDisableParallelForRestriction]
      public NativeArray<SortedEntity> SortedEntities;

      public void Execute(int index) {
        SortedEntities[index] = new SortedEntity {
          Value = SortedIndices[index].Entity
        };
      }
    }

    [BurstCompile]
    struct InitSortedEntities : IJobParallelFor {
      [ReadOnly]
      [DeallocateOnJobCompletion]
      public NativeArray<Entity> Values;
      [NativeDisableParallelForRestriction]
      public NativeArray<SortedEntity> SortedEntities;

      public void Execute(int index) {
        SortedEntities[index] = new SortedEntity {
          Value = Values[index]
        };
      }
    }    

    protected override void OnUpdate() {
      if (m_query.GetCombinedComponentOrderVersion() != m_cachedOrderVersion) {
        m_cachedOrderVersion = m_query.GetCombinedComponentOrderVersion();
        Dependency = EntityManager.UniversalQuery.ResizeBuffer<SortedEntity>(this, m_query.CalculateEntityCount(), Dependency);
        Dependency.Complete();
        Dependency = new InitSortedEntities {
          Values = m_query.ToEntityArray(Allocator.TempJob),
          SortedEntities = EntityManager.GetBuffer<SortedEntity>(GetSingletonEntity<SortedEntities>()).AsNativeArray()
        }.Schedule(m_query.CalculateEntityCount(), 64, Dependency);
        m_state.Status = SortStatus.PREPARE;
        m_state.Offset = 0;
      }

      Profiler.BeginSample("Timespan Parallel Sort");
      switch (m_state.Status) {
        case SortStatus.PREPARE:
          Profiler.BeginSample("PREPARE");
          PrepareData();
          Profiler.EndSample();
          break;
        case SortStatus.START:
          Profiler.BeginSample("START");
          Dependency = m_parallelSort.Start(m_distancesToCamera, STEP_SIZE, inputDeps: Dependency);
          m_state.Status = SortStatus.CONTINUE;
          Profiler.EndSample();
          break;
        case SortStatus.CONTINUE:
          Profiler.BeginSample("CONTINUE");
          Dependency = m_parallelSort.Update(Dependency);
          if (m_parallelSort.IsComplete)
            m_state.Status = SortStatus.FINALIZE;
          Profiler.EndSample();
          break;
        case SortStatus.FINALIZE:
          Profiler.BeginSample("FINALIZE");
          Dependency = Finalize(Dependency);
          Profiler.EndSample();
          break;
      }

      EntityManager.SetComponentData(
        GetSingletonEntity<SortedEntities>(),
        new SortedEntities { Status = m_state.Status });
#if UNITY_EDITOR
      UpdateStats(m_state);
#endif
      Profiler.EndSample();
    }

    Entity GetCameraSingletonEnity() {
      // TODO remove this
      // UNITY Bug:
      // For some reason GetSingletonEntity yields wrong Entity after a while
      // We cache it here until this bug is solved
      m_cameraEntity = m_cameraEntity == Entity.Null ? 
        (HasSingleton<MainCameraComponentData>() ? GetSingletonEntity<MainCameraComponentData>() : Entity.Null) :
        m_cameraEntity;
      return m_cameraEntity;
    }

    void PrepareData() {
      var mainCameraEntity = GetCameraSingletonEnity();
      if (mainCameraEntity == Entity.Null) {
        m_state.Length = 0;
        return;
      }

      if (m_state.Offset == 0) {
        m_state.Length = m_query.CalculateEntityCount();
        Dependency = m_distancesToCamera.Resize(m_state.Length, Dependency);
      }
      var mainCameraLocalToWorld = EntityManager.GetComponentData<LocalToWorld>(mainCameraEntity);
      var calcLength = math.min(STEP_SIZE * 4, m_state.Length - m_state.Offset);
      CalcDistance(
        m_distancesToCamera.AsDeferredJobArray(),
        mainCameraLocalToWorld.Value,
        m_state.Offset,
        calcLength);
      m_state.Offset += calcLength;
      if (m_state.Offset >= m_state.Length) {
        m_state.Status = SortStatus.START;
        m_state.Offset = 0;
      }
    }

    JobHandle Finalize(JobHandle inputDeps) {
      var length = m_parallelSort.SortedData.Length;
      inputDeps = new UpdateSortedEntities {
        SortedIndices = m_parallelSort.SortedData.AsDeferredJobArray(),
        SortedEntities = EntityManager.GetBuffer<SortedEntity>(GetSingletonEntity<SortedEntities>()).AsNativeArray()
      }.Schedule(length, 64, inputDeps);
      m_state.Status = SortStatus.PREPARE;
      m_state.Offset = 0;
      return inputDeps;
    }

#if UNITY_EDITOR
    struct Stat : IComponentData {
      public int Value;
    }
    struct StatState : IBufferElementData {
      public SortStatus Status;
      public int Value;
    }

    void CreateStats() {
      var statEntity = EntityManager.CreateEntity(typeof(Stat));
#if UNITY_EDITOR
      EntityManager.SetName(statEntity, $"{nameof(SortSystem)} Stats");
#endif
      SetSingleton<Stat>(default);
      var buffer = EntityManager.AddBuffer<StatState>(statEntity);
      foreach (var state in System.Enum.GetValues(typeof(SortStatus)))
        buffer.Add(new StatState {
          Status = (SortStatus)state,
          Value = 0
        });
    }

    void UpdateStats(State state) {
      Entities
        .ForEach((DynamicBuffer<StatState> statStateBuffer, ref Stat stat) => {
          if (state.Status == SortStatus.START) {
            var total = 0;
            for (var i = 0; i < statStateBuffer.Length; ++i) {
              var statState = statStateBuffer[i];
              total += statState.Value;
              statState.Value = 0;
              statStateBuffer[i] = statState;
            }
            stat.Value = total;
          }

          for (var i = 0; i < statStateBuffer.Length; ++i) {
            if (statStateBuffer[i].Status == state.Status) {
              var statState = statStateBuffer[i];
              statState.Value++;
              statStateBuffer[i] = statState;
            }
          }
        }).ScheduleParallel();
    }

#endif
  }
}
