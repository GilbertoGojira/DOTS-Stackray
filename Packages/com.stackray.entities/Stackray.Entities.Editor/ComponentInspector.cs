using System;
using System.Reflection;
using Unity.Properties;
using UnityEditor;

namespace Stackray.Entities.Editor {
  class ComponentInspector {

    interface IVisitor : IPropertyVisitorAdapter {
      void LoadValueMethod(MethodInfo method);
      void LoadClassMethod(MethodInfo method);
    }

    class Visitor<T> : IVisitor, IVisitAdapter<T> {
      delegate void OnGUIDelegate(ref T value);
      delegate void OnGUIDelegateByClass(T value);

      OnGUIDelegate m_guiValueMethod;
      OnGUIDelegateByClass m_guiClassMethod;

      public void LoadValueMethod(MethodInfo method) {
        m_guiValueMethod = (OnGUIDelegate)Delegate.CreateDelegate(typeof(OnGUIDelegate), method);
      }

      public void LoadClassMethod(MethodInfo method) {
        m_guiClassMethod = (OnGUIDelegateByClass)Delegate.CreateDelegate(typeof(OnGUIDelegateByClass), null, method);
      }

      public VisitStatus Visit<TProperty, TContainer>(IPropertyVisitor visitor, TProperty property, ref TContainer container, ref T value, ref ChangeTracker changeTracker)
          where TProperty : IProperty<TContainer, T> {
        EditorGUI.BeginChangeCheck();

        m_guiValueMethod?.Invoke(ref value);
        m_guiClassMethod?.Invoke(value);

        if (EditorGUI.EndChangeCheck()) {
          changeTracker.MarkChanged();
        }
        return VisitStatus.Handled;
      }
    }

    public static bool TryGetAdapterForClass(Type componentType, out IPropertyVisitorAdapter visitor) {
      var method = componentType.GetMethod(
        nameof(IComponentEditor<int>.OnInspectorGUI), 
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
      if (method != null && method.ReturnType == typeof(void)) {
        var args = method.GetParameters();
        if (args.Length == 1) {
          var targetType = args[0].ParameterType;
          var result = (IVisitor)Activator.CreateInstance(typeof(Visitor<>).MakeGenericType(targetType));
          result.LoadClassMethod(method);
          visitor = result;
          return true;
        }
      }
      visitor = null;
      return false;
    }

    public static bool TryGetAdapterForValue(Type componentType, out IPropertyVisitorAdapter visitor) {
      var method = componentType.GetMethod(
        nameof(IComponentEditor<int>.OnInspectorGUI), 
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
      if (method != null && method.ReturnType == typeof(void)) {
        var args = method.GetParameters();
        if (args.Length == 0) {
          var result = (IVisitor)Activator.CreateInstance(typeof(Visitor<>).MakeGenericType(componentType));
          result.LoadValueMethod(method);
          visitor = result;
          return true;
        }
      }
      visitor = null;
      return false;
    }
  }
}
