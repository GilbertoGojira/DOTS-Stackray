using Stackray.Entities.Editor;
using Unity.Mathematics;
using UnityEditor;

namespace Stackray.Sprite.Editor {
  public class TileOffsetPropertyEditor : ComponentDataEditor<TileOffsetProperty> {
    public override void OnInspectorGUI(TileOffsetProperty target) {
      base.OnInspectorGUI(target);
      EditorGUI.indentLevel++;
      EditorGUILayout.Vector4Field(nameof(TileOffsetProperty.Value), (float4)target.Value);
      EditorGUI.indentLevel--;
    }
  }
}
