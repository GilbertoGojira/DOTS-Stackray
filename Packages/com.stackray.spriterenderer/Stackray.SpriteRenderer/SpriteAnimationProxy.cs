using Stackray.Entities;
using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Profiling;

namespace Stackray.SpriteRenderer {

  [RequiresEntityConversion]
  public class SpriteAnimationProxy : MonoBehaviour, IConvertGameObjectToEntity {
    public int ClipIndex;
    public float Speed = 1;
    public float StartTime;
    public AnimationClip[] Clips;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {

      var animationBufferEntity = dstManager.CreateEntity();
      var availableComponentTypes = TypeUtility.GetAvailableComponentTypes(typeof(IDynamicBufferProperty<>));
      foreach (var propertyType in availableComponentTypes) {
        var baseType = typeof(BufferGenerator<,>);
        var genericType0 = propertyType;
        var genericType1 = TypeUtility.ExtractInterfaceGenericType(propertyType, typeof(IComponentValue<>), 0);
        var instance = TypeUtility.CreateInstance(
                    baseType: baseType,
                    genericType0: genericType0,
                    genericType1: genericType1,
                    constructorArgs: Array.Empty<object>()) as IBufferGenerator;
        instance.Generate(gameObject, dstManager, animationBufferEntity, Clips);
      }

      dstManager.SetSharedComponentData(entity, new SpriteAnimation {
        ClipSetEntity = animationBufferEntity,
        ClipIndex = ClipIndex,
        ClipCount = Clips.Length
      });

      dstManager.AddComponentData(entity, new SpriteAnimationState {
        Speed = Speed,
        Time = StartTime
      });
    }
  }

  interface IBufferGenerator {
    void Generate(GameObject gameObject, EntityManager dstManager, Entity entity, AnimationClip[] Clips);
  }

  class BufferGenerator<TProperty, TData> : IBufferGenerator
      where TProperty : struct, IDynamicBufferProperty<TData>
      where TData : struct, IEquatable<TData> {

    public void Generate(GameObject gameObject, EntityManager dstManager, Entity entity, AnimationClip[] Clips) {
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
