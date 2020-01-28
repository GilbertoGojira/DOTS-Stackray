using Unity.Entities;
using UnityEngine;

namespace Stackray.Renderer {
  public interface IAnimationClipConverter {
    void Convert(GameObject gameObject, EntityManager dstManager, Entity entity, AnimationClip[] Clips);
  }
}