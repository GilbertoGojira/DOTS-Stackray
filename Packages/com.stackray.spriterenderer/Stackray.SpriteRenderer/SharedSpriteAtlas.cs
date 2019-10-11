using System;
using Unity.Entities;
using UnityEngine.U2D;

namespace Stackray.SpriteRenderer {
  public struct SharedSpriteAtlas : ISharedComponentData, IEquatable<SharedSpriteAtlas> {
    public SpriteAtlas Value;

    public bool Equals(SharedSpriteAtlas other) {
      return Value == other.Value;
    }

    public override int GetHashCode() {
      int hash = default;

      if (!ReferenceEquals(Value, null))
        hash ^= Value.GetHashCode();
      return hash;
    }
  }
}