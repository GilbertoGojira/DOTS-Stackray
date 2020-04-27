using Stackray.Renderer;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

public class UseUnityRenderer : MonoBehaviour {
  private void Awake() {
    World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<RendererSystem>().Enabled = false;

#if ENABLE_HYBRID_RENDERER_V2
    World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<HybridRendererSystem>().Enabled = true;
#else
    World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<RenderMeshSystemV2>().Enabled = true;
#endif
  }
}
