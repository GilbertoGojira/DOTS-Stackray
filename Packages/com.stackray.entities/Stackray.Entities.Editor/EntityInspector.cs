using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using Unity.Entities.Editor;
using Unity.Properties;
using UnityEditor;
using UnityEngine;

namespace Stackray.Entities.Editor {

  public static class EntityInspector {
    static Type m_proxyEditor = typeof(EntitySelectionProxy).Assembly.GetType("Unity.Entities.Editor.EntitySelectionProxyEditor");
    static Type m_entityDebugger = typeof(EntitySelectionProxy).Assembly.GetType("Unity.Entities.Editor.EntityDebugger");
    static FieldInfo m_visitorField = m_proxyEditor.GetField("visitor", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

    public static readonly List<IPropertyVisitorAdapter> Adapters = new List<IPropertyVisitorAdapter>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() {
      var entityDebuggerInstance = Resources.FindObjectsOfTypeAll(m_entityDebugger).SingleOrDefault();
      var entityListViewField = entityDebuggerInstance?.GetType().GetField("entityListView", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
      var entityListViewInstance = entityListViewField?.GetValue(entityDebuggerInstance);
      var selectedEntityQueryProperty = entityListViewInstance?.GetType().GetProperty("SelectedEntityQuery");
      selectedEntityQueryProperty?.SetValue(entityListViewInstance, null);
    }

    [InitializeOnLoadMethod]
    static void Init() {
      Selection.selectionChanged += OnSelectionChanged;

      foreach (var type in TypeCache.GetTypesDerivedFrom(typeof(IComponentEditor<>)))
        if (!type.IsAbstract && !type.IsGenericType && ComponentVisitor.TryGetAdapterForClass(type, out var adapter))
          Adapters.Add(adapter);

      foreach (var type in TypeCache.GetTypesDerivedFrom<ISharedComponentData>())
        if (!type.IsAbstract && !type.IsGenericType && ComponentVisitor.TryGetAdapterForValue(type, out var adapter))
          Adapters.Add(adapter);

      foreach (var type in TypeCache.GetTypesDerivedFrom<IComponentData>())
        if (!type.IsAbstract && !type.IsGenericType && ComponentVisitor.TryGetAdapterForValue(type, out var adapter))
          Adapters.Add(adapter);

      foreach (var type in TypeCache.GetTypesDerivedFrom<IBufferElementData>())
        if (!type.IsAbstract && !type.IsGenericType && ComponentVisitor.TryGetAdapterForValue(type, out var adapter))
          Adapters.Add(adapter);
    }

    private static void OnSelectionChanged() {
      if (Selection.activeObject != null && Selection.activeObject.GetType() == typeof(EntitySelectionProxy)) {
        var editor = Resources.FindObjectsOfTypeAll(m_proxyEditor)[0];
        var visitor = (PropertyVisitor)m_visitorField.GetValue(editor);
        foreach (var adapter in Adapters)
          visitor.AddAdapter(adapter);
      }
    }
  }
}
