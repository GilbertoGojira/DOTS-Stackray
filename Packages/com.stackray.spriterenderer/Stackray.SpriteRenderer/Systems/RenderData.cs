using Stackray.Collections;
using Stackray.Entities;
using Stackray.Jobs;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Profiling;

namespace Stackray.SpriteRenderer {
  class RenderData<TFilter> : IDisposable where TFilter : struct, ISharedComponentData {
    private Dictionary<string, IBufferGroup> m_buffers = new Dictionary<string, IBufferGroup>();
    private List<JobHandle> m_jobs = new List<JobHandle>();

    public ComputeBuffer ArgsBuffer {
      get;
      private set;
    }

    public Mesh Mesh {
      get => m_spriteRenderMesh.Mesh;
    }

    public Material Material {
      get => m_spriteRenderMesh.Material;
    }

    public Bounds Bounds {
      get;
      private set;
    }

    private int m_submeshIndex;
    public int SubmeshIndex {
      get => m_submeshIndex;
      private set {
        m_submeshIndex = value;
        m_args[0] = Mesh.GetIndexCount(value);
        m_args[2] = Mesh.GetIndexStart(value);
        m_args[3] = Mesh.GetBaseVertex(value);

      }
    }

    private List<(TFilter, int)> m_extraFilterCache = new List<(TFilter, int)>();

    public List<TFilter> ExtraFilter {
      set {
        m_extraFilterCache.Clear();
        foreach (var filter in value) {
          SetFilter(filter);
          var length = Query.CalculateEntityCount();
          if (length > 0)
            m_extraFilterCache.Add((filter, length));
        }
      }
    }

    public int SharedComponentIndex;

    public uint InstanceCount {
      get => m_args[1];
      private set => m_args[1] = value;
    }

    public EntityQuery Query {
      get => m_system.EntityQueries[0];
    }

    ComponentSystemBase m_system;
    SpriteRenderMesh m_spriteRenderMesh;
    NativeUnit<AABB> m_chunkWorldRenderBounds;
    uint[] m_args;
    Dictionary<Type, string> m_fixedBuffersInfo;
    Dictionary<Type, string> m_dynamicBuffersInfo;

    public RenderData(
      ComponentSystemBase system,
      SpriteRenderMesh spriteRenderMesh,
      int sharedComponentIndex,
      Dictionary<Type, string> fixedBuffers,
      Dictionary<Type, string> dynamicBuffers,
      List<TFilter> extraFilter) {

      m_system = system;
      m_spriteRenderMesh = spriteRenderMesh;
      SharedComponentIndex = sharedComponentIndex;
      ExtraFilter = extraFilter;
      m_args = new uint[5] { 0, 0, 0, 0, 0 };
      ArgsBuffer = new ComputeBuffer(1, m_args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
      m_fixedBuffersInfo = fixedBuffers;
      m_dynamicBuffersInfo = dynamicBuffers;
    }

    public bool Update() {
      SetFilter();
      var boundsInputDeps = UpdateBounds();
      var instanceCapacity = Query.CalculateEntityCount();
      var instanceOffset = 0;
      var bufferJobHandle = default(JobHandle);
      Profiler.BeginSample("Traverse Extra Filter");
      foreach (var filter in m_extraFilterCache) {
        SetFilter(filter.Item1);
        var instanceCount = filter.Item2;
        Profiler.BeginSample("Traverse fixed buffer Extra Filter");
        foreach (var kvp in m_fixedBuffersInfo)
          m_jobs.Add(UpdateBuffer(kvp.Key, kvp.Value, instanceCount, instanceOffset, instanceCapacity, bufferJobHandle));
        Profiler.EndSample();
        Profiler.BeginSample("Traverse dynamic buffer Extra Filter");
        foreach (var kvp in m_dynamicBuffersInfo)
          m_jobs.Add(UpdateBuffer(kvp.Key, kvp.Value, instanceCount, instanceOffset, instanceCapacity, bufferJobHandle));
        Profiler.EndSample();
        Profiler.BeginSample("Extra Filter combine jobs");
        bufferJobHandle = JobUtility.CombineDependencies(m_jobs);
        m_jobs.Clear();
        Profiler.EndSample();
        instanceOffset += instanceCount;
      }
      Profiler.EndSample();
      bufferJobHandle = JobHandle.CombineDependencies(boundsInputDeps, bufferJobHandle);
      Complete(bufferJobHandle);
      UpdateArgs(instanceOffset);
      return InstanceCount > 0;
    }

    private void SetFilter() {
      Query.SetFilter(
        new SpriteRenderMesh {
          Mesh = Mesh,
          Material = Material
        });
    }

    private void SetFilter(TFilter filter) {
      Query.SetFilter(
        filter,
        new SpriteRenderMesh {
        Mesh = Mesh,
        Material = Material
      });
    }

    private void UpdateArgs(int instanceCount, int submeshIndex = 0) {
      InstanceCount = (uint)instanceCount;
      SubmeshIndex = submeshIndex;
      ArgsBuffer.SetData(m_args);
    }

    private JobHandle UpdateBuffer(Type bufferType, string bufferName, int instanceCount, int instanceOffset, int instanceCapacity, JobHandle inputDeps = default) {
      if (instanceCount == 0)
        return inputDeps;

      if (!m_buffers.ContainsKey(bufferName))
        m_buffers[bufferName] = CreateBufferGroup(bufferType);
      m_buffers[bufferName].InstanceCapacity = instanceCapacity;
      inputDeps = m_buffers[bufferName].Update(m_system, Query, instanceOffset, inputDeps);
      return inputDeps;
    }

    private void Complete(JobHandle inputDeps) {
      inputDeps.Complete();

      foreach (var kvp in m_buffers) {
        Profiler.BeginSample($"Buffer Push");
        kvp.Value.Push();
        Profiler.EndSample();
        Profiler.BeginSample($"Material SetBuffer");
        Material.SetBuffer(kvp.Key, kvp.Value.ComputeBuffer);
        Profiler.EndSample();
      }

      Bounds = new Bounds(Vector3.zero,
        new float3(math.length(m_chunkWorldRenderBounds.Value.Center + m_chunkWorldRenderBounds.Value.Extents)));
    }

    public void Dispose() {
      ArgsBuffer?.Release();
      ArgsBuffer = null;
      foreach (var buffer in m_buffers.Values)
        buffer?.Dispose();
      m_buffers.Clear();
      if (m_chunkWorldRenderBounds.IsCreated)
        m_chunkWorldRenderBounds.Dispose();
    }

    IBufferGroup CreateBufferGroup(Type type) {
      var propertyInterface = type.GetInterfaces().FirstOrDefault(
        t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IDynamicBufferProperty<>));
      var isBufferProperty = propertyInterface != null;
      var isDynamicBuffer = propertyInterface?.GetGenericArguments().Length > 0;
      var isComponentData = type.ImplementsInterface(typeof(IComponentData));

      if (!isBufferProperty && !isComponentData)
        throw new ArgumentException($"'{type}' does not implement interface '{typeof(IDynamicBufferProperty<>)}'");
      if (!isComponentData)
        throw new ArgumentException($"'{type}' does not implement interface '{typeof(IComponentData)}'");

      if (isDynamicBuffer) {
        var dataType = propertyInterface.GetGenericArguments()[0];
        var constructor = typeof(ComponentDataValueBufferGroup<,>)
          .MakeGenericType(type, dataType)
          .GetConstructor(Array.Empty<Type>());
        Debug.Assert(constructor != null, nameof(constructor) + " != null");
        return constructor.Invoke(Array.Empty<object>()) as IBufferGroup;
      }
      if (type == typeof(LocalToWorld))
        return new LocalToWorldBufferGroup();
      else
        throw new ArgumentException($"ComponentData '{type}' does not have a valid buffer! Please integrate a buffer for this ComponentData.");
    }

    #region extract renderData bounds
    public JobHandle UpdateBounds(JobHandle inputDeps = default) {
      if (m_chunkWorldRenderBounds.IsCreated)
        m_chunkWorldRenderBounds.Dispose();
      m_chunkWorldRenderBounds = new NativeUnit<AABB>(Allocator.TempJob);
      var chunks = Query.CreateArchetypeChunkArray(Allocator.TempJob, out var createChunksHandle);
      inputDeps = JobHandle.CombineDependencies(inputDeps, createChunksHandle);
      inputDeps = new ExtractChunkWorldRenderBounds<SpriteRenderMesh> {
        ChunkWorldBounds = m_chunkWorldRenderBounds,
        ChunkWorldRenderBoundsType = m_system.GetArchetypeChunkComponentType<ChunkWorldRenderBounds>(true),
        FilterType = m_system.GetArchetypeChunkSharedComponentType<SpriteRenderMesh>(),
        SharedComponentIndex = SharedComponentIndex,
        Chunks = chunks
      }.Schedule(inputDeps);
      return inputDeps;
    }

    [BurstCompile]
    struct ExtractChunkWorldRenderBounds<T> : IJob where T : struct, ISharedComponentData {
      [ReadOnly]
      [DeallocateOnJobCompletion]
      public NativeArray<ArchetypeChunk> Chunks;
      [ReadOnly]
      public ArchetypeChunkComponentType<ChunkWorldRenderBounds> ChunkWorldRenderBoundsType;
      [ReadOnly]
      public ArchetypeChunkSharedComponentType<T> FilterType;
      public int SharedComponentIndex;
      public NativeUnit<AABB> ChunkWorldBounds;

      public void Execute() {
        for (var i = 0; i < Chunks.Length; ++i) {
          if (SharedComponentIndex == Chunks[i].GetSharedComponentIndex(FilterType)) {
            MinMaxAABB bounds = ChunkWorldBounds.Value;
            bounds.Encapsulate(Chunks[i].GetChunkComponentData(ChunkWorldRenderBoundsType).Value);
            ChunkWorldBounds.Value = bounds;
          }
        }
      }
    }
    #endregion extract renderData Bounds
  }
}
