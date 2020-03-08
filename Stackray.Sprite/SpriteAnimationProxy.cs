using Stackray.Entities;
using Stackray.Renderer;
using System;
using Unity.Entities;
using UnityEngine;

namespace Stackray.Sprite {

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
      foreach(var converter in SpritePropertyAnimatorUtility.CreateConverters())
        converter.Convert(gameObject, dstManager, animationBufferEntity, Clips);

      dstManager.AddSharedComponentData(entity, new SpriteAnimation {
        ClipSetEntity = animationBufferEntity,
        ClipIndex = ClipIndex,
        ClipCount = Clips.Length
      });

      dstManager.AddComponentData(entity, new SpriteAnimationTimeSpeedState {
        Speed = Speed,
        Time = StartTime
      });
    }
  }
}
