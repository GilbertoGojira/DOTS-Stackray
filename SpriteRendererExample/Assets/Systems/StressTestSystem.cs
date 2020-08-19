using Stackray.Text;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class StressTestTextSystem : SystemBase {

  NativeArray<FixedString64> m_strings;
  BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;
  Random m_random;
  bool m_active;

  protected override void OnCreate() {
    base.OnCreate();
    m_random = new Random((uint)System.DateTime.Now.Millisecond);
    m_strings = new NativeArray<FixedString64>(
      Enumerable.Range(10, 1000).Select(v => new FixedString64($"{v}%")).ToArray(),
      Allocator.Persistent);
  }

  protected override void OnDestroy() {
    base.OnDestroy();
    m_strings.Dispose();
  }

  protected override void OnUpdate() {
    if (Input.GetKeyDown(KeyCode.S))
      m_active = !m_active;
    if (m_active) {
      var seed = m_random.NextUInt();
      var strings = m_strings;
      Entities.ForEach((Entity entity, int entityInQueryIndex, ref TextData textData) => {
        var random = new Random((uint)(seed + entityInQueryIndex));
        var stringIndex = random.NextInt(100, strings.Length - 1);
        textData.Value = strings[stringIndex];
      }).ScheduleParallel();
    }
  }
}
