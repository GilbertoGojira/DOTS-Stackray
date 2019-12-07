using Stackray.Entities.Editor;
using UnityEditor;
using UnityEngine;

namespace Stackray.Text {
  class FontMaterialEditor : SharedComponentDataEditor<FontMaterial> {

    public override void OnInspectorGUI(FontMaterial target) {
      base.OnInspectorGUI(target);
      EditorGUI.indentLevel++;
      EditorGUILayout.ObjectField(nameof(FontMaterial.Value), target.Value, typeof(Material), false);
      EditorGUI.indentLevel--;
    }
  }
}
