using Stackray.Collections;
using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace Stackray.Renderer {
  interface IBufferGroup : IDisposable {
    ComputeBuffer ComputeBuffer { get; }
    JobHandle Update(ComponentSystemBase system, EntityQuery query, int instanceCount, JobHandle inputDeps = default);
    bool Push();
  }

  public abstract class BufferGroup<TSource, TTarget> : IBufferGroup
    where TSource : struct, IComponentData
    where TTarget : struct {

    protected NativeUnit<bool> m_chunksHaveChanged;
    protected NativeList<TTarget> m_values;
    protected bool m_didChange;

    public ComputeBuffer ComputeBuffer {
      get;
      private set;
    }

    public BufferGroup() {
      m_values = new NativeList<TTarget>(0, Allocator.Persistent);
      m_chunksHaveChanged = new NativeUnit<bool>(Allocator.Persistent);
    }

    protected abstract JobHandle ExtractValues(ComponentSystemBase system, EntityQuery query, JobHandle inputDeps);

    public JobHandle Update(ComponentSystemBase system, EntityQuery query, int instanceCount, JobHandle inputDeps = default) {
      m_didChange = false;
      if (instanceCount != (ComputeBuffer?.count ?? -1) && instanceCount > 0) {
        ComputeBuffer?.Release();
        ComputeBuffer = new ComputeBuffer(instanceCount, Marshal.SizeOf<TTarget>(), ComputeBufferType.Default, ComputeBufferMode.SubUpdates);
        m_didChange = true;
      }
      inputDeps = m_values.Resize(instanceCount, inputDeps);
      inputDeps = ExtractValues(system, query, inputDeps);
      return inputDeps;
    }

    private readonly string ProfilerString = $"Update computeBuffer {typeof(TSource)}";

    public bool Push() {
      var pushed = false;
      if (m_chunksHaveChanged.Value) {
        UnityEngine.Profiling.Profiler.BeginSample(ProfilerString);
        var uploadData = ComputeBuffer.BeginWrite<TTarget>(0, m_values.Length);
        uploadData.CopyFrom(m_values);
        ComputeBuffer.EndWrite<TTarget>(m_values.Length);
        pushed = true;
        m_chunksHaveChanged.Value = false;
        UnityEngine.Profiling.Profiler.EndSample();
      }
      return pushed;
    }

    public void Dispose() {
      ComputeBuffer?.Release();
      if (m_values.IsCreated)
        m_values.Dispose();
      m_chunksHaveChanged.Dispose();
    }
  }
}
