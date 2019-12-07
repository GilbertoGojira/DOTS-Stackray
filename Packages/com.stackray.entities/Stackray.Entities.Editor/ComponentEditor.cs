using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Stackray.Entities.Editor {

  public interface IComponentEditor<T> {
    void OnInspectorGUI(T target);
  }

  public abstract class ComponentDataEditor<T> : IComponentEditor<T> 
    where T : struct, IComponentData {

    public virtual void OnInspectorGUI(T target) {
      var enabled = GUI.enabled;
      GUI.enabled = true;
      EditorGUILayout.LabelField(
        typeof(T).Name,
        new GUIStyle(EditorStyles.boldLabel) {
          fontStyle = FontStyle.Bold
        });
      GUI.enabled = enabled;
    }
  }

  public abstract class SharedComponentDataEditor<T> : IComponentEditor<T>
    where T : struct, ISharedComponentData {

    public virtual void OnInspectorGUI(T target) {
      var enabled = GUI.enabled;
      GUI.enabled = true;
      EditorGUILayout.LabelField(
        typeof(T).Name,
        new GUIStyle(EditorStyles.boldLabel) {
          fontStyle = FontStyle.Bold
        });
      GUI.enabled = enabled;
    }
  }

  public abstract class BufferElementDataEditor<T> : IComponentEditor<T> 
    where T : struct, IBufferElementData {

    public virtual void OnInspectorGUI(T target, string index) {
      var enabled = GUI.enabled;
      GUI.enabled = true;
      EditorGUILayout.LabelField(
        string.IsNullOrEmpty(index) ? typeof(T).Name : index,
        new GUIStyle(EditorStyles.boldLabel) {
          fontStyle = FontStyle.Bold
        });
      GUI.enabled = enabled;
    }

    public void OnInspectorGUI(T target) {
      OnInspectorGUI(target, string.Empty);
    }
  }
}
