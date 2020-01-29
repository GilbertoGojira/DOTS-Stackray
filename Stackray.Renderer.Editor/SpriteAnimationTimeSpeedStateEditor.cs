using Stackray.Entities.Editor;
using UnityEditor;

namespace Stackray.Renderer {
  public class SpriteAnimationTimeSpeedStateEditor : ComponentDataEditor<SpriteAnimationTimeSpeedState> {
    public override void OnInspectorGUI(SpriteAnimationTimeSpeedState target) {
      base.OnInspectorGUI(target);
      EditorGUI.indentLevel++;
      EditorGUILayout.FloatField(nameof(SpriteAnimationTimeSpeedState.Time), target.Time);
      EditorGUILayout.FloatField(nameof(SpriteAnimationTimeSpeedState.Speed), target.Speed);
      EditorGUI.indentLevel--;
    }
  }
}
