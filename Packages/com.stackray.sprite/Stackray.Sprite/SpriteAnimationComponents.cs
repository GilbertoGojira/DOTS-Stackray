using System;
using System.Collections.Generic;
using System.Linq;
using Stackray.Entities;
using Stackray.Renderer;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Stackray.Sprite {

  public struct SpriteAnimationTimeSpeedState : IComponentData {

    public float PrevioutTime {
      private set;
      get;
    }

    float m_time;
    public float Time {
      set {
        PrevioutTime = m_time;
        m_time = value;
      }
      get => m_time;
    }
    public float Speed;
  }

  public struct SpriteAnimationPlayingState : IComponentData {
    public bool Value;
  }

  public struct SpriteAnimation : ISharedComponentData {
    public Entity ClipSetEntity;
    public int ClipIndex;
    public int ClipCount;
  }

  public struct SpriteAnimationClipMaterials : ISharedComponentData, IEquatable<SpriteAnimationClipMaterials> {
    public List<Material> Value;

    public bool Equals(SpriteAnimationClipMaterials other) {
      return Value?.SequenceEqual(other.Value) ?? false;
    }

    public override int GetHashCode() {
      var hashcode = Value?.Count ?? 0;
      if (Value != null)
        for (int i = 0; i < Value.Count; ++i)
          hashcode = unchecked(hashcode * 17 + Value[i].GetHashCode());
      return hashcode;
    }
  }

  public struct SpriteAnimationClipBufferElement<TProperty, TData> : IBufferElementData, IEquatable<SpriteAnimationClipBufferElement<TProperty, TData>>
    where TProperty : IComponentValue<TData>, IBlendable<TData>
    where TData : struct, IEquatable<TData> {

    public NativeString32 ClipName;
    public BlobAssetReference<ClipSet<TProperty, TData>> Value;

    public bool Equals(SpriteAnimationClipBufferElement<TProperty, TData> other) {
      return ClipName.Equals(other.ClipName) &&
        Value.Equals(other.Value);
    }

    public override int GetHashCode() {
      int hash = 0;
      hash ^= ClipName.GetHashCode();
      hash ^= Value.GetHashCode();
      return hash;
    }
  }

  public struct ClipSet<TProperty, TData>
    where TProperty : IComponentValue<TData>, IBlendable<TData>
    where TData : struct, IEquatable<TData> {

    public BlobArray<SpriteAnimationClip<TProperty, TData>> Value;

    public bool Loop;
    public float AnimationLength;

    public TData GetValue(float time) {
      var lastFrame = Value.Length - 1;
      var elapsedAnimationTime = ComputeElapsedAnimationTime(time);
      var frame = (int)math.floor(elapsedAnimationTime * lastFrame / AnimationLength);
      var nextFrame = (int)Mathf.Repeat(frame + 1, lastFrame);
      var frameTime = frame * AnimationLength / lastFrame;
      var nextFrameTime = nextFrame * AnimationLength / lastFrame;
      var frameElapsed = elapsedAnimationTime - frameTime;
      var dt = nextFrame > frame ? nextFrameTime - frameTime : AnimationLength - (frameTime - nextFrameTime);
      return default(TProperty).GetBlendedValue(Value[frame].Value, Value[nextFrame].Value, frameElapsed / dt);
    }

    public float ComputeNormalizedTime(float time) {
      return Loop ?
        ComputeElapsedAnimationTime(time) / AnimationLength :
        math.saturate(time / AnimationLength);
    }

    public float ComputeElapsedAnimationTime(float time) {
      return Loop ?
        Mathf.Repeat(time, AnimationLength) :
        math.saturate(time / AnimationLength) * AnimationLength;
    }
  }

  public struct SpriteAnimationClip<TProperty, TData>
    where TProperty : IComponentValue<TData>, IBlendable<TData>
    where TData : struct {

    public TData Value;
  }
}
