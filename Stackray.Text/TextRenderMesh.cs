using System;
using Unity.Entities;
using UnityEngine;

namespace Stackray.Text {
  public struct TextRenderMesh : ISharedComponentData, IEquatable<TextRenderMesh> {
    public Mesh Mesh;

    public bool Equals(TextRenderMesh other) {
      return Mesh == other.Mesh;
    }
    public override int GetHashCode() {
      int hash = default;
      if (!ReferenceEquals(Mesh, null))
        hash ^= Mesh.GetHashCode();
      return hash;
    }
  }
}