using Stackray.Entities;
using Stackray.Jobs;
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
  public class SortSystem : JobComponentSystem {

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

    [BurstCompile]
    struct CalcDistance : IJobForEachWithEntity<LocalToWorld> {
      [WriteOnly]
      public NativeArray<DataWithEntity<float>> DistancesToCamera;
      public float4x4 CameraLocalToWorld;
      public int Offset;
      public int Length;
      public void Execute(Entity entity, int index, [ReadOnly] ref LocalToWorld localToWorld) {
        if (index >= Offset && index < Offset + Length) {
          // TODO: So far we only detect if the entity is behind the camera or not
          var isVisible = new Plane(CameraLocalToWorld.Forward(), CameraLocalToWorld.Position())
            .GetDistanceToPoint(localToWorld.Position) > 0;
          DistancesToCamera[index] = new DataWithEntity<float> {
            Entity = entity,
            Index = index,
            Value = isVisible ? math.distancesq(localToWorld.Position, CameraLocalToWorld.Position()) : float.MaxValue
          };
        }
      }
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

    protected override JobHandle OnUpdate(JobHandle inputDeps) {
      if (m_query.GetCombinedComponentOrderVersion() != m_cachedOrderVersion) {
        m_cachedOrderVersion = m_query.GetCombinedComponentOrderVersion();
        inputDeps = EntityManager.UniversalQuery.ResizeBuffer<SortedEntity>(this, m_query.CalculateEntityCount(), inputDeps);
        inputDeps.Complete();
        inputDeps = new InitSortedEntities {
          Values = m_query.ToEntityArray(Allocator.TempJob),
          SortedEntities = EntityManager.GetBuffer<SortedEntity>(GetSingletonEntity<SortedEntities>()).AsNativeArray()
        }.Schedule(m_query.CalculateEntityCount(), 64, inputDeps);
        m_state.Status = SortStatus.PREPARE;
        m_state.Offset = 0;
      }

      Profiler.BeginSample("Timespan Parallel Sort");
      switch (m_state.Status) {
        case SortStatus.PREPARE:
          Profiler.BeginSample("PREPARE");
          inputDeps = PrepareData(inputDeps);
          Profiler.EndSample();
          break;
        case SortStatus.START:
          Profiler.BeginSample("START");
          inputDeps = m_parallelSort.Start(m_distancesToCamera, STEP_SIZE, inputDeps: inputDeps);
          m_state.Status = SortStatus.CONTINUE;
          Profiler.EndSample();
          break;
        case SortStatus.CONTINUE:
          Profiler.BeginSample("CONTINUE");
          inputDeps = m_parallelSort.Update(inputDeps);
          if (m_parallelSort.IsComplete)
            m_state.Status = SortStatus.FINALIZE;
          Profiler.EndSample();
          break;
        case SortStatus.FINALIZE:
          Profiler.BeginSample("FINALIZE");
          inputDeps = Finalize(inputDeps);
          Profiler.EndSample();
          break;
      }

      EntityManager.SetComponentData(
        GetSingletonEntity<SortedEntities>(),
        new SortedEntities { Status = m_state.Status });
#if UNITY_EDITOR
      inputDeps = new UpdateStats {
        State = m_state
      }.Schedule(this, inputDeps);
#endif
      Profiler.EndSample();
      return inputDeps;
    }

    JobHandle PrepareData(JobHandle inputDeps) {
      if (m_state.Offset == 0) {
        m_state.Length = m_query.CalculateEntityCount();
        inputDeps = new ResizeNativeList<DataWithEntity<float>> {
          Source = m_distancesToCamera,
          Length = m_state.Length
        }.Schedule(inputDeps);
      }
      var calcLength = math.min(STEP_SIZE * 4, m_state.Length - m_state.Offset);
      inputDeps = new CalcDistance {
        DistancesToCamera = m_distancesToCamera.AsDeferredJobArray(),
        CameraLocalToWorld = Camera.main.transform.localToWorldMatrix,
        Offset = m_state.Offset,
        Length = calcLength
      }.Schedule(m_query, inputDeps);
      m_state.Offset += calcLength;
      if (m_state.Offset >= m_state.Length) {
        m_state.Status = SortStatus.START;
        m_state.Offset = 0;
      }
      return inputDeps;
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

    [BurstCompile]
    struct UpdateStats : IJobForEach_BC<StatState, Stat> {
      public State State;
      public void Execute(DynamicBuffer<StatState> statStateBuffer, [WriteOnly]ref Stat stat) {

        if (State.Status == SortStatus.START) {
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
          if (statStateBuffer[i].Status == State.Status) {
            var statState = statStateBuffer[i];
            statState.Value++;
            statStateBuffer[i] = statState;
          }
        }
      }
    }
#endif
  }
}
