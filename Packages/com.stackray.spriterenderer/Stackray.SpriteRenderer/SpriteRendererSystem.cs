using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Stackray.Entities;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Profiling;

namespace Stackray.SpriteRenderer {

  [UpdateInGroup(typeof(PresentationSystemGroup))]
  [UpdateAfter(typeof(RenderBoundsUpdateSystem))]
  public class SpriteRendererSystem : ComponentSystem {

    Dictionary<SpriteRenderMesh, RenderData<SpriteAnimation>> m_renderData =
      new Dictionary<SpriteRenderMesh, RenderData<SpriteAnimation>>();
    EntityQuery m_renderQuery;
    int m_lastOrderInfo;
    Dictionary<Type, string> m_availableFixedBuffers;
    Dictionary<Type, string> m_availableDynamicBuffers;

    protected override void OnCreate() {
      m_availableFixedBuffers = GetAvailableBufferProperties(typeof(IFixedBufferProperty<>), nameof(IBufferProperty<bool>.BufferName));
      m_availableDynamicBuffers = GetAvailableBufferProperties(typeof(IDynamicBufferProperty<>), nameof(IBufferProperty<bool>.BufferName));
      var queryDesc = new EntityQueryDesc {
        All = new ComponentType[] {
            ComponentType.ReadOnly<SpriteRenderMesh>(),
            ComponentType.ReadOnly<SpriteAnimation>(),
            ComponentType.ChunkComponent<ChunkWorldRenderBounds>()
          }.Union(m_availableFixedBuffers.Select(kvp => ComponentType.ReadOnly(kvp.Key))).ToArray(),
        Any = m_availableDynamicBuffers.Select(kvp => ComponentType.ReadOnly(kvp.Key)).ToArray()
      };
      m_renderQuery = GetEntityQuery(queryDesc);
    }

    protected override void OnDestroy() {
      foreach (var kvp in m_renderData)
        kvp.Value.Dispose();
      m_renderData.Clear();
    }

    private void CheckIfChanged() {
      if (m_lastOrderInfo != m_renderQuery.GetCombinedComponentOrderVersion()) {
        m_lastOrderInfo = m_renderQuery.GetCombinedComponentOrderVersion();
        var spriteMeshes = new List<SpriteRenderMesh>();
        var sharedComponentIndices = new List<int>();
        var extraFilter = new List<SpriteAnimation>();        
        EntityManager.GetAllUniqueSharedComponentData(spriteMeshes, sharedComponentIndices);
        EntityManager.GetAllUniqueSharedComponentData(extraFilter);
        extraFilter = extraFilter.Where(f => f.ClipSetEntity != Entity.Null).ToList();
        for (var i = 0; i < spriteMeshes.Count; ++i)
          if (spriteMeshes[i].Mesh && spriteMeshes[i].Material) {
            if (m_renderData.ContainsKey(spriteMeshes[i])) {
              m_renderData[spriteMeshes[i]].SharedComponentIndex = sharedComponentIndices[i];
              m_renderData[spriteMeshes[i]].ExtraFilter = extraFilter;
            } else
              m_renderData.Add(spriteMeshes[i],
                new RenderData<SpriteAnimation>(this, spriteMeshes[i], sharedComponentIndices[i], m_availableFixedBuffers, m_availableDynamicBuffers, extraFilter));
          }
        foreach (var spriteMesh in m_renderData.Keys.ToList())
          if (!spriteMeshes.Contains(spriteMesh)) {
            m_renderData[spriteMesh].Dispose();
            m_renderData.Remove(spriteMesh);
          }
      }
    }

    protected override void OnUpdate() {
      Profiler.BeginSample("Check if SpriteRenderer changed");
      CheckIfChanged();
      Profiler.EndSample();
      foreach (var renderData in m_renderData.Values) {
        if (renderData.Update())
          Graphics.DrawMeshInstancedIndirect(
            renderData.Mesh, renderData.SubmeshIndex, renderData.Material, renderData.Bounds, renderData.ArgsBuffer);
      }
    }

    static Dictionary<Type, string> GetAvailableBufferProperties(Type componentType, string bufferNameProperty) {
      var bufferTypes = new Dictionary<Type, string>();
      var availableTypes = new List<Type>();
      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
        IEnumerable<Type> allTypes;
        try {
          allTypes = assembly.GetTypes();
        } catch (ReflectionTypeLoadException e) {
          allTypes = e.Types.Where(t => t != null);
        }
        availableTypes.AddRange(allTypes.Where(t => t.IsValueType && t.ImplementsInterface(componentType)));
      }

      foreach (var type in availableTypes) {
        var propertyInterface = type.GetInterfaces().FirstOrDefault(
          t => t.IsGenericType && t.GetGenericTypeDefinition() == componentType);
        var argType = propertyInterface.GetGenericArguments().Length > 0 &&
          propertyInterface.GetGenericArguments()[0].ImplementsInterface(typeof(IComponentData)) ? propertyInterface.GetGenericArguments()[0] : type;
        var instance = Activator.CreateInstance(type);
        var bufferName = (string)type.GetProperty(bufferNameProperty).GetValue(instance);
        bufferTypes.Add(argType, bufferName);
      }
      return bufferTypes;
    }
  }
}