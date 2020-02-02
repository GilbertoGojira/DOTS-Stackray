using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;

namespace Stackray.Sprite {
  [UpdateInGroup(typeof(PresentationSystemGroup))]
  [UpdateBefore(typeof(RenderBoundsUpdateSystem))]
  public class SpriteBoundSystem : JobComponentSystem {
    [BurstCompile]
    struct UpdateBoundsFromPivot : IJobForEach<SpriteBounds, PivotProperty, RenderBounds> {
      public void Execute(
        [ReadOnly, ChangedFilter] ref SpriteBounds spriteBounds,
        [ReadOnly, ChangedFilter] ref PivotProperty pivot,
        [WriteOnly] ref RenderBounds renderBounds) {

        renderBounds.Value = new AABB {
          Center = (spriteBounds.Value.Center + new float3(pivot.Value, 0)),
          Extents = spriteBounds.Value.Extents
        };
      }
    }

    [BurstCompile]
    struct UpdateBoundsFromScale : IJobForEach<SpriteBounds, ScaleProperty, RenderBounds> {
      public void Execute(
        [ReadOnly, ChangedFilter] ref SpriteBounds spriteBounds,
        [ReadOnly, ChangedFilter] ref ScaleProperty scale,
        ref RenderBounds renderBounds) {

        renderBounds.Value = new AABB {
          Center = renderBounds.Value.Center * new float3(scale.Value.xyz),
          Extents = renderBounds.Value.Extents * new float3(scale.Value.xyz)
        };
      }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {
      inputDeps = new UpdateBoundsFromPivot()
        .Schedule(this, inputDeps);
      inputDeps = new UpdateBoundsFromScale()
        .Schedule(this, inputDeps);
      return inputDeps;
    }
  }
}
