using Unity.Entities;

[GenerateAuthoringComponent]
public struct PrefabComponent : IComponentData {
  public Entity Prefab;
}
