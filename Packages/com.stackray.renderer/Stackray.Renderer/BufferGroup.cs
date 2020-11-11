using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace Stackray.Renderer {
  interface IBufferGroup : IDisposable {
    ComputeBuffer ComputeBuffer { get; }
    JobHandle Update(ComponentSystemBase system, EntityQuery query, JobHandle inputDeps = default);
    void BeginWrite(int instanceCount);
    void EndWrite();
  }

  public abstract class BufferGroup<TSource, TTarget> : IBufferGroup
    where TSource : struct, IComponentData
    where TTarget : struct {

    private readonly string ProfilerString = $"Update computeBuffer {typeof(TSource)}";
    protected NativeArray<TTarget> m_values;
    protected bool m_changed;

    public ComputeBuffer ComputeBuffer {
      get;
      private set;
    }

    public abstract JobHandle Update(ComponentSystemBase system, EntityQuery query, JobHandle inputDeps);

    public void BeginWrite(int instanceCount) {
      m_changed = false;
      if (instanceCount != (ComputeBuffer?.count ?? -1)) {
        ComputeBuffer?.Release();
        ComputeBuffer = null;
        if (instanceCount > 0) {
          ComputeBuffer = new ComputeBuffer(instanceCount, Marshal.SizeOf<TTarget>(), ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
          m_changed = true;
        }
      }
      m_values = ComputeBuffer?.BeginWrite<TTarget>(0, instanceCount) ?? default;
    }

    public void EndWrite() {
       ComputeBuffer?.EndWrite<TTarget>(m_values.Length);
    }

    public void Dispose() {
      ComputeBuffer?.Release();
    }
  }
}
