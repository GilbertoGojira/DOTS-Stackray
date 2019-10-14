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

  [UpdateAfter(typeof(TransformSystemGroup))]
  public class SortSystem : JobComponentSystem {

    private const int STEP_SIZE = 65_536;

    struct State {
      public enum EStatus {
        PREPARE,
        START,
        CONTINUE,
        FINALIZE
      }
      EStatus m_status;
      public EStatus Status {
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
    EntityQuery m_missingSortIndexQuery;
    NativeList<DataWithEntity<float>> m_distancesToCamera;
    TimeSpanParallelSort<DataWithEntity<float>> m_parallelSort;

    protected override void OnCreate() {
      base.OnCreate();
      m_query = GetEntityQuery(ComponentType.ReadOnly<LocalToWorld>(), ComponentType.ReadWrite<SortIndex>());
      m_missingSortIndexQuery = GetEntityQuery(ComponentType.ReadOnly<LocalToWorld>(), ComponentType.Exclude<SortIndex>());
      m_distancesToCamera = new NativeList<DataWithEntity<float>>(Allocator.Persistent);
      m_parallelSort = new TimeSpanParallelSort<DataWithEntity<float>>();
      CreateStats();
    }

    protected override void OnDestroy() {
      m_distancesToCamera.Dispose();
      m_parallelSort.Dispose();
    }

    [BurstCompile]
    struct InitIndices : IJobForEachWithEntity<SortIndex> {
      public void Execute(Entity entity, int index, [WriteOnly]ref SortIndex sortIndex) {
        sortIndex.Value = index;
      }
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
    struct CopySortedIndices : IJobParallelFor {
      [ReadOnly]
      public NativeArray<DataWithEntity<float>> SortedIndices;
      [NativeDisableParallelForRestriction]
      public ComponentDataFromEntity<SortIndex> SortIndexFromEntity;
      public int Offset;
      public void Execute(int index) {
        var entity = SortedIndices[index + Offset].Entity;
        if (SortIndexFromEntity.Exists(entity))
          SortIndexFromEntity[entity] = new SortIndex { Value = index + Offset };
      }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {
      if (m_missingSortIndexQuery.CalculateEntityCount() > 0) {
        EntityManager.AddComponent<SortIndex>(m_missingSortIndexQuery);
        inputDeps = new InitIndices().Schedule(m_query, inputDeps);
      }

      Profiler.BeginSample("Timespan Parallel Sort");
      switch (m_state.Status) {
        case State.EStatus.PREPARE:
          Profiler.BeginSample("PREPARE");
          inputDeps = PrepareData(inputDeps);
          Profiler.EndSample();
          break;
        case State.EStatus.START:
          Profiler.BeginSample("START");
          inputDeps = m_parallelSort.Start(m_distancesToCamera, STEP_SIZE, inputDeps: inputDeps);
          m_state.Status = State.EStatus.CONTINUE;
          Profiler.EndSample();
          break;
        case State.EStatus.CONTINUE:
          Profiler.BeginSample("CONTINUE");
          inputDeps = m_parallelSort.Update(inputDeps);
          if (m_parallelSort.IsComplete)
            m_state.Status = State.EStatus.FINALIZE;
          Profiler.EndSample();
          break;
        case State.EStatus.FINALIZE:
          Profiler.BeginSample("FINALIZE");
          inputDeps = Finalize(inputDeps);
          Profiler.EndSample();
          break;
      }
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
          Length = m_query.CalculateEntityCount()
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
        m_state.Status = State.EStatus.START;
        m_state.Offset = 0;
      }
      return inputDeps;
    }

    JobHandle Finalize(JobHandle inputDeps) {
      if (m_state.Offset == 0)
        m_state.Length = m_parallelSort.SortedData.Length;
      var copyLength = math.min(STEP_SIZE, m_state.Length - m_state.Offset);
      inputDeps = new CopySortedIndices {
        SortedIndices = m_parallelSort.SortedData.AsDeferredJobArray(),
        SortIndexFromEntity = GetComponentDataFromEntity<SortIndex>(false),
        Offset = m_state.Offset
      }.Schedule(copyLength, 64, inputDeps);
      m_state.Offset += copyLength;
      if (m_state.Offset >= m_state.Length) {
        m_state.Status = State.EStatus.PREPARE;
        m_state.Offset = 0;
      }
      return inputDeps;
    }

#if UNITY_EDITOR
    struct Stat : IComponentData {
      public int Value;
    }
    struct StatState : IBufferElementData {
      public State.EStatus Status;
      public int Value;
    }

    void CreateStats() {
      var statEntity = EntityManager.CreateEntity(typeof(Stat));
#if UNITY_EDITOR
      EntityManager.SetName(statEntity, $"{nameof(SortSystem)} Stats");
#endif
      SetSingleton<Stat>(default);
      var buffer = EntityManager.AddBuffer<StatState>(statEntity);
      foreach (var state in System.Enum.GetValues(typeof(State.EStatus)))
        buffer.Add(new StatState {
          Status = (State.EStatus)state,
          Value = 0
        });
    }

    [BurstCompile]
    struct UpdateStats : IJobForEach_BC<StatState, Stat> {
      public State State;
      public void Execute(DynamicBuffer<StatState> statStateBuffer, [WriteOnly]ref Stat stat) {

        if (State.Status == State.EStatus.START) {
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
