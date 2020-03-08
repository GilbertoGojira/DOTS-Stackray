using Stackray.Entities.Editor;
using Stackray.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Stackray.Renderer {
  public class ColorPropertyEditor : ComponentDataEditor<ColorProperty> {
    public override void OnInspectorGUI(ColorProperty target) {
      base.OnInspectorGUI(target);
      EditorGUI.indentLevel++;
      EditorGUILayout.ColorField(nameof(ColorProperty.Value), target.Value.ToColor());
      EditorGUI.indentLevel--;
    }
  }
}
