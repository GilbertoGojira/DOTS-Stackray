using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Stackray.Entities {

  public struct ChunkHashcode<T> : IComponentData
    where T : struct, IComponentData {

    public int Value;
    public bool Changed;
  }

  public abstract class HashChunkSystem<T> : JobComponentSystem
    where T : struct, IComponentData {

    EntityQuery m_missingChunkHashcode;
    EntityQuery m_query;

    protected override void OnCreate() {
      base.OnCreate();
      m_query = GetEntityQuery(ComponentType.ReadOnly<T>(), ComponentType.ChunkComponent<ChunkHashcode<T>>());
      m_missingChunkHashcode = GetEntityQuery(new EntityQueryDesc {
        All = new[] { ComponentType.ReadOnly<T>() },
        None = new[] { ComponentType.ChunkComponentReadOnly<ChunkHashcode<T>>() }
      });
    }

    [BurstCompile]
    public unsafe struct WriteHashPerChunk : IJobChunk {
      [ReadOnly]
      public ArchetypeChunkComponentType<T> ChunkType;
      public ArchetypeChunkComponentType<ChunkHashcode<T>> ChunkHashcodeType;
      public uint LastSystemVersion;
      public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
        if (!chunk.DidChange(ChunkType, LastSystemVersion))
          return;
        var components = chunk.GetNativeArray(ChunkType).GetUnsafeReadOnlyPtr();
        var hash = (int)math.hash(components, chunk.Count * UnsafeUtility.SizeOf<T>());
        var oldHash = chunk.GetChunkComponentData(ChunkHashcodeType);
        chunk.SetChunkComponentData(ChunkHashcodeType, new ChunkHashcode<T> {
          Value = hash,
          Changed = oldHash.Value != hash
        });
      }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {
      EntityManager.AddComponent(m_missingChunkHashcode, ComponentType.ChunkComponent<ChunkHashcode<T>>());
      inputDeps = new WriteHashPerChunk {
        ChunkType = GetArchetypeChunkComponentType<T>(true),
        ChunkHashcodeType = GetArchetypeChunkComponentType<ChunkHashcode<T>>(false),
        LastSystemVersion = LastSystemVersion
      }.Schedule(m_query, inputDeps);
      return inputDeps;
    }
  }
}
