using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Stackray.Renderer {

  public interface ISpritePropertyAnimator {
    JobHandle Update(SpriteAnimation filter, JobHandle inputDeps);
  }

  public class SpritePropertyAnimator<TProperty, TData> : ISpritePropertyAnimator
      where TProperty : struct, IDynamicBufferProperty<TData>
      where TData : struct, IEquatable<TData> {

    JobComponentSystem m_system;
    EntityQuery m_query;

    public SpritePropertyAnimator(JobComponentSystem system, EntityQuery query) {
      m_system = system;
      m_query = query;
    }

    [BurstCompile]
    struct SpriteAnimationJobChunk : IJobChunk {
      public SpriteAnimation Filter;
      [ReadOnly]
      public ArchetypeChunkComponentType<SpriteAnimationState> SpriteAnimationStateType;
      public ArchetypeChunkComponentType<TProperty> PropertyType;
      [ReadOnly]
      public BufferFromEntity<SpriteAnimationClipBufferElement<TProperty, TData>> ClipSetFromEntity;
      public uint LastSystemVersion;

      public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
        if (!chunk.DidChange(SpriteAnimationStateType, LastSystemVersion))
          return;
        var states = chunk.GetNativeArray(SpriteAnimationStateType);
        var propertyComponents = chunk.GetNativeArray(PropertyType);
        if (propertyComponents.Length > 0) {
          ref var clipSet = ref ClipSetFromEntity[Filter.ClipSetEntity][Filter.ClipIndex].Value.Value;
          for (var i = 0; i < propertyComponents.Length; ++i)
            propertyComponents[i] = new TProperty {
              Value = clipSet.GetValue(states[i].Time)
            };  
        }
      }
    }

    public JobHandle Update(SpriteAnimation filter, JobHandle inputDeps) {
      var clipSetFromEntity = m_system.GetBufferFromEntity<SpriteAnimationClipBufferElement<TProperty, TData>>(true);
      if (clipSetFromEntity.Exists(filter.ClipSetEntity) && clipSetFromEntity[filter.ClipSetEntity][filter.ClipIndex].Value.IsCreated)
        inputDeps = new SpriteAnimationJobChunk {
          Filter = filter,
          SpriteAnimationStateType = m_system.GetArchetypeChunkComponentType<SpriteAnimationState>(true),
          PropertyType = m_system.GetArchetypeChunkComponentType<TProperty>(false),
          ClipSetFromEntity = clipSetFromEntity,
          LastSystemVersion = m_system.LastSystemVersion
        }.Schedule(m_query, inputDeps);
      return inputDeps;
    }
  }
}