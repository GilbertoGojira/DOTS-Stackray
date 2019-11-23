using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Stackray.Mathematics {
  [Serializable]
  public struct half4x4 : IEquatable<half4x4>, IFormattable {
    public half4 c0;
    public half4 c1;
    public half4 c2;
    public half4 c3;

    /// <summary>half4x4 identity transform.</summary>
    public static readonly half4x4 identity = new half4x4(1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);

    /// <summary>half4x4 zero value.</summary>
    public static readonly half4x4 zero;

    /// <summary>Constructs a half4x4 matrix from four float4 vectors.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public half4x4(float4 c0, float4 c1, float4 c2, float4 c3) {
      this.c0 = (half4)c0;
      this.c1 = (half4)c1;
      this.c2 = (half4)c2;
      this.c3 = (half4)c3;
    }

    /// <summary>Constructs a half4x4 matrix from 16 float values given in row-major order.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public half4x4(float m00, float m01, float m02, float m03,
                    float m10, float m11, float m12, float m13,
                    float m20, float m21, float m22, float m23,
                    float m30, float m31, float m32, float m33) {
      this.c0 = new half4(new float4(m00, m10, m20, m30));
      this.c1 = new half4(new float4(m01, m11, m21, m31));
      this.c2 = new half4(new float4(m02, m12, m22, m32));
      this.c3 = new half4(new float4(m03, m13, m23, m33));
    }

    /// <summary>Constructs a half4x4 matrix from a single float value by assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public half4x4(float v) {
      this.c0 = (half4)v;
      this.c1 = (half4)v;
      this.c2 = (half4)v;
      this.c3 = (half4)v;
    }

    /// <summary>Constructs a half4x4 matrix from a single bool value by converting it to float and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public half4x4(bool v) {
      this.c0 = (half4)math.select(new float4(0.0f), new float4(1.0f), v);
      this.c1 = (half4)math.select(new float4(0.0f), new float4(1.0f), v);
      this.c2 = (half4)math.select(new float4(0.0f), new float4(1.0f), v);
      this.c3 = (half4)math.select(new float4(0.0f), new float4(1.0f), v);
    }

    /// <summary>Constructs a half4x4 matrix from a bool4x4 matrix by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public half4x4(bool4x4 v) {
      this.c0 = (half4)math.select(new float4(0.0f), new float4(1.0f), v.c0);
      this.c1 = (half4)math.select(new float4(0.0f), new float4(1.0f), v.c1);
      this.c2 = (half4)math.select(new float4(0.0f), new float4(1.0f), v.c2);
      this.c3 = (half4)math.select(new float4(0.0f), new float4(1.0f), v.c3);
    }

    /// <summary>Constructs a half4x4 matrix from a single int value by converting it to float and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public half4x4(int v) {
      this.c0 = (half4)v;
      this.c1 = (half4)v;
      this.c2 = (half4)v;
      this.c3 = (half4)v;
    }

    /// <summary>Constructs a half4x4 matrix from a int4x4 matrix by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public half4x4(int4x4 v) {
      this.c0 = new half4(v.c0);
      this.c1 = new half4(v.c1);
      this.c2 = new half4(v.c2);
      this.c3 = new half4(v.c3);
    }

    /// <summary>Constructs a half4x4 matrix from a single uint value by converting it to float and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public half4x4(uint v) {
      this.c0 = (half4)v;
      this.c1 = (half4)v;
      this.c2 = (half4)v;
      this.c3 = (half4)v;
    }

    /// <summary>Constructs a half4x4 matrix from a uint4x4 matrix by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public half4x4(uint4x4 v) {
      this.c0 = new half4(v.c0);
      this.c1 = new half4(v.c1);
      this.c2 = new half4(v.c2);
      this.c3 = new half4(v.c3);
    }

    /// <summary>Constructs a half4x4 matrix from a single double value by converting it to float and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public half4x4(double v) {
      this.c0 = (half4)v;
      this.c1 = (half4)v;
      this.c2 = (half4)v;
      this.c3 = (half4)v;
    }

    /// <summary>Constructs a half4x4 matrix from a double4x4 matrix by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public half4x4(double4x4 v) {
      this.c0 = (half4)v.c0;
      this.c1 = (half4)v.c1;
      this.c2 = (half4)v.c2;
      this.c3 = (half4)v.c3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public half4x4(float4x4 v) {
      this.c0 = (half4)v.c0;
      this.c1 = (half4)v.c1;
      this.c2 = (half4)v.c2;
      this.c3 = (half4)v.c3;
    }


    /// <summary>Implicitly converts a single float value to a half4x4 matrix by assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator half4x4(float v) { return new half4x4(v); }

    /// <summary>Explicitly converts a single bool value to a half4x4 matrix by converting it to float and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator half4x4(bool v) { return new half4x4(v); }

    /// <summary>Explicitly converts a bool4x4 matrix to a half4x4 matrix by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator half4x4(bool4x4 v) { return new half4x4(v); }

    /// <summary>Implicitly converts a single int value to a half4x4 matrix by converting it to float and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator half4x4(int v) { return new half4x4(v); }

    /// <summary>Implicitly converts a int4x4 matrix to a half4x4 matrix by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator half4x4(int4x4 v) { return new half4x4(v); }

    /// <summary>Implicitly converts a single uint value to a half4x4 matrix by converting it to float and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator half4x4(uint v) { return new half4x4(v); }

    /// <summary>Implicitly converts a uint4x4 matrix to a half4x4 matrix by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator half4x4(uint4x4 v) { return new half4x4(v); }

    /// <summary>Explicitly converts a single double value to a half4x4 matrix by converting it to float and assigning it to every component.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator half4x4(double v) { return new half4x4(v); }

    /// <summary>Explicitly converts a double4x4 matrix to a half4x4 matrix by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator half4x4(double4x4 v) { return new half4x4(v); }

    /// <summary>Explicitly converts a float4x4 matrix to a half4x4 matrix by componentwise conversion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator half4x4(float4x4 v) { return new half4x4(v); }

    /// <summary>Returns the result of a componentwise equality operation on two half4x4 matrices.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4x4 operator ==(half4x4 lhs, half4x4 rhs) { return new bool4x4(lhs.c0 == rhs.c0, lhs.c1 == rhs.c1, lhs.c2 == rhs.c2, lhs.c3 == rhs.c3); }

    /// <summary>Returns the result of a componentwise equality operation on a half4x4 matrix and a float value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4x4 operator ==(half4x4 lhs, float rhs) { return new bool4x4(lhs.c0 == (half4)rhs, lhs.c1 == (half4)rhs, lhs.c2 == (half4)rhs, lhs.c3 == (half4)rhs); }

    /// <summary>Returns the result of a componentwise equality operation on a float value and a half4x4 matrix.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4x4 operator ==(float lhs, half4x4 rhs) { return new bool4x4((half4)lhs == rhs.c0, (half4)lhs == rhs.c1, (half4)lhs == rhs.c2, (half4)lhs == rhs.c3); }


    /// <summary>Returns the result of a componentwise not equal operation on two half4x4 matrices.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4x4 operator !=(half4x4 lhs, half4x4 rhs) { return new bool4x4(lhs.c0 != rhs.c0, lhs.c1 != rhs.c1, lhs.c2 != rhs.c2, lhs.c3 != rhs.c3); }

    /// <summary>Returns the result of a componentwise not equal operation on a half4x4 matrix and a float value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4x4 operator !=(half4x4 lhs, float rhs) { return new bool4x4(lhs.c0 != (half4)rhs, lhs.c1 != (half4)rhs, lhs.c2 != (half4)rhs, lhs.c3 != (half4)rhs); }

    /// <summary>Returns the result of a componentwise not equal operation on a float value and a half4x4 matrix.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool4x4 operator !=(float lhs, half4x4 rhs) { return new bool4x4((half4)lhs != rhs.c0, (half4)lhs != rhs.c1, (half4)lhs != rhs.c2, (half4)lhs != rhs.c3); }



    /// <summary>Returns the float4 element at a specified index.</summary>
    unsafe public ref half4 this[int index] {
      get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        if ((uint)index >= 4)
          throw new System.ArgumentException("index must be between[0...3]");
#endif
        fixed (half4x4* array = &this) { return ref ((half4*)array)[index]; }
      }
    }

    /// <summary>Returns true if the half4x4 is equal to a given half4x4, false otherwise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(half4x4 rhs) { return c0.Equals(rhs.c0) && c1.Equals(rhs.c1) && c2.Equals(rhs.c2) && c3.Equals(rhs.c3); }

    /// <summary>Returns true if the half4x4 is equal to a given half4x4, false otherwise.</summary>
    public override bool Equals(object o) { return Equals((half4x4)o); }


    /// <summary>Returns a hash code for the half4x4.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() {
      var hash = math.csum(math.asuint(c0) * math.uint4(0xC4B1493Fu, 0xBA0966D3u, 0xAFBEE253u, 0x5B419C01u) +
                        math.asuint(c1) * math.uint4(0x515D90F5u, 0xEC9F68F3u, 0xF9EA92D5u, 0xC2FAFCB9u) +
                        math.asuint(c2) * math.uint4(0x616E9CA1u, 0xC5C5394Bu, 0xCAE78587u, 0x7A1541C9u) +
                        math.asuint(c3) * math.uint4(0xF83BD927u, 0x6A243BCBu, 0x509B84C9u, 0x91D13847u)) + 0x52F7230Fu;
      return (int)hash;
    }


    /// <summary>Returns a string representation of the half4x4.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() {
      return string.Format("half4x4({0}f, {1}f, {2}f, {3}f,  {4}f, {5}f, {6}f, {7}f,  {8}f, {9}f, {10}f, {11}f,  {12}f, {13}f, {14}f, {15}f)", c0.x, c1.x, c2.x, c3.x, c0.y, c1.y, c2.y, c3.y, c0.z, c1.z, c2.z, c3.z, c0.w, c1.w, c2.w, c3.w);
    }

    /// <summary>Returns a string representation of the half4x4 using a specified format and culture-specific format information.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToString(string format, IFormatProvider formatProvider) {
      return string.Format("half4x4({0}f, {1}f, {2}f, {3}f,  {4}f, {5}f, {6}f, {7}f,  {8}f, {9}f, {10}f, {11}f,  {12}f, {13}f, {14}f, {15}f)", c0.x.ToString(format, formatProvider), c1.x.ToString(format, formatProvider), c2.x.ToString(format, formatProvider), c3.x.ToString(format, formatProvider), c0.y.ToString(format, formatProvider), c1.y.ToString(format, formatProvider), c2.y.ToString(format, formatProvider), c3.y.ToString(format, formatProvider), c0.z.ToString(format, formatProvider), c1.z.ToString(format, formatProvider), c2.z.ToString(format, formatProvider), c3.z.ToString(format, formatProvider), c0.w.ToString(format, formatProvider), c1.w.ToString(format, formatProvider), c2.w.ToString(format, formatProvider), c3.w.ToString(format, formatProvider));
    }
  }
}
