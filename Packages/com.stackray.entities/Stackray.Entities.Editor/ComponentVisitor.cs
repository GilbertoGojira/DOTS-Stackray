using System;
using System.Linq;
using System.Reflection;
using Unity.Properties;
using UnityEditor;

namespace Stackray.Entities.Editor {
  class ComponentVisitor {

    interface IVisitor : IPropertyVisitorAdapter {
      void LoadValueMethod(MethodInfo method);
      void LoadClassMethod(MethodInfo method);
      void LoadClassMethodWithIndex(MethodInfo method);
    }

    class Visitor<T> : IVisitor/*, IVisitAdapter<T>*/ {
      delegate void OnGUIDelegate(ref T value);
      delegate void OnGUIDelegateByClass(T value);
      delegate void OnGUIDelegateByClassWithIndex(T value, string index);

      OnGUIDelegate m_guiValueMethod;
      OnGUIDelegateByClass m_guiClassMethod;
      OnGUIDelegateByClassWithIndex m_guiClassMethodWithIndex;

      public void LoadValueMethod(MethodInfo method) {
        m_guiValueMethod = (OnGUIDelegate)Delegate.CreateDelegate(typeof(OnGUIDelegate), method);
      }

      public void LoadClassMethod(MethodInfo method) {
        m_guiClassMethod = (OnGUIDelegateByClass)Delegate.CreateDelegate(typeof(OnGUIDelegateByClass), null, method);
      }

      public void LoadClassMethodWithIndex(MethodInfo method) {
        m_guiClassMethodWithIndex = (OnGUIDelegateByClassWithIndex)Delegate.CreateDelegate(typeof(OnGUIDelegateByClassWithIndex), null, method);
      }

      public VisitStatus Visit<TProperty, TContainer>(PropertyVisitor visitor, TProperty property, ref TContainer container, ref T value/*, ref ChangeTracker changeTracker*/)
          where TProperty : Property<TContainer, T> {
        EditorGUI.BeginChangeCheck();

        m_guiValueMethod?.Invoke(ref value);
        m_guiClassMethod?.Invoke(value);
        m_guiClassMethodWithIndex?.Invoke(value, property.Name);

        /*if (EditorGUI.EndChangeCheck()) {
          changeTracker.MarkChanged();
        }*/
        return VisitStatus.Handled;
      }
    }

    public static bool TryGetAdapterForClass(Type componentType, out IPropertyVisitorAdapter visitor) {
      MethodInfo method = null;
      if (IsSubClassOfGeneric(componentType, typeof(BufferElementDataEditor<>)))
        method = componentType.GetMethods().SingleOrDefault(
             m =>
                 m.ReturnType == typeof(void) &&
                 m.Name == nameof(IComponentEditor<int>.OnInspectorGUI) &&
                 m.GetParameters().Length == 2 &&
                 m.GetParameters()[1].ParameterType == typeof(string));
      else
        method = componentType.GetMethods().SingleOrDefault(
           m =>
               m.ReturnType == typeof(void) &&
               m.Name == nameof(IComponentEditor<int>.OnInspectorGUI) &&
               m.GetParameters().Length == 1);

      if (method != null) {
        var args = method.GetParameters();
        var targetType = args[0].ParameterType;
        var result = (IVisitor)Activator.CreateInstance(typeof(Visitor<>).MakeGenericType(targetType));
        if (args.Length == 1)
          result.LoadClassMethod(method);
        else if (args.Length == 2)
          result.LoadClassMethodWithIndex(method);
        visitor = result;
        return true;
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

    static bool IsSubClassOfGeneric(Type child, Type parent) {
      if (child == parent)
        return false;

      if (child.IsSubclassOf(parent))
        return true;

      var parameters = parent.GetGenericArguments();
      var isParameterLessGeneric = !(parameters != null && parameters.Length > 0 &&
          ((parameters[0].Attributes & TypeAttributes.BeforeFieldInit) == TypeAttributes.BeforeFieldInit));

      while (child != null && child != typeof(object)) {
        var cur = GetFullTypeDefinition(child);
        if (parent == cur || (isParameterLessGeneric && cur.GetInterfaces().Select(i => GetFullTypeDefinition(i)).Contains(GetFullTypeDefinition(parent))))
          return true;
        else if (!isParameterLessGeneric)
          if (GetFullTypeDefinition(parent) == cur && !cur.IsInterface) {
            if (VerifyGenericArguments(GetFullTypeDefinition(parent), cur))
              if (VerifyGenericArguments(parent, child))
                return true;
          } else
            foreach (var item in child.GetInterfaces()
              .Where(i => GetFullTypeDefinition(parent) == GetFullTypeDefinition(i)))
              if (VerifyGenericArguments(parent, item))
                return true;

        child = child.BaseType;
      }

      return false;
    }

    static Type GetFullTypeDefinition(Type type) {
      return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
    }

    static bool VerifyGenericArguments(Type parent, Type child) {
      var childArguments = child.GetGenericArguments();
      var parentArguments = parent.GetGenericArguments();
      if (childArguments.Length == parentArguments.Length)
        for (var i = 0; i < childArguments.Length; i++)
          if (childArguments[i].Assembly != parentArguments[i].Assembly || 
            childArguments[i].Name != parentArguments[i].Name || 
            childArguments[i].Namespace != parentArguments[i].Namespace)
            if (!childArguments[i].IsSubclassOf(parentArguments[i]))
              return false;
      return true;
    }
  }
}
