using Stackray.Mathematics;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Stackray.Text {
  /// <summary>
  /// 16 seems like a quite big buffer, but it's enough to store 9-slice sprite vertices.
  /// It is the most common sprite type used in UI. I noticed performance increase compared to smaller buffers
  /// </summary>
  [InternalBufferCapacity(16)]
  public struct Vertex : IBufferElementData {
    public float3 Position;
    public half4 Normal;
    public half4 Color;
    public half2 TexCoord0;
    public half2 TexCoord1;

//#if UNITY_EDITOR
//    void OnInspectorGUI() {
//      var enabled = GUI.enabled;
//      GUI.enabled = true;
//      UnityEditor.EditorGUILayout.LabelField(
//        nameof(Vertex),
//        new GUIStyle(UnityEditor.EditorStyles.boldLabel) {
//          fontStyle = FontStyle.Bold
//        });
//      GUI.enabled = enabled;
//      UnityEditor.EditorGUI.indentLevel++;
//      UnityEditor.EditorGUILayout.Vector3Field(nameof(Position), Position);
//      UnityEditor.EditorGUILayout.Vector4Field(nameof(Normal), (float4)Normal);
//      UnityEditor.EditorGUILayout.ColorField(nameof(Color), Color.ToColor());
//      UnityEditor.EditorGUILayout.Vector2Field(nameof(TexCoord0), (float2)TexCoord0);
//      UnityEditor.EditorGUILayout.Vector2Field(nameof(TexCoord1), (float2)TexCoord1);
//      UnityEditor.EditorGUI.indentLevel--;
//    }
//#endif
  }
}