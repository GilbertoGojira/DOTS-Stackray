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

namespace Stackray.Renderer {
  class RenderData<TFilter1> : IDisposable 
    where TFilter1 : struct, ISharedComponentData {

    private const string COMPUTE_KEYWORD = "USE_COMPUTE";
    Dictionary<string, IBufferGroup> m_buffers = new Dictionary<string, IBufferGroup>();
    List<JobHandle> m_jobs = new List<JobHandle>();

    public ComputeBuffer ArgsBuffer {
      get;
      private set;
    }

    public Mesh Mesh {
      get;
      private set;
    }

    public Material Material {
      get;
      private set;
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

    public TFilter1 Filter1;
    public int FilterIndex;

    public uint InstanceCount {
      get => m_args[1];
      private set => m_args[1] = value;
    }

    public EntityQuery Query {
      get;
      private set;
    }

    ComponentSystemBase m_system;
    NativeUnit<AABB> m_chunkWorldRenderBounds;
    uint[] m_args;
    Dictionary<Type, string> m_fixedBuffersInfo;
    Dictionary<Type, string> m_dynamicBuffersInfo;

    public RenderData(
      ComponentSystemBase system,
      Mesh mesh,
      Material material,
      Dictionary<Type, string> fixedBuffers,
      Dictionary<Type, string> dynamicBuffers) {

      m_system = system;
      Mesh = mesh;
      Material = material;
      Material?.EnableKeyword(COMPUTE_KEYWORD);
      m_args = new uint[5] { 0, 0, 0, 0, 0 };
      ArgsBuffer = new ComputeBuffer(1, m_args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
      m_fixedBuffersInfo = fixedBuffers;
      m_dynamicBuffersInfo = dynamicBuffers;
      m_chunkWorldRenderBounds = new NativeUnit<AABB>(Allocator.Persistent);
        Query = system.EntityManager.CreateEntityQuery(
          ComponentType.ReadOnly<TFilter1>());
    }

    public void Dispose() {
      ArgsBuffer?.Release();
      ArgsBuffer = null;
      foreach (var buffer in m_buffers.Values)
        buffer?.Dispose();
      m_buffers.Clear();
      m_chunkWorldRenderBounds.Dispose();
      Material?.DisableKeyword(COMPUTE_KEYWORD);
    }

    public bool Update() {
      Profiler.BeginSample("Update RenderData");
      SetFilter();
      var boundsInputDeps = UpdateBounds();
      var instanceCount = Query.CalculateEntityCount();
      var inputDeps = default(JobHandle);
      Profiler.BeginSample("Update Buffers");
      inputDeps = UpdateBuffers(instanceCount, inputDeps);
      Profiler.EndSample();
      inputDeps = JobHandle.CombineDependencies(boundsInputDeps, inputDeps);
      Complete(inputDeps);
      UpdateArgs(instanceCount);
      Profiler.EndSample();
      return InstanceCount > 0;
    }

    private JobHandle UpdateBuffers(int instanceCount, JobHandle inputDeps) {
      Profiler.BeginSample("Traverse fixed buffer");
      foreach (var kvp in m_fixedBuffersInfo)
        m_jobs.Add(UpdateBuffer(kvp.Key, kvp.Value, instanceCount, inputDeps));
      Profiler.EndSample();
      Profiler.BeginSample("Traverse dynamic buffer");
      foreach (var kvp in m_dynamicBuffersInfo)
        m_jobs.Add(UpdateBuffer(kvp.Key, kvp.Value, instanceCount, inputDeps));
      Profiler.EndSample();
      Profiler.BeginSample("Combine jobs");
      inputDeps = JobUtility.CombineDependencies(m_jobs);
      m_jobs.Clear();
      Profiler.EndSample();
      return inputDeps;
    }

    private void SetFilter() {
      Query.SetSharedComponentFilter(Filter1);
    }

    private void UpdateArgs(int instanceCount, int submeshIndex = 0) {
      InstanceCount = (uint)instanceCount;
      SubmeshIndex = submeshIndex;
      ArgsBuffer.SetData(m_args);
    }

    private JobHandle UpdateBuffer(Type bufferType, string bufferName, int instanceCount, JobHandle inputDeps = default) {
      if (instanceCount == 0)
        return inputDeps;

      if (!m_buffers.ContainsKey(bufferName))
        m_buffers[bufferName] = CreateBufferGroup(bufferType);
      inputDeps = m_buffers[bufferName].Update(m_system, Query, instanceCount, inputDeps);
      return inputDeps;
    }

    private void Complete(JobHandle inputDeps) {
      inputDeps.Complete();

      foreach (var kvp in m_buffers) {
        Profiler.BeginSample($"Buffer Push");
        kvp.Value.Push();
        Profiler.EndSample();
        Profiler.BeginSample($"Material SetBuffer");
        Material?.EnableKeyword(COMPUTE_KEYWORD);
        Material.SetBuffer(kvp.Key, kvp.Value.ComputeBuffer);
        Profiler.EndSample();
      }

      Bounds = new Bounds(Vector3.zero,
        new float3(math.length(m_chunkWorldRenderBounds.Value.Center + m_chunkWorldRenderBounds.Value.Extents)));
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
      m_chunkWorldRenderBounds.Value = default;
      var chunks = Query.CreateArchetypeChunkArray(Allocator.TempJob, out var createChunksHandle);
      inputDeps = JobHandle.CombineDependencies(inputDeps, createChunksHandle);
      inputDeps = new ExtractChunkWorldRenderBounds<RenderMesh> {
        ChunkWorldBounds = m_chunkWorldRenderBounds,
        ChunkWorldRenderBoundsType = m_system.GetArchetypeChunkComponentType<ChunkWorldRenderBounds>(true),
        FilterType = m_system.GetArchetypeChunkSharedComponentType<RenderMesh>(),
        SharedComponentIndex = FilterIndex,
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
