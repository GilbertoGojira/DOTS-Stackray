using Unity.Entities;
using Unity.Mathematics;

namespace Stackray.Transforms {
  [UpdateInGroup(typeof(GameObjectAfterConversionGroup))]
  class RectTransformConversion : GameObjectConversionSystem {
    protected override void OnUpdate() {

      Entities.ForEach((UnityEngine.RectTransform transform) => {
        var entity = GetPrimaryEntity(transform);
        DstEntityManager.AddComponentData(entity, new Active { Value = transform.gameObject.activeSelf });
        DstEntityManager.AddComponentData(entity, new LocalRectTransform {
          Value = new AABB {
            Center = new float3(transform.rect.center, 0),
            Extents = new float3(transform.rect.size * 0.5f, 0)
          }
        });
      });
    }
  }
}