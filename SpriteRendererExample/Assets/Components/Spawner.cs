using Unity.Entities;

public struct Spawner : IComponentData {
  public int CountX;
  public int CountY;
  public int CountZ;
  public Entity Prefab;
  public Entity Parent;
  public float HorizontalInterval;
  public float VerticalInterval;
  public float DepthInterval;
}