using Stackray.Entities.Editor;
using Unity.Mathematics;
using UnityEditor;

namespace Stackray.Renderer {
  public class PivotPropertyEditor : ComponentDataEditor<PivotProperty> {
    public override void OnInspectorGUI(PivotProperty target) {
      base.OnInspectorGUI(target);
      EditorGUI.indentLevel++;
      EditorGUILayout.Vector2Field(nameof(PivotProperty.Value), (float2)target.Value);
      EditorGUI.indentLevel--;
    }
  }
}
