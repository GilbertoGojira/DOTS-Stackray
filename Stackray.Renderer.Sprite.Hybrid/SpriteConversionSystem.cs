﻿using Unity.Entities;
using Unity.Mathematics;

namespace Stackray.Renderer {
  public class SpriteConversionSystem : GameObjectConversionSystem {

    protected override void OnUpdate() {
      var sceneBounds = MinMaxAABB.Empty;

      Entities.ForEach((UnityEngine.SpriteRenderer spriteRenderer) =>
        ProcessSpriteRender(sceneBounds, spriteRenderer));

      using (var boundingVolume = DstEntityManager.CreateEntityQuery(typeof(SceneBoundingVolume))) {
        if (!boundingVolume.IsEmptyIgnoreFilter) {
          var bounds = boundingVolume.GetSingleton<SceneBoundingVolume>();
          bounds.Value.Encapsulate(sceneBounds);
          boundingVolume.SetSingleton(bounds);
        }
      }
    }

    private void ProcessSpriteRender(MinMaxAABB sceneBounds, UnityEngine.SpriteRenderer spriteRenderer) {
      var entity = GetPrimaryEntity(spriteRenderer);
      SpriteUtility.CreateSpriteComponent(DstEntityManager, entity, spriteRenderer);
      sceneBounds.Encapsulate(spriteRenderer.bounds.ToAABB());
    }
  }
}
