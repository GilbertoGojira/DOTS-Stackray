using Stackray.Entities;
using Stackray.Jobs;
using Stackray.Collections;
using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace Stackray.SpriteRenderer {
  interface IBufferGroup : IDisposable {
    ComputeBuffer ComputeBuffer { get; }
    int InstanceCapacity { get; set; }
    bool IsChanged { get; }
    JobHandle Update(ComponentSystemBase system, EntityQuery query, int instanceOffset, JobHandle inputDeps = default);
    bool Push();
  }

  public abstract class BufferGroup<TSource, TTarget> : IBufferGroup
    where TSource : struct, IComponentData
    where TTarget : struct {

    private NativeQueue<VTuple<int, int>> m_changedSlices;
    private NativeQueue<VTuple<int, int>>.ParallelWriter m_changedSlicesParallelWriter;
    protected NativeList<TTarget> m_values;

    public ComputeBuffer ComputeBuffer {
      get;
      private set;
    }

    public bool IsChanged {
      get;
      private set;
    }

    public int InstanceCapacity {
      get;
      set;
    }

    public BufferGroup() {
      m_values = new NativeList<TTarget>(0, Allocator.Persistent);
      m_changedSlices = new NativeQueue<VTuple<int, int>>(Allocator.Persistent);
      m_changedSlicesParallelWriter = m_changedSlices.AsParallelWriter();
    }

    protected abstract JobHandle ExtractValues(ComponentSystemBase system, EntityQuery query, int instanceOffset, JobHandle inputDeps);

    private JobHandle GatherChangedValues(ComponentSystemBase system, EntityQuery query, int instanceOffset, JobHandle inputDeps) {
      inputDeps = query.GetChangedChunks<TSource>(system, Allocator.TempJob, ref m_changedSlicesParallelWriter, inputDeps, IsChanged, instanceOffset);
      return inputDeps;
    }

    public JobHandle Update(ComponentSystemBase system, EntityQuery query, int instanceOffset, JobHandle inputDeps = default) {
      if (instanceOffset == 0)
        IsChanged = false;
      if (InstanceCapacity != (ComputeBuffer?.count ?? -1) && InstanceCapacity > 0) {
        ComputeBuffer?.Release();
        ComputeBuffer = new ComputeBuffer(InstanceCapacity, Marshal.SizeOf<TTarget>());
        IsChanged = true;
      }
      inputDeps = m_values.Resize(InstanceCapacity, inputDeps);
      inputDeps = GatherChangedValues(system, query, instanceOffset, inputDeps);
      inputDeps = ExtractValues(system, query, instanceOffset, inputDeps);
      return inputDeps;
    }

    private readonly string ProfilerString = $"Update computeBuffer {typeof(TSource)}";

    public bool Push() {
      var pushed = false;
      if (m_changedSlices.Count > 0) {
        UnityEngine.Profiling.Profiler.BeginSample(ProfilerString);
        var valuesArray = m_values.AsArray();
        while (m_changedSlices.TryDequeue(out var slice))
          ComputeBuffer.SetData(valuesArray, slice.Item1, slice.Item1, slice.Item2);
        pushed = true;
        m_changedSlices.Clear(default).Complete();
        UnityEngine.Profiling.Profiler.EndSample();
      }
      return pushed;
    }

    public void Dispose() {
      ComputeBuffer?.Release();
      if (m_values.IsCreated)
        m_values.Dispose();
      m_changedSlices.Dispose();
    }
  }
}
