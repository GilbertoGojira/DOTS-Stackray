using Stackray.Entities.Editor;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Stackray.Text {
  class TextRendererEditor : ComponentDataEditor<TextRenderer> {

    public override void OnInspectorGUI(TextRenderer target) {
      base.OnInspectorGUI(target);
      var fontMaterial = World.DefaultGameObjectInjectionWorld.EntityManager.GetSharedComponentData<FontMaterial>(target.MaterialId);
      EditorGUI.indentLevel++;
      EditorGUILayout.FloatField(nameof(TextRenderer.Size), target.Size);
      EditorGUILayout.EnumFlagsField(nameof(TextRenderer.Alignment), target.Alignment);
      EditorGUILayout.Toggle(nameof(TextRenderer.Bold), target.Bold);
      EditorGUILayout.Toggle(nameof(TextRenderer.Italic), target.Italic);
      EditorGUILayout.ObjectField(nameof(FontMaterial), fontMaterial.Value, typeof(Material), false);
      EditorGUI.indentLevel--;
    }
  }
}
