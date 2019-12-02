using System;
using System.Collections.Generic;
using System.Linq;
using Stackray.Entities;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Stackray.Renderer {

  public struct SpriteAnimationState : IComponentData {
    public float Time;
    public float Speed;
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

  public struct SpriteAnimationClipBufferElement<TProperty, TData> : IBufferElementData
    where TProperty : IComponentValue<TData>
    where TData : struct, System.IEquatable<TData> {

    public BlobAssetReference<ClipSet<TProperty, TData>> Value;
  }

  public struct ClipSet<TProperty, TData>
    where TProperty : IComponentValue<TData>
    where TData : struct, System.IEquatable<TData> {

    public BlobArray<SpriteAnimationClip<TProperty, TData>> Value;

    public bool Loop;
    public float AnimationLength;

    public TData GetValue(float time) {
      var frame = (int)math.round(ComputeNormalizedTime(time) * (Value.Length - 1));
      return Value[frame].Value;
    }

    public float ComputeNormalizedTime(float time) {
      return Loop ?
        Mathf.Repeat(time, AnimationLength) / AnimationLength :
        math.saturate(time / AnimationLength);
    }
  }

  public struct SpriteAnimationClip<TProperty, TData>
    where TProperty : IComponentValue<TData>
    where TData : struct {

    public TData Value;
  }
}
