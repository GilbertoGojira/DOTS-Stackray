using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Stackray.Text {

  public struct TextData : IComponentData {
    public FixedString64 Value;

#if UNITY_EDITOR
    void OnInspectorGUI() {
      var enabled = GUI.enabled;
      GUI.enabled = true;
      UnityEditor.EditorGUILayout.LabelField(
        nameof(TextData),
        new GUIStyle(UnityEditor.EditorStyles.boldLabel) {
          fontStyle = FontStyle.Bold
        });
      GUI.enabled = enabled;
      UnityEditor.EditorGUI.indentLevel++;
      UnityEditor.EditorGUILayout.TextField(nameof(Value), Value.ToString());
      UnityEditor.EditorGUI.indentLevel--;
    }
#endif
  }
}
