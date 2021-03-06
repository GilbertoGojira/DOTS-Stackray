﻿using Stackray.Renderer;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Scripting;

namespace Stackray.Sprite {

  public interface ISpritePropertyAnimator {
    JobHandle Update(SpriteAnimation filter, JobHandle inputDeps);
  }

  public class SpritePropertyAnimator<TProperty, TData> : ISpritePropertyAnimator
      where TProperty : struct, IDynamicBufferProperty<TData>
      where TData : struct, IEquatable<TData> {

    SystemBase m_system;
    EntityQuery m_query;

    private SpritePropertyAnimator() { }

    [Preserve]
    public SpritePropertyAnimator(SystemBase system, EntityQuery query) {
      m_system = system;
      m_query = query;
    }

    [BurstCompile]
    struct SpriteAnimationJobChunk : IJobChunk {
      public SpriteAnimation Filter;
      [ReadOnly]
      public ComponentTypeHandle<SpriteAnimationTimeSpeedState> SpriteAnimationTimeSpeedStateType;
      public ComponentTypeHandle<SpriteAnimationPlayingState> SpriteAnimationPlayingStateType;
      public ComponentTypeHandle<TProperty> PropertyType;
      [ReadOnly]
      public BufferFromEntity<SpriteAnimationClipBufferElement<TProperty, TData>> ClipSetFromEntity;
      public uint LastSystemVersion;

      public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
        if (!chunk.DidChange(SpriteAnimationTimeSpeedStateType, LastSystemVersion))
          return;
        var states = chunk.GetNativeArray(SpriteAnimationTimeSpeedStateType);
        var playingStates = chunk.GetNativeArray(SpriteAnimationPlayingStateType);
        var propertyComponents = chunk.GetNativeArray(PropertyType);
        if (propertyComponents.Length > 0) {
          var clipSet = ClipSetFromEntity[Filter.ClipSetEntity][Filter.ClipIndex];
          for (var i = 0; i < propertyComponents.Length; ++i) {
            var isPlaying = clipSet.Loop || clipSet.ComputeNormalizedTime(states[i].Time) > clipSet.ComputeNormalizedTime(states[i].PrevioutTime);
            propertyComponents[i] = new TProperty {
              Value = clipSet.GetValue(states[i].Time)
            };
            if (isPlaying)
              playingStates[i] = new SpriteAnimationPlayingState { Value = true };
          }
        }
      }
    }

    public JobHandle Update(SpriteAnimation filter, JobHandle inputDeps) {
      var clipSetFromEntity = m_system.GetBufferFromEntity<SpriteAnimationClipBufferElement<TProperty, TData>>(true);
      if (clipSetFromEntity.HasComponent(filter.ClipSetEntity) && clipSetFromEntity[filter.ClipSetEntity][filter.ClipIndex].Value.IsCreated)
        inputDeps = new SpriteAnimationJobChunk {
          Filter = filter,
          SpriteAnimationTimeSpeedStateType = m_system.GetComponentTypeHandle<SpriteAnimationTimeSpeedState>(true),
          SpriteAnimationPlayingStateType = m_system.GetComponentTypeHandle<SpriteAnimationPlayingState>(false),
          PropertyType = m_system.GetComponentTypeHandle<TProperty>(false),
          ClipSetFromEntity = clipSetFromEntity,
          LastSystemVersion = m_system.LastSystemVersion
        }.Schedule(m_query, inputDeps);
      return inputDeps;
    }
  }
}