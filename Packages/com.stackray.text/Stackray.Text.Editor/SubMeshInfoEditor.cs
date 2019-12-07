using Stackray.Entities.Editor;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Stackray.Text {
  class SubMeshInfoEditor : BufferElementDataEditor<SubMeshInfo> {

    public override void OnInspectorGUI(SubMeshInfo target, string index) {
      base.OnInspectorGUI(target, index);
      var fontMaterial = World.DefaultGameObjectInjectionWorld.EntityManager.GetSharedComponentData<FontMaterial>(target.MaterialId);
      EditorGUI.indentLevel++;
      EditorGUILayout.IntField(nameof(SubMeshInfo.VertexCount), target.VertexCount);
      EditorGUILayout.IntField(nameof(SubMeshInfo.Offset), target.Offset);
      EditorGUILayout.ObjectField(nameof(FontMaterial), fontMaterial.Value, typeof(Material), false);
      EditorGUI.indentLevel--;
    }
  }
}
