using Stackray.Entities.Editor;
using Unity.Mathematics;
using UnityEditor;

namespace Stackray.Renderer {
  public class ScalePropertyEditor : ComponentDataEditor<ScaleProperty> {
    public override void OnInspectorGUI(ScaleProperty target) {
      base.OnInspectorGUI(target);
      EditorGUI.indentLevel++;
      EditorGUILayout.Vector3Field(nameof(ScaleProperty.Value), (float3)target.Value);
      EditorGUI.indentLevel--;
    }
  }
}
