using Stackray.Entities.Editor;
using UnityEditor;
using UnityEngine;

namespace Stackray.Sprite.Editor {
  public class SpriteAnimationClipMaterialEditor : SharedComponentDataEditor<SpriteAnimationClipMaterials> {

    public override void OnInspectorGUI(SpriteAnimationClipMaterials target) {
      base.OnInspectorGUI(target);
      EditorGUI.indentLevel++;
      foreach(var material in target.Value)
        EditorGUILayout.ObjectField(nameof(Material), material, typeof(Material), false);
      EditorGUI.indentLevel--;
    }

  }
}
