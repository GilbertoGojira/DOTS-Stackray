using Stackray.Entities.Editor;
using Unity.Rendering;
using UnityEditor;
using UnityEngine;

namespace Stackray.Renderer {
  public class RenderMeshEditor : SharedComponentDataEditor<RenderMesh> {
    public override void OnInspectorGUI(RenderMesh target) {
      base.OnInspectorGUI(target);
      EditorGUI.indentLevel++;
      EditorGUILayout.ObjectField(nameof(RenderMesh.mesh), target.mesh, typeof(Mesh), false);
      EditorGUILayout.ObjectField(nameof(RenderMesh.material), target.material, typeof(Material), false);
      EditorGUILayout.IntField(nameof(RenderMesh.subMesh), target.subMesh);
      EditorGUILayout.IntField(nameof(RenderMesh.layer), target.layer);
      EditorGUILayout.EnumFlagsField(nameof(RenderMesh.castShadows), target.castShadows);
      EditorGUILayout.Toggle(nameof(RenderMesh.receiveShadows), target.receiveShadows);
      EditorGUI.indentLevel--;
    }
  }
}
