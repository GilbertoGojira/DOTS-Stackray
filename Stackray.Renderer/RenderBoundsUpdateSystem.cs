using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Stackray.Renderer {
  /// <summary>
  /// Updates WorldRenderBounds for anything that has LocalToWorld and RenderBounds (and ensures WorldRenderBounds exists)
  /// </summary>
  [UpdateInGroup(typeof(PresentationSystemGroup))]
  [UpdateAfter(typeof(CreateMissingRenderBoundsFromMeshRenderer))]
  [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.EntitySceneOptimizations)]
  [ExecuteAlways]
  public class RenderBoundsUpdateSystem : JobComponentSystem {
    EntityQuery m_MissingWorldRenderBounds;
    EntityQuery m_WorldRenderBounds;
    EntityQuery m_MissingWorldChunkRenderBounds;

    [BurstCompile]
    struct BoundsJob : IJobChunk {
      [ReadOnly] public ArchetypeChunkComponentType<RenderBounds> RendererBounds;
      [ReadOnly] public ArchetypeChunkComponentType<LocalToWorld> LocalToWorld;
      public ArchetypeChunkComponentType<WorldRenderBounds> WorldRenderBounds;
      public ArchetypeChunkComponentType<ChunkWorldRenderBounds> ChunkWorldRenderBounds;
      public uint LastSystemVersion;

      public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
        if (!chunk.DidChange(RendererBounds, LastSystemVersion) && !chunk.DidChange(LocalToWorld, LastSystemVersion))
          return;

        //@TODO: Delta change...
        var worldBounds = chunk.GetNativeArray(WorldRenderBounds);
        var localBounds = chunk.GetNativeArray(RendererBounds);
        var localToWorld = chunk.GetNativeArray(LocalToWorld);
        MinMaxAABB combined = MinMaxAABB.Empty;
        for (int i = 0; i != localBounds.Length; i++) {
          var transformed = AABB.Transform(localToWorld[i].Value, localBounds[i].Value);

          worldBounds[i] = new WorldRenderBounds { Value = transformed };
          combined.Encapsulate(transformed);
        }

        chunk.SetChunkComponentData(ChunkWorldRenderBounds, new ChunkWorldRenderBounds { Value = combined });
      }
    }

    protected override void OnCreate() {
      m_MissingWorldRenderBounds = GetEntityQuery
      (
          new EntityQueryDesc {
            All = new[] { ComponentType.ReadOnly<RenderBounds>(), ComponentType.ReadOnly<LocalToWorld>() },
            None = new[] { ComponentType.ReadOnly<WorldRenderBounds>(), ComponentType.ReadOnly<Frozen>() }
          }
      );

      m_MissingWorldChunkRenderBounds = GetEntityQuery
      (
          new EntityQueryDesc {
            All = new[] { ComponentType.ReadOnly<RenderBounds>(), ComponentType.ReadOnly<LocalToWorld>() },
            None = new[] { ComponentType.ChunkComponentReadOnly<ChunkWorldRenderBounds>(), ComponentType.ReadOnly<Frozen>() }
          }
      );

      m_WorldRenderBounds = GetEntityQuery
      (
          new EntityQueryDesc {
            All = new[] { ComponentType.ChunkComponent<ChunkWorldRenderBounds>(), ComponentType.ReadWrite<WorldRenderBounds>(), ComponentType.ReadOnly<RenderBounds>(), ComponentType.ReadOnly<LocalToWorld>() },
            None = new[] { ComponentType.ReadOnly<Frozen>() }
          }
      );

      var originalSystem = World.GetOrCreateSystem<Unity.Rendering.RenderBoundsUpdateSystem>();
      originalSystem.Enabled = false;
    }

    protected override JobHandle OnUpdate(JobHandle dependency) {
      EntityManager.AddComponent(m_MissingWorldRenderBounds, typeof(WorldRenderBounds));
      EntityManager.AddComponent(m_MissingWorldChunkRenderBounds, ComponentType.ChunkComponent<ChunkWorldRenderBounds>());

      var boundsJob = new BoundsJob {
        RendererBounds = GetArchetypeChunkComponentType<RenderBounds>(true),
        LocalToWorld = GetArchetypeChunkComponentType<LocalToWorld>(true),
        WorldRenderBounds = GetArchetypeChunkComponentType<WorldRenderBounds>(),
        ChunkWorldRenderBounds = GetArchetypeChunkComponentType<ChunkWorldRenderBounds>(),
        LastSystemVersion = LastSystemVersion
      };
      return boundsJob.Schedule(m_WorldRenderBounds, dependency);
    }
  }
}