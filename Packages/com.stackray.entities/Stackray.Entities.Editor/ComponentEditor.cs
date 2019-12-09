using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Stackray.Entities.Editor {

  public interface IComponentEditor<T> {
    void OnInspectorGUI(T target);
  }

  public abstract class ComponentDataEditorBase<T> : IComponentEditor<T> {
    public virtual void OnInspectorGUI(T target) {
      throw new System.NotImplementedException();
    }

    protected void DrawLabel(string label) {
      var enabled = GUI.enabled;
      GUI.enabled = true;
      EditorGUILayout.LabelField(
        label,
        new GUIStyle(EditorStyles.boldLabel) {
          fontStyle = FontStyle.Bold
        });
      GUI.enabled = enabled;
    }
  }

  public abstract class ComponentDataEditor<T> : ComponentDataEditorBase<T> 
    where T : struct, IComponentData {

    public override void OnInspectorGUI(T target) {
      DrawLabel(typeof(T).Name);
    }
  }

  public abstract class SharedComponentDataEditor<T> : ComponentDataEditorBase<T>
    where T : struct, ISharedComponentData {

    public override void OnInspectorGUI(T target) {
      DrawLabel(typeof(T).Name);
    }
  }

  public abstract class BufferElementDataEditor<T> : ComponentDataEditorBase<T> 
    where T : struct, IBufferElementData {

    public virtual void OnInspectorGUI(T target, string index) {
      DrawLabel(string.IsNullOrEmpty(index) ? typeof(T).Name : index);
    }

    public override void OnInspectorGUI(T target) {
      OnInspectorGUI(target, string.Empty);
    }
  }
}
