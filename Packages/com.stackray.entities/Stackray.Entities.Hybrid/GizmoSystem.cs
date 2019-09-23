using System;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using UnityEngine;

namespace Stackray.Entities {
  class GizmoSystemBootstrap : ComponentSystem {

    protected override void OnStartRunning() {
      base.OnStartRunning();
      var gizmoManager = UnityEngine.Object.FindObjectOfType<HybridGizmo>();
      if (!gizmoManager)
        gizmoManager = new GameObject("Gizmos").AddComponent<HybridGizmo>();
      foreach (var manager in World.Active.Systems)
        foreach (var method in manager.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
          if (method.GetCustomAttributes(typeof(DrawGizmos), true).Any())
            gizmoManager.DrawGizmos += () => method.Invoke(manager, Array.Empty<object>());
    }

    protected override void OnUpdate() {
    }
  }

  [ExecuteInEditMode]
  class HybridGizmo : MonoBehaviour {
    public Action DrawGizmos;
    public Action DrawGizmosSelected;

    private void OnDrawGizmos() => DrawGizmos?.Invoke();

    private void OnDrawGizmosSelected() => DrawGizmosSelected?.Invoke();

    private void OnDestroy() {
      DrawGizmos = null;
      DrawGizmosSelected = null;
    }
  }

  /// <summary>
  /// Will be called when draw gizmos life cycle
  /// </summary>
  public class DrawGizmos : Attribute { }

  /// <summary>
  /// Will be called when draw gizmos selected life cycle
  /// </summary>
  public class DrawGizmosSelected : Attribute { }
}
