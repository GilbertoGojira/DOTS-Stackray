using Stackray.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Stackray.Transforms {
  public class RectTransformSystem : JobComponentSystem {

    EntityQuery m_missingWorldRectTransformsQuery;
    EntityQuery m_worldRectTransformQuery;
    EntityQuery m_missingWorldChunkRectTransformsQuery;

    protected override void OnCreate() {
      m_missingWorldRectTransformsQuery = GetEntityQuery(
          new EntityQueryDesc {
            All = new[] {
              ComponentType.ReadOnly<LocalRectTransform>(),
              ComponentType.ReadOnly<LocalToWorld>() },
            None = new[] {
              ComponentType.ReadOnly<WorldRectTransform>(),
              ComponentType.ReadOnly<Frozen>() }
          });

      m_missingWorldChunkRectTransformsQuery = GetEntityQuery(
          new EntityQueryDesc {
            All = new[] {
              ComponentType.ReadOnly<LocalRectTransform>(),
              ComponentType.ReadOnly<LocalToWorld>()
            },
            None = new[] {
              ComponentType.ChunkComponentReadOnly<ChunkWorldRectTransform>(),
              ComponentType.ReadOnly<Frozen>()
            }
          });

      m_worldRectTransformQuery = GetEntityQuery(
          new EntityQueryDesc {
            All = new[] {
              ComponentType.ChunkComponent<ChunkWorldRectTransform>(),
              ComponentType.ReadWrite<WorldRectTransform>(),
              ComponentType.ReadOnly<LocalRectTransform>(),
              ComponentType.ReadOnly<LocalToWorld>()
            },
            None = new[] { ComponentType.ReadOnly<Frozen>() }
          });
    }

    [BurstCompile]
    struct UpdateRectTransformJob : IJobChunk {
      [ReadOnly] public ArchetypeChunkComponentType<LocalRectTransform> RectTransformType;
      [ReadOnly] public ArchetypeChunkComponentType<LocalToWorld> LocalToWorldType;
      public ArchetypeChunkComponentType<WorldRectTransform> WorldRectTransformType;
      public ArchetypeChunkComponentType<ChunkWorldRectTransform> ChunkWorldRectTransformType;
      public uint LastSystemVersion;

      public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
        if (!chunk.DidChange(RectTransformType, LastSystemVersion) && !chunk.DidChange(LocalToWorldType, LastSystemVersion))
          return;

        var rectTransforms = chunk.GetNativeArray(RectTransformType);
        var localToWorld = chunk.GetNativeArray(LocalToWorldType);

        var worldRectTransforms = chunk.GetNativeArray(WorldRectTransformType);
        MinMaxAABB combined = MinMaxAABB.Empty;
        for (int i = 0; i != rectTransforms.Length; i++) {
          var transformed = AABB.Transform(localToWorld[i].Value, rectTransforms[i].Value);
          worldRectTransforms[i] = new WorldRectTransform { Value = transformed };
          combined.Encapsulate(transformed);
        }
        chunk.SetChunkComponentData(ChunkWorldRectTransformType, new ChunkWorldRectTransform { Value = combined });
      }
    }

    protected override JobHandle OnUpdate(JobHandle dependency) {
      EntityManager.AddComponent(m_missingWorldRectTransformsQuery, typeof(WorldRectTransform));
      EntityManager.AddComponent(m_missingWorldChunkRectTransformsQuery, ComponentType.ChunkComponent<ChunkWorldRectTransform>());

      var boundsJob = new UpdateRectTransformJob {
        RectTransformType = GetArchetypeChunkComponentType<LocalRectTransform>(true),
        LocalToWorldType = GetArchetypeChunkComponentType<LocalToWorld>(true),
        WorldRectTransformType = GetArchetypeChunkComponentType<WorldRectTransform>(false),
        ChunkWorldRectTransformType = GetArchetypeChunkComponentType<ChunkWorldRectTransform>(false),
        LastSystemVersion = LastSystemVersion
      };
      return boundsJob.Schedule(m_worldRectTransformQuery, dependency);
    }

    [DrawGizmos]
    void OnDrawGizmos() {
      Gizmos.color = Color.green;
      var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<WorldRectTransform>());
      using (var transforms = query.ToComponentDataArray<WorldRectTransform>(Allocator.TempJob)) {
        for (var i = 0; i < transforms.Length; ++i) {
          var b = transforms[i].Value;
          Gizmos.DrawWireCube(b.Center, b.Size);
        }
      }
    }
  }
}
