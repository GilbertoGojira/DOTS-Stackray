using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public struct PrefabComponent : IComponentData {
  public Entity Prefab;
}

[RequiresEntityConversion]
public class PrefabProxy : MonoBehaviour, IConvertGameObjectToEntity, IDeclareReferencedPrefabs
{
  public GameObject Prefab;

  public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
    dstManager.AddComponentData(entity, new PrefabComponent {
      Prefab = conversionSystem.GetPrimaryEntity(Prefab)
    });
  }

  public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs) {
    referencedPrefabs.Add(Prefab);
  }
}
