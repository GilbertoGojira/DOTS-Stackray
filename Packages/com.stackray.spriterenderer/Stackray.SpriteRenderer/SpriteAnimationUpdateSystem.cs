using System.Collections.Generic;
using Unity.Entities;

namespace Stackray.SpriteRenderer {

  public class SpriteAnimationUpdateSystem : ComponentSystem {
    EntityQuery m_query;
    int m_lastOrderVersion;
    Dictionary<Entity, SpriteRenderMesh> m_spriteMeshesMap = new Dictionary<Entity, SpriteRenderMesh>();

    protected override void OnCreate() {
      base.OnCreate();
      m_query = GetEntityQuery(typeof(SpriteAnimation));
    }

    protected override void OnUpdate() {
      var orderVersion = m_query.GetCombinedComponentOrderVersion();
      if (orderVersion == m_lastOrderVersion)
        return;
      m_lastOrderVersion = orderVersion;
      using (var chunks = m_query.CreateArchetypeChunkArray(Unity.Collections.Allocator.TempJob)) {
        foreach (var chunk in chunks) {
          var animationFilter = chunk.GetSharedComponentData(GetArchetypeChunkSharedComponentType<SpriteAnimation>(), EntityManager);
          var entities = chunk.GetNativeArray(GetArchetypeChunkEntityType());
          var materials = EntityManager.GetSharedComponentData<SpriteAnimationClipMaterials>(animationFilter.ClipSetEntity).Value;
          foreach (var entity in entities) {
            var mesh = EntityManager.GetSharedComponentData<SpriteRenderMesh>(entity).Mesh;
            m_spriteMeshesMap.Add(entity, new SpriteRenderMesh {
              Mesh = mesh,
              Material = materials[animationFilter.ClipIndex]
            });
          }
        }
      }
      foreach (var kvp in m_spriteMeshesMap)
        EntityManager.SetSharedComponentData(kvp.Key, kvp.Value);
      m_spriteMeshesMap.Clear();      
    }
  }
}
