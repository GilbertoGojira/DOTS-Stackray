using Unity.Entities;
using UnityEngine;

public struct SpriteAnimationRandomizer : IComponentData {
  public bool RandomAnimation;
  public float RandomSpeedStart;
  public float RandomSpeedEnd;
}

// TODO: Wait for `GameObjectConversionMappingSystem` to handle this
// Right now GameObjectConversionMappingSystem calls DestroyImmediate and that will fail here
// [RequireComponent(typeof(SpriteAnimationProxy))]
public class SpriteAnimationRandomizerProxy : MonoBehaviour, IConvertGameObjectToEntity
{
  public bool RandomAnimation;
  public float RandomSpeedStart = 1;
  public float RandomSpeedEnd = 1;

  public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
    dstManager.AddComponentData(entity, new SpriteAnimationRandomizer {
      RandomAnimation = RandomAnimation,
      RandomSpeedStart = RandomSpeedStart,
      RandomSpeedEnd = RandomSpeedEnd
    });

  }
}
