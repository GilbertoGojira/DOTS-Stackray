using Stackray.Renderer;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;

namespace Stackray.Sprite {
  [UpdateInGroup(typeof(PresentationSystemGroup))]
  [UpdateBefore(typeof(RendererSystem))]
  public class SpriteBoundSystem : SystemBase {

    void UpdateBoundsFromPivot() {
      Entities
        .WithChangeFilter<SpriteBounds, PivotProperty>()
        .ForEach((ref RenderBounds renderBounds, in SpriteBounds spriteBounds, in PivotProperty pivot) => {
          renderBounds.Value = new AABB {
            Center = (spriteBounds.Value.Center + new float3(pivot.Value, 0)),
            Extents = spriteBounds.Value.Extents
          };
        }).ScheduleParallel();
    }

    void UpdateBoundsFromScale() {
      Entities
        .WithChangeFilter<SpriteBounds, ScaleProperty>()
        .ForEach((ref RenderBounds renderBounds, in SpriteBounds spriteBounds, in ScaleProperty scale) => {
          renderBounds.Value = new AABB {
            Center = renderBounds.Value.Center * new float3(scale.Value.xyz),
            Extents = renderBounds.Value.Extents * new float3(scale.Value.xyz)
          };
        }).ScheduleParallel();
    }

    protected override void OnUpdate() {
      UpdateBoundsFromPivot();
      UpdateBoundsFromScale();
    }
  }
}
