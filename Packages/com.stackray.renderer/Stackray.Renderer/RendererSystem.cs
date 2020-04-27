using Stackray.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Profiling;

namespace Stackray.Renderer {

  [ExecuteAlways]
  [AlwaysUpdateSystem]
  [UpdateInGroup(typeof(PresentationSystemGroup))]
  [UpdateAfter(typeof(RenderBoundsUpdateSystem))]
  public class RendererSystem : ComponentSystem {

    Dictionary<RenderMesh, RenderData<RenderMesh>> m_renderData =
      new Dictionary<RenderMesh, RenderData<RenderMesh>>();
    EntityQuery m_missingColorProperty;
    EntityQuery m_renderQuery;
    int m_lastOrderInfo;
    Dictionary<Type, string> m_availableFixedBuffers;
    Dictionary<Type, string> m_availableDynamicBuffers;

    protected override void OnCreate() {
      m_availableFixedBuffers = BufferGroupUtility.GetAvailableBufferProperties(typeof(IFixedBufferProperty<>), inst => ((IBufferProperty)inst).BufferName);
      m_availableDynamicBuffers = BufferGroupUtility.GetAvailableBufferProperties(typeof(IDynamicBufferProperty<>), inst => ((IBufferProperty)inst).BufferName);
      var queryDesc = new EntityQueryDesc {
        All = new ComponentType[] {
            ComponentType.ReadOnly<RenderMesh>(),
            ComponentType.ChunkComponent<ChunkWorldRenderBounds>()
          }.Union(m_availableFixedBuffers.Select(kvp => ComponentType.ReadOnly(kvp.Key))).ToArray(),
        Any = m_availableDynamicBuffers.Select(kvp => ComponentType.ReadOnly(kvp.Key)).ToArray()
      };
      m_renderQuery = GetEntityQuery(queryDesc);
      m_missingColorProperty = GetEntityQuery(new EntityQueryDesc {
        All = new ComponentType[] {
            ComponentType.ReadOnly<RenderMesh>(),
            ComponentType.ChunkComponent<ChunkWorldRenderBounds>()
          },
        None = new ComponentType[] { ComponentType.ReadOnly<ColorProperty>() }
      });
    }

    protected override void OnStartRunning() {
      base.OnStartRunning();
#if ENABLE_HYBRID_RENDERER_V2
      World.GetOrCreateSystem<HybridRendererSystem>().Enabled = false;
#else
      World.GetOrCreateSystem<RenderMeshSystemV2>().Enabled = false;
#endif
    }

    protected override void OnStopRunning() {
      base.OnStopRunning();
      foreach (var kvp in m_renderData)
        kvp.Value.Dispose();
      m_renderData.Clear();
      m_lastOrderInfo = -1;
    }

    private void CheckIfChanged() {
      if (m_lastOrderInfo != m_renderQuery.GetCombinedComponentOrderVersion()) {
        m_lastOrderInfo = m_renderQuery.GetCombinedComponentOrderVersion();
        var renderMeshes = new List<RenderMesh>();
        var sharedComponentIndices = new List<int>();
        EntityManager.GetAllUniqueSharedComponentData(renderMeshes, sharedComponentIndices);
        renderMeshes.Remove(default);
        sharedComponentIndices.Remove(default);
        for (var i = 0; i < renderMeshes.Count; ++i) {
          if (!m_renderData.ContainsKey(renderMeshes[i])) {
            m_renderData.Add(renderMeshes[i],
              new RenderData<RenderMesh>(
                this,
                renderMeshes[i],
                m_availableFixedBuffers,
                m_availableDynamicBuffers));
          }
          m_renderData[renderMeshes[i]].FilterIndex = sharedComponentIndices[i];
          // Alternatively we could filter by something else
          // By default we will filter by RenderMesh
          m_renderData[renderMeshes[i]].Filter = renderMeshes[i];
        }
      }
    }

    protected override void OnUpdate() {
      EntityManager.AddComponentData(
        m_missingColorProperty,
        new NativeArray<ColorProperty>(
          Enumerable.Range(0, m_missingColorProperty.CalculateEntityCount()).Select(_ => new ColorProperty { Value = new half4(1) }).ToArray(), Allocator.Temp));
      Profiler.BeginSample("Check if Renderer changed");
      CheckIfChanged();
      Profiler.EndSample();
      foreach (var renderData in m_renderData.Values) {
        if (renderData.Update())
          Graphics.DrawMeshInstancedIndirect(
            renderData.Mesh, 
            renderData.SubmeshIndex, 
            renderData.Material, 
            renderData.Bounds, 
            renderData.ArgsBuffer,
            layer: renderData.Layer,
            castShadows: renderData.CastShadows,
            receiveShadows: renderData.ReceiveShadows);
      }
    }

    [DrawGizmos]
    void OnDrawGizmos() {
      Gizmos.color = Color.black;
      foreach (var render in m_renderData.Values)
        Gizmos.DrawWireCube(render.Bounds.center, render.Bounds.size);
    }
  }
}