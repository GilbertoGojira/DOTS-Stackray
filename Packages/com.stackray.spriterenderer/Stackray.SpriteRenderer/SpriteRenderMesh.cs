using System;
using Unity.Entities;
using UnityEngine;

namespace Stackray.SpriteRenderer {
  public struct SpriteRenderMesh : ISharedComponentData, IEquatable<SpriteRenderMesh> {
    public Mesh Mesh;
    public Material Material;
    public bool Equals(SpriteRenderMesh other) {
      return
        Material == other.Material &&
        Mesh == other.Mesh;
    }
    public override int GetHashCode() {
      int hash = default;
      if (!ReferenceEquals(Mesh, null))
        hash ^= Mesh.GetHashCode();
      if (!ReferenceEquals(Material, null))
        hash ^= Material.GetHashCode();
      return hash;
    }
  }
}
