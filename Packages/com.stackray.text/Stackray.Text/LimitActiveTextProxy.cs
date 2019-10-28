using Stackray.Transforms;
using Unity.Entities;
using UnityEngine;


namespace Stackray.Text {
  [RequiresEntityConversion]
  public class LimitActiveTextProxy : MonoBehaviour, IConvertGameObjectToEntity {
    public int Limit;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
      var limitSystem = World.Active.GetOrCreateSystem<LimitActiveComponentSystem<TextRenderer>>();
      if (!limitSystem.HasSingleton<LimitActiveComponentSystem<TextRenderer>.LimitActive<TextRenderer>>())
        dstManager.AddComponentData(
          entity,
          default(LimitActiveComponentSystem<TextRenderer>.LimitActive<TextRenderer>));
      limitSystem.Limit = Limit;
    }
  }
}
