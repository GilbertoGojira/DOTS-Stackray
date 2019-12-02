using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

[RequiresEntityConversion]
public class SpawnerProxy : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity {
  public GameObject Prefab;
  public GameObject Parent;
  public int CountX;
  public int CountY;
  public int CountZ;
  public float HorizontalInterval = 1.3F;
  public float VerticalInterval = 1.3F;
  public float DepthInterval = 1.3F;

  private void Awake() {
    if (Parent != null)
      Parent.transform.SetParent(transform, false);
  }

  // Referenced prefabs have to be declared so that the conversion system knows about them ahead of time
  public void DeclareReferencedPrefabs(List<GameObject> gameObjects) {
    gameObjects.Add(Prefab);
  }

  // Lets you convert the editor data representation to the entity optimal runtime representation
  public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
    var spawnerData = new Spawner {
      // The referenced prefab will be converted due to DeclareReferencedPrefabs.
      // So here we simply map the game object to an entity reference to that prefab.
      Prefab = conversionSystem.GetPrimaryEntity(Prefab),
      Parent = conversionSystem.GetPrimaryEntity(Parent),
      CountX = CountX,
      CountY = CountY,
      CountZ = CountZ,
      HorizontalInterval = HorizontalInterval,
      VerticalInterval = VerticalInterval,
      DepthInterval = DepthInterval
    };
    dstManager.AddComponentData(entity, spawnerData);
  }
}
