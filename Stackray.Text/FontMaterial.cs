using System;
using Unity.Entities;
using UnityEngine;

namespace Stackray.Text {
  public struct FontMaterial : ISharedComponentData, IEquatable<FontMaterial> {
    public Material Value;

    public bool Equals(FontMaterial other) {
      return Equals(Value, other.Value);
    }

    public override bool Equals(object obj) {
      if (ReferenceEquals(null, obj)) return false;
      return obj is FontMaterial other && Equals(other);
    }

    public override int GetHashCode() {
      return (Value != null ? Value.GetHashCode() : 0);
    }
  }
}