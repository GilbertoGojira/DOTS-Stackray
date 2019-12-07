using Stackray.Entities.Editor;
using Stackray.Mathematics;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Stackray.Text {
  class VertexEditor : IBufferElementDataEditor<Vertex> {

    public void OnInspectorGUI(Vertex target) {
      var enabled = GUI.enabled;
      GUI.enabled = true;
      EditorGUILayout.LabelField(
        nameof(Vertex),
        new GUIStyle(EditorStyles.boldLabel) {
          fontStyle = FontStyle.Bold
        });
      GUI.enabled = enabled;
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
