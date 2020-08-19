using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace Stackray.Sprite {

  public class SpriteConversionSystem : GameObjectConversionSystem {

    protected override void OnStartRunning() {
      base.OnStartRunning();
      // We need to override the default sprite conversion system
      World.GetOrCreateSystem<SpriteRendererConversionSystem>().Enabled = false;
    }

    protected override void OnUpdate() {
      var sceneBounds = MinMaxAABB.Empty;

      Entities.ForEach((UnityEngine.SpriteRenderer spriteRenderer) =>
        ProcessSpriteRender(sceneBounds, spriteRenderer));
/*    TODO: Fix this or check if it is still required
      using (var boundingVolume = DstEntityManager.CreateEntityQuery(typeof(SceneBoundingVolume))) {
        if (!boundingVolume.IsEmptyIgnoreFilter) {
          var bounds = boundingVolume.GetSingleton<SceneBoundingVolume>();
          bounds.Value.Encapsulate(sceneBounds);
          boundingVolume.SetSingleton(bounds);
        }
      }*/
    }

    private void ProcessSpriteRender(MinMaxAABB sceneBounds, UnityEngine.SpriteRenderer spriteRenderer) {
      var entity = GetPrimaryEntity(spriteRenderer);
      SpriteUtility.CreateSpriteComponent(DstEntityManager, entity, spriteRenderer);
      sceneBounds.Encapsulate(spriteRenderer.bounds.ToAABB());
    }
  }
}
