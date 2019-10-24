using Stackray.Transforms;
using Unity.Entities;
using UnityEngine;


namespace Stackray.Text {
  [RequiresEntityConversion]
  public class LimitActiveTextProxy : MonoBehaviour, IConvertGameObjectToEntity {
    public int Limit;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
      dstManager.AddComponentData(
        entity,
        new LimitActiveComponentSystem<TextRenderer>.LimitActive<TextRenderer> { Value = Limit });
      World.Active.GetOrCreateSystem<LimitActiveComponentSystem<TextRenderer>>().Limit = Limit;
    }
  }
}
