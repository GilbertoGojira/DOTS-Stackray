using Stackray.Entities;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Stackray.Renderer {

  public class SpriteAnimationSystem : JobComponentSystem {
    EntityQuery m_query;
    int m_lastOrderInfo;
    List<SpriteAnimation> m_spriteAnimations = new List<SpriteAnimation>();
    List<ISpritePropertyAnimator> m_spriteAnimators = new List<ISpritePropertyAnimator>();

    protected override void OnCreate() {
      base.OnCreate();
      var availableComponentTypes = TypeUtility.GetAvailableComponentTypes(typeof(IDynamicBufferProperty<>));
      m_query = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
            ComponentType.ReadOnly<SpriteAnimation>(),
            ComponentType.ReadOnly<SpriteAnimationState>()
        },
        Any = availableComponentTypes.Select(t => (ComponentType)t).ToArray()
      });

      foreach (var propertyType in availableComponentTypes) {
        var baseType = typeof(SpritePropertyAnimator<,>);
        var genericType0 = propertyType;
        var genericType1 = TypeUtility.ExtractInterfaceGenericType(propertyType, typeof(IComponentValue<>), 0);

        m_spriteAnimators.Add(
          TypeUtility.CreateInstance(
            baseType: baseType,
            genericType0: genericType0,
            genericType1: genericType1,
            constructorArgs: new object[] { this, m_query }) as ISpritePropertyAnimator);
      }
    }

    [BurstCompile]
    struct UpdateTime : IJobForEach<SpriteAnimationState> {
      public float DeltaTime;
      public void Execute([WriteOnly]ref SpriteAnimationState state) {
        state.Time += math.mul(DeltaTime, state.Speed);
      }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {
      if (m_lastOrderInfo != m_query.GetCombinedComponentOrderVersion()) {
        m_lastOrderInfo = m_query.GetCombinedComponentOrderVersion();
        m_spriteAnimations.Clear();
        var animations = new List<SpriteAnimation>();
        EntityManager.GetAllUniqueSharedComponentData(animations);
        foreach (var animation in animations) {
          m_query.SetSharedComponentFilter(animation);
          var length = m_query.CalculateEntityCount();
          if (length > 0 && animation.ClipSetEntity != Entity.Null)
            m_spriteAnimations.Add(animation);
        }
      }

      inputDeps = new UpdateTime {
        DeltaTime = Time.DeltaTime
      }.Schedule(this, inputDeps);

      foreach (var spriteAnimation in m_spriteAnimations) {
        m_query.SetSharedComponentFilter(spriteAnimation);
        foreach (var spriteAnimator in m_spriteAnimators)
          inputDeps = JobHandle.CombineDependencies(
            inputDeps,
            spriteAnimator.Update(spriteAnimation, inputDeps));
      }
      return inputDeps;
    }
  }
}