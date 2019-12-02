using System.Collections.Generic;
using Stackray.Renderer;
using Unity.Entities;
using UnityEngine;

public class SelectAnimationSystem : ComponentSystem {

  Dictionary<Entity, SpriteAnimation> m_animationFilterMap = new Dictionary<Entity, SpriteAnimation>();
  protected override void OnUpdate() {
    var indexStep = 0;
    if (Input.GetKeyDown(KeyCode.UpArrow))
      indexStep = 1;
    if (Input.GetKeyDown(KeyCode.DownArrow))
      indexStep = -1;

    if (indexStep != 0) {
      Entities.ForEach((Entity entity, SpriteAnimation filter) => m_animationFilterMap.Add(entity, filter));
      foreach(var kvp in m_animationFilterMap) {
        var filter = kvp.Value;
        filter.ClipIndex = (int)Mathf.Repeat(filter.ClipIndex + indexStep, filter.ClipCount);
        EntityManager.SetSharedComponentData(kvp.Key, filter);
      }
      m_animationFilterMap.Clear();
    }

  }
}
