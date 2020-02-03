using Stackray.Collections;
using Stackray.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Stackray.Transforms {

  public abstract class LimitActiveComponentSystem<TComponentData> : JobComponentSystem
    where TComponentData : struct, IComponentData {

    public struct LimitActive<T> : IComponentData {
      public int Value;
    }

    public int Limit {
      get => GetSingleton<LimitActive<TComponentData>>().Value;
      set => SetSingleton(new LimitActive<TComponentData> { Value = value });
    }

    EntityQuery m_query;

    protected override void OnCreate() {
      base.OnCreate();
      m_query = GetEntityQuery(
        new EntityQueryDesc {
          All = new ComponentType[] { ComponentType.ReadOnly<Active>(), typeof(TComponentData) },
          Options = EntityQueryOptions.IncludeDisabled
        });
    }

    protected override void OnStartRunning() {
      base.OnStartRunning();
      if (!HasSingleton<LimitActive<TComponentData>>()) {
        EntityManager.CreateEntity(typeof(LimitActive<TComponentData>));
        Limit = -1;
      }
#if UNITY_EDITOR
      EntityManager.SetName(GetSingletonEntity<LimitActive<TComponentData>>(), $"Limit for {typeof(TComponentData)}");
#endif
    }

    [BurstCompile]
    struct GetNextActiveEntities : IJob {
      [ReadOnly]
      public NativeHashMap<Entity, int> EntitiesIndexMap;
      [ReadOnly]
      public NativeArray<SortedEntity> AllSortedEntities;
      public NativeHashSet<Entity> EntitiesToActivate;
      private int m_index;

      public void Execute() {
        var maxEntities = EntitiesToActivate.Capacity;
        for(var i = 0; i < AllSortedEntities.Length; ++i) {
          var entity = AllSortedEntities[i].Value;
          if (m_index < maxEntities && EntitiesIndexMap.ContainsKey(entity)) {
            EntitiesToActivate.TryAdd(entity);
            m_index++;
          }
        }
      }
    }

    [BurstCompile]
    struct UpdateActiveEntities : IJobForEachWithEntity<Active> {
      [ReadOnly]
      public NativeHashSet<Entity> ActiveEntities;

      public void Execute(Entity entity, int index, [WriteOnly]ref Active active) {
        active.Value = ActiveEntities.Contains(entity);
      }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {
      if (GetSingleton<SortedEntities>().Status != SortStatus.PREPARE)
        return inputDeps;
      var length = m_query.CalculateEntityCount();
      var limit = GetSingleton<LimitActive<TComponentData>>().Value;
      var allSortedEntites = EntityManager.GetAllSortedEntities(this, Allocator.TempJob);

      var entityIndexMap = new NativeHashMap<Entity, int>(length, Allocator.TempJob);
      inputDeps = m_query.ToEntityIndexMap(EntityManager, ref entityIndexMap, inputDeps);
      var entitiesToActivate = new NativeHashSet<Entity>(math.min(length, Limit < 0 ? int.MaxValue : Limit), Allocator.TempJob);
      inputDeps = new GetNextActiveEntities {
        EntitiesIndexMap = entityIndexMap,
        AllSortedEntities = allSortedEntites,
        EntitiesToActivate = entitiesToActivate
      }.Schedule(inputDeps);

      inputDeps = new UpdateActiveEntities {
        ActiveEntities = entitiesToActivate
      }.Schedule(m_query, inputDeps);

      inputDeps = JobHandle.CombineDependencies(
        entitiesToActivate.Dispose(inputDeps),
        entityIndexMap.Dispose(inputDeps),
        allSortedEntites.Dispose(inputDeps));

      return inputDeps;
    }

  }
}
