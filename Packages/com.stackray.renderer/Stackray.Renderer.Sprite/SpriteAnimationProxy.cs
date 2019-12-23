using Stackray.Entities;
using System;
using Unity.Entities;
using UnityEngine;

namespace Stackray.Renderer {

  [RequiresEntityConversion]
  public class SpriteAnimationProxy : MonoBehaviour, IConvertGameObjectToEntity {
    public int ClipIndex;
    public float Speed = 1;
    public float StartTime;
    public AnimationClip[] Clips;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {

      var animationBufferEntity = dstManager.CreateEntity();
#if UNITY_EDITOR
      dstManager.SetName(animationBufferEntity, $"{gameObject.name} - SpriteAnimationBuffer");
#endif
      var availableComponentTypes = TypeUtility.GetAvailableComponentTypes(typeof(IDynamicBufferProperty<>));
      foreach (var propertyType in availableComponentTypes) {
        var baseType = typeof(SpriteAnimationClipConverter<,>);
        var genericType0 = propertyType;
        var genericType1 = TypeUtility.ExtractInterfaceGenericType(propertyType, typeof(IComponentValue<>), 0);
        var instance = TypeUtility.CreateInstance(
                    baseType: baseType,
                    genericType0: genericType0,
                    genericType1: genericType1,
                    constructorArgs: Array.Empty<object>()) as IAnimationClipConverter;
        instance.Convert(gameObject, dstManager, animationBufferEntity, Clips);
      }

      dstManager.AddSharedComponentData(entity, new SpriteAnimation {
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
}
