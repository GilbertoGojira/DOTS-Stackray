using Stackray.Renderer;
using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Profiling;

namespace Stackray.Sprite {
  public class SpriteAnimationClipConverter<TProperty, TData> : IAnimationClipConverter
      where TProperty : struct, IDynamicBufferProperty<TData>
      where TData : struct, IEquatable<TData> {

    public SpriteAnimationClipConverter() { }

    public void Convert(GameObject gameObject, EntityManager dstManager, Entity entity, AnimationClip[] Clips) {
      var animationMaterials = new List<Material>();
      var tileOffsetAnimationBuffer = dstManager.AddBuffer<SpriteAnimationClipBufferElement<TProperty, TData>>(entity);
      for (var i = 0; i < Clips.Length; ++i) {
        Profiler.BeginSample("Create Clipset");
        var animationClipSet = SpriteUtility.CreateClipSet<TProperty, TData>(gameObject, Clips[i], out var material);
        Profiler.EndSample();
        tileOffsetAnimationBuffer.Add(animationClipSet);
        animationMaterials.Add(material);
      }
      if (!dstManager.HasComponent<SpriteAnimationClipMaterials>(entity))
        dstManager.AddSharedComponentData(entity, new SpriteAnimationClipMaterials {
          Value = animationMaterials
        });
#if UNITY_EDITOR
      else {
        var existingClipMaterials = dstManager.GetSharedComponentData<SpriteAnimationClipMaterials>(entity);
        var newClipMaterials = new SpriteAnimationClipMaterials { Value = animationMaterials };
        if (!existingClipMaterials.Equals(newClipMaterials))
          throw new ArgumentException($"Sprite animation materials don't match!");
      }
#endif
    }
  }
}