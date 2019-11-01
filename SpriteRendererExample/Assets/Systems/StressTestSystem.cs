using Stackray.Text;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class StressTestTextSystem : JobComponentSystem {

  NativeArray<NativeString64> m_strings;
  BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;
  Random m_random;
  bool m_active;

  protected override void OnCreate() {
    base.OnCreate();
    m_random = new Random((uint)System.DateTime.Now.Millisecond);
    m_strings = new NativeArray<NativeString64>(
      Enumerable.Range(10, 1000).Select(v => new NativeString64($"{v}%")).ToArray(),
      Allocator.Persistent);
  }

  protected override void OnDestroy() {
    base.OnDestroy();
    m_strings.Dispose();
  }

  [BurstCompile]
  struct UpdateText : IJobForEachWithEntity_EC<TextData> {
    [ReadOnly]
    public NativeArray<NativeString64> Strings;
    public uint Seed;

    public void Execute(Entity entity, int index, [WriteOnly]ref TextData textData) {
      var random = new Random((uint)(Seed + index));
      var stringIndex = random.NextInt(100, Strings.Length - 1);
      textData.Value = Strings[stringIndex];
    }
  }

  protected override JobHandle OnUpdate(JobHandle inputDeps) {
    if (Input.GetKeyDown(KeyCode.S))
      m_active = !m_active;

    if (m_active)
      inputDeps = new UpdateText {
        Strings = m_strings,
        Seed = m_random.NextUInt()
      }.Schedule(this, inputDeps);
    return inputDeps;
  }
}
