using Stackray.Entities;
using Stackray.Jobs;
using System;
using System.Collections.Generic;
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

    private List<NativeQueue<VTuple<int, int, int>>> m_changedSlices = new List<NativeQueue<VTuple<int, int, int>>>();
    private List<NativeArray<int>> m_indicesStates = new List<NativeArray<int>>();
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
    }

    protected abstract JobHandle ExtractValues(ComponentSystemBase system, EntityQuery query, int instanceOffset, JobHandle inputDeps);

    private JobHandle GatherChangedValues(ComponentSystemBase system, EntityQuery query, int instanceOffset, JobHandle inputDeps) {

      NativeQueue<VTuple<int, int, int>> changedSlices = default;
      NativeArray<int> indicesState;
      inputDeps = query.GetChangedChunks<TSource>(system, Allocator.TempJob, out indicesState, out changedSlices, inputDeps, IsChanged, instanceOffset);
      m_indicesStates.Add(indicesState);
      m_changedSlices.Add(changedSlices);
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
      inputDeps = new ResizeNativeList<TTarget> {
        Source = m_values,
        Length = InstanceCapacity
      }.Schedule(inputDeps);
      inputDeps = GatherChangedValues(system, query, instanceOffset, inputDeps);
      inputDeps = ExtractValues(system, query, instanceOffset, inputDeps);
      return inputDeps;
    }

    private readonly string ProfilerString = $"Update computeBuffer {typeof(TSource)}";

    public bool Push() {
      var pushed = false;
      foreach (var changedSlice in m_changedSlices) {
        if (changedSlice.Count > 0) {
          UnityEngine.Profiling.Profiler.BeginSample(ProfilerString);
          var valuesArray = m_values.AsArray();
          while (changedSlice.TryDequeue(out var slice)) {
            ComputeBuffer.SetData(valuesArray, slice.Item1, slice.Item2, slice.Item3);
          }
          pushed = true;
          UnityEngine.Profiling.Profiler.EndSample();
        }
        changedSlice.Dispose();
      }

      m_changedSlices.Clear();
      foreach(var indicesState in m_indicesStates)
        indicesState.Dispose();
      m_indicesStates.Clear();
      return pushed;
    }

    public void Dispose() {
      ComputeBuffer?.Release();
      if (m_values.IsCreated)
        m_values.Dispose();
      foreach (var indicesState in m_indicesStates)
        indicesState.Dispose();
      m_indicesStates.Clear();
      foreach (var changedSlice in m_changedSlices)
        changedSlice.Dispose();
      m_changedSlices.Clear();
    }
  }
}
