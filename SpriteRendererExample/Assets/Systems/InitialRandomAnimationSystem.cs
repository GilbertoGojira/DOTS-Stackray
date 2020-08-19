using Stackray.Sprite;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

public class InitialRandomAnimationSystem : ComponentSystem {

  EntityQuery m_query;  

  protected override void OnCreate() {
    base.OnCreate();
    m_query = GetEntityQuery(
      ComponentType.ReadWrite<SpriteAnimation>(),
      ComponentType.ReadOnly<SpriteAnimationTimeSpeedState>(),
      ComponentType.ReadOnly<SpriteAnimationRandomizer>());
  }

  [BurstCompile]
  unsafe struct RandomizeJob : IJobChunk {
    [ReadOnly]
    public SharedComponentTypeHandle<SpriteAnimation> SpriteAnimationChunkType;
    [ReadOnly]
    public EntityTypeHandle EntityChunkType;
    public ComponentTypeHandle<SpriteAnimationTimeSpeedState> SpriteAnimationStateChunkType;
    [ReadOnly]
    public ComponentTypeHandle<SpriteAnimationRandomizer> SpriteAnimationRandomizerChunkType;
    [ReadOnly]
    public NativeHashMap<int, SpriteAnimation> UniqueSpriteAnimations;
    [WriteOnly]
    public NativeHashMap<Entity, SpriteAnimation>.ParallelWriter SpriteAnimationMap;
    public uint RandomSeed;
    public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex) {
      var random = new Unity.Mathematics.Random(RandomSeed);
      var index = chunk.GetSharedComponentIndex(SpriteAnimationChunkType);
      var spriteAnimation = UniqueSpriteAnimations[index];
      var entities = chunk.GetNativeArray(EntityChunkType);
      var states = chunk.GetNativeArray(SpriteAnimationStateChunkType).GetUnsafePtr();
      var randomizers = chunk.GetNativeArray(SpriteAnimationRandomizerChunkType);
      for(var i = 0; i < chunk.Count; ++i) {
        spriteAnimation.ClipIndex = random.NextInt(0, spriteAnimation.ClipCount);
        SpriteAnimationMap.TryAdd(entities[i], spriteAnimation);
        UnsafeUtility.ArrayElementAsRef<SpriteAnimationTimeSpeedState>(states, i).Speed =
          random.NextFloat(randomizers[i].RandomSpeedStart, randomizers[i].RandomSpeedEnd);
      }
    }
  }

  protected override void OnUpdate() {
    
    UnityEngine.Profiling.Profiler.BeginSample("Gather Shared data");
     var animations = new List<SpriteAnimation>();
    var indices = new List<int>();
    EntityManager.GetAllUniqueSharedComponentData(animations, indices);
    var uniqueSpriteAnimations = new NativeHashMap<int, SpriteAnimation>(indices.Count, Allocator.TempJob);
    for (var i = 0; i < indices.Count; ++i)
      uniqueSpriteAnimations.TryAdd(indices[i], animations[i]);
    var spriteAnimationMap = new NativeHashMap<Entity, SpriteAnimation>(m_query.CalculateEntityCount(), Allocator.TempJob);
    UnityEngine.Profiling.Profiler.EndSample();
    var inputDeps = new RandomizeJob {
      EntityChunkType = GetEntityTypeHandle(),
      SpriteAnimationChunkType = GetSharedComponentTypeHandle<SpriteAnimation>(),
      SpriteAnimationStateChunkType = GetComponentTypeHandle<SpriteAnimationTimeSpeedState>(),
      SpriteAnimationRandomizerChunkType = GetComponentTypeHandle<SpriteAnimationRandomizer>(true),
      SpriteAnimationMap = spriteAnimationMap.AsParallelWriter(),
      UniqueSpriteAnimations = uniqueSpriteAnimations,
      RandomSeed = (uint)System.DateTime.Now.Second
    }.Schedule(m_query);
    inputDeps.Complete();
    uniqueSpriteAnimations.Dispose();

    UnityEngine.Profiling.Profiler.BeginSample("Process Shared data");
    foreach (var key in spriteAnimationMap.GetKeyArray(Allocator.Temp)) {
      var spriteAnimation = spriteAnimationMap[key];
      EntityManager.SetSharedComponentData(key, spriteAnimation);
      PostUpdateCommands.RemoveComponent<SpriteAnimationRandomizer>(key);
    }
    spriteAnimationMap.Dispose();
    UnityEngine.Profiling.Profiler.EndSample();
  }
}
