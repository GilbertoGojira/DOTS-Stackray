using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;

namespace Stackray.Entities {

  public struct ComponentAccessState<T> : IDisposable where T : Component {
    public T[] Data;
    public int OrderVersion;

    public void Dispose() {
      Data = null;
    }
  }

  public struct GameObjectAccessState : IDisposable {
    public List<GameObject> Data;
    public int OrderVersion;

    public void Dispose() {
      Data = null;
    }
  }

  public struct GameObjectEntityAccessState : IDisposable {
    public IDictionary<Entity, GameObject> Data;
    public int OrderVersion;

    public void Dispose() {
      Data = null;
    }
  }

  public static class EntityQueryExtensionsForGameObjectAccessList {

    public static T[] GetComponentAccess<T>(this EntityQuery group, EntityManager entityManager, ref ComponentAccessState<T> state)
      where T : Component {
      var orderVersion = entityManager.GetComponentOrderVersion<T>();
      if (state.Data != null && orderVersion == state.OrderVersion)
        return state.Data;

      state.OrderVersion = orderVersion;

      UnityEngine.Profiling.Profiler.BeginSample("DirtyComponentAccessUpdate");
      state.Data = group.ToComponentArray<T>();
      UnityEngine.Profiling.Profiler.EndSample();
      return state.Data;
    }

    public static List<GameObject> GetGameObjectAccess<T>(this EntityQuery group, EntityManager entityManager, ref GameObjectAccessState state)
      where T : Component {
      var orderVersion = entityManager.GetComponentOrderVersion<T>();
      if (state.Data != null && orderVersion == state.OrderVersion)
        return state.Data;

      state.OrderVersion = orderVersion;

      UnityEngine.Profiling.Profiler.BeginSample("DirtyGameObjectAccessUpdate");
      state.Data = group.ToComponentArray<T>().Select(t => t.gameObject).ToList();
      UnityEngine.Profiling.Profiler.EndSample();
      return state.Data;
    }

    public static bool GetGameObjectAccess<T>(this EntityQuery group, EntityManager entityManager, ref GameObjectEntityAccessState state)
      where T : Component {
      var orderVersion = entityManager.GetComponentOrderVersion<T>();
      if (state.Data != null && orderVersion == state.OrderVersion)
        return false;

      state.OrderVersion = orderVersion;

      UnityEngine.Profiling.Profiler.BeginSample("DirtyGameObjectAccessUpdate");
      var compData = group.ToComponentArray<T>();
      var entities = group.ToEntityArray(Unity.Collections.Allocator.TempJob);
      state.Data = entities.Zip(compData, (k, v) => new { k, v }).ToDictionary(x => x.k, x => x.v.gameObject);
      entities.Dispose();
      UnityEngine.Profiling.Profiler.EndSample();
      return true;
    }
  }
}
