using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Stackray.Renderer {
  public class InstanceColorProperty : MonoBehaviour, IConvertGameObjectToEntity {
    public Color Color;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
      dstManager.AddComponentData(entity, new ColorProperty {
        Value = (half4)new float4(Color.r, Color.g, Color.b, Color.a)
      });
    }
  }
}
