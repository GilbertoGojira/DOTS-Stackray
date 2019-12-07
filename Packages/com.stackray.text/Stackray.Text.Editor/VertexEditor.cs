using Stackray.Entities.Editor;
using Stackray.Mathematics;
using Unity.Mathematics;
using UnityEditor;

namespace Stackray.Text {
  class VertexEditor : BufferElementDataEditor<Vertex> {

    public override void OnInspectorGUI(Vertex target, string index) {
      base.OnInspectorGUI(target, index);
      EditorGUI.indentLevel++;
      EditorGUILayout.Vector3Field(nameof(Vertex.Position), target.Position);
      EditorGUILayout.Vector4Field(nameof(Vertex.Normal), (float4)target.Normal);
      EditorGUILayout.ColorField(nameof(Vertex.Color), target.Color.ToColor());
      EditorGUILayout.Vector2Field(nameof(Vertex.TexCoord0), (float2)target.TexCoord0);
      EditorGUILayout.Vector2Field(nameof(Vertex.TexCoord1), (float2)target.TexCoord1);
      EditorGUI.indentLevel--;
    }
  }
}
