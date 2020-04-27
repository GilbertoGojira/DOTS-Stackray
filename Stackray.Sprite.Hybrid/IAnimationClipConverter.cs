using Unity.Entities;
using UnityEngine;

namespace Stackray.Sprite {
  public interface IAnimationClipConverter {
    void Convert(GameObject gameObject, EntityManager dstManager, Entity entity, AnimationClip[] Clips);
  }
}