﻿using Stackray.Collections;
using Stackray.Entities;
using Stackray.Jobs;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Stackray.Renderer {
  class RenderData<TFilter> : IDisposable 
    where TFilter : struct, ISharedComponentData {

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

    public int Layer {
      get;
      private set;
    }

    public ShadowCastingMode CastShadows {
      get;
      private set;
    }

    public bool ReceiveShadows {
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

    public TFilter Filter;
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
      RenderMesh renderMesh,
      Dictionary<Type, string> fixedBuffers,
      Dictionary<Type, string> dynamicBuffers) {

      m_system = system;
      InitRenderMeshValues(renderMesh);
      m_args = new uint[5] { 0, 0, 0, 0, 0 };
      ArgsBuffer = new ComputeBuffer(1, m_args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
      m_fixedBuffersInfo = fixedBuffers;
      m_dynamicBuffersInfo = dynamicBuffers;
      m_chunkWorldRenderBounds = new NativeUnit<AABB>(Allocator.Persistent);
        Query = system.EntityManager.CreateEntityQuery(
          ComponentType.ReadOnly<TFilter>());
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

    void InitRenderMeshValues(RenderMesh renderMesh) {
      Mesh = renderMesh.mesh;
      // We must create another material otherwise we might end up
      // overwriting in the same buffer positions
      // eg. When using same material in different meshes
      Material = new Material(renderMesh.material);
      Material?.EnableKeyword(COMPUTE_KEYWORD);
      Layer = renderMesh.layer;
      CastShadows = renderMesh.castShadows;
      ReceiveShadows = renderMesh.receiveShadows;
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
      Query.SetSharedComponentFilter(Filter);
    }

    private void UpdateArgs(int instanceCount, int submeshIndex = 0) {
      InstanceCount = (uint)instanceCount;
      SubmeshIndex = submeshIndex;
      ArgsBuffer.SetData(m_args);
    }

    private JobHandle UpdateBuffer(Type bufferType, string bufferName, int instanceCount, JobHandle inputDeps = default) {
      if (!m_buffers.ContainsKey(bufferName))
        m_buffers[bufferName] = CreateBufferGroup(bufferType);
      m_buffers[bufferName].BeginWrite(instanceCount);
      if(instanceCount > 0)
        inputDeps = m_buffers[bufferName].Update(m_system, Query, inputDeps);
      return inputDeps;
    }

    private void Complete(JobHandle inputDeps) {
      inputDeps.Complete();

      foreach (var kvp in m_buffers) {
        Profiler.BeginSample($"Buffer EndWrite");
        kvp.Value.EndWrite();
        Profiler.EndSample();
        Profiler.BeginSample($"Material SetBuffer");
        Material.EnableKeyword(COMPUTE_KEYWORD);
        Material.SetBuffer(kvp.Key, kvp.Value.ComputeBuffer);
        Profiler.EndSample();
      }

      Bounds = new Bounds(Vector3.zero,
        new float3(math.length(m_chunkWorldRenderBounds.Value.Center + m_chunkWorldRenderBounds.Value.Extents) * 2));
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
          .GetConstructor(Type.EmptyTypes);
        Debug.Assert(constructor != null, $"No default constructor for type {type.Name}");
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
      var chunks = Query.CreateArchetypeChunkArrayAsync(Allocator.TempJob, out var createChunksHandle);
      inputDeps = JobHandle.CombineDependencies(inputDeps, createChunksHandle);
      inputDeps = new FilterChunkWorldRenderBounds<RenderMesh> {
        ChunkWorldBounds = m_chunkWorldRenderBounds,
        ChunkWorldRenderBoundsType = m_system.GetComponentTypeHandle<ChunkWorldRenderBounds>(true),
        FilterType = m_system.GetSharedComponentTypeHandle<RenderMesh>(),
        SharedComponentIndex = FilterIndex,
        Chunks = chunks
      }.Schedule(inputDeps);
      return inputDeps;
    }
    #endregion extract renderData Bounds
  }
}
